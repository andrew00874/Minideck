using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MiniDeck {
 // Only the separately installed MiniDeck translation runtime is started here.
 // Custom Ollama/OpenAI-compatible servers remain under the user's control.
 static class LocalModelRuntime {
  public sealed class Installation {
   public string Executable {get;set;}
   public string ModelPath {get;set;}
   public string ModelId {get;set;}
   public string BaseUrl {get;set;}
   public string ApiKeyFile {get;set;}
   public string GpuDevice {get;set;}
   public int IdleSeconds {get;set;}
   public Installation(){GpuDevice="Vulkan0";IdleSeconds=900;}
  }
  static readonly SemaphoreSlim starting=new SemaphoreSlim(1,1);
  static readonly object lifetime=new object();
  static Process owned;
  static bool stopping;
  static string Root {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiniDeck","translation");}}
  static bool WithinRoot(string path){return !String.IsNullOrWhiteSpace(path)&&Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  static Installation ReadManifest(){
   string file=Path.Combine(Root,"runtime.json");
   if(!File.Exists(file)||new FileInfo(file).Length>16384)return null;
   return new JavaScriptSerializer().Deserialize<Installation>(File.ReadAllText(file));
  }
  static void ValidateInstallation(Installation installed){
   if(!WithinRoot(installed.Executable)||!WithinRoot(installed.ModelPath)||
      !String.Equals(Path.GetFileName(installed.Executable),"llama-server.exe",StringComparison.OrdinalIgnoreCase)||
      !String.Equals(Path.GetExtension(installed.ModelPath),".gguf",StringComparison.OrdinalIgnoreCase)||
      String.IsNullOrWhiteSpace(installed.ModelId)||!System.Text.RegularExpressions.Regex.IsMatch(installed.ModelId,@"^[A-Za-z0-9._-]+$")||
      String.IsNullOrWhiteSpace(installed.GpuDevice)||!System.Text.RegularExpressions.Regex.IsMatch(installed.GpuDevice,@"^Vulkan[0-9]+$")||
      installed.IdleSeconds<60||installed.IdleSeconds>3600||
      !File.Exists(installed.Executable)||!File.Exists(installed.ModelPath)||!WithinRoot(installed.ApiKeyFile)||!File.Exists(installed.ApiKeyFile))
    throw new InvalidOperationException(L10n.T("번역 모델 설치 파일을 찾지 못했습니다. 로컬 번역 설치를 다시 실행하세요."));
   Uri endpoint=new Uri(installed.BaseUrl);
   if(endpoint.Host!="127.0.0.1"||endpoint.Scheme!="http"||endpoint.AbsolutePath!="/"||endpoint.UserInfo!=""||endpoint.Query!=""||endpoint.Fragment!="")throw new InvalidOperationException(L10n.T("로컬 번역 설정을 확인해 주세요."));
  }
  static Installation ReadInstallation(LocalTranslationSettings settings){
   if(settings.Provider!="OpenAI")return null;
   Installation installed=ReadManifest();
   if(installed==null||String.IsNullOrWhiteSpace(installed.BaseUrl)||!String.Equals(installed.BaseUrl.TrimEnd('/'),settings.BaseUrl.TrimEnd('/'),StringComparison.OrdinalIgnoreCase))return null;
   ValidateInstallation(installed);
   return installed;
  }
  public static void ApplyInstalledDefault(LocalTranslationSettings settings){
   try{
    Installation installed=ReadManifest();if(installed==null)return;
    ValidateInstallation(installed);Key(installed);
    settings.Provider="OpenAI";settings.BaseUrl=installed.BaseUrl;settings.Model=installed.ModelId;
   }catch(Exception){/* An incomplete optional installation must not break startup. */}
  }
  static string Key(Installation installed){
   string key=File.ReadAllText(installed.ApiKeyFile).Trim();
   if(!System.Text.RegularExpressions.Regex.IsMatch(key,@"^[a-f0-9]{64}$"))throw new InvalidOperationException(L10n.T("로컬 번역 설정을 확인해 주세요."));
   return key;
  }
  public static string Authorization(LocalTranslationSettings settings){Installation installed=ReadInstallation(settings);return installed==null?null:Key(installed);}
  static async Task<bool> Ready(Installation installed,CancellationToken token){
   var request=(HttpWebRequest)WebRequest.Create(installed.BaseUrl.TrimEnd('/')+"/v1/models");
   request.Proxy=null;request.AllowAutoRedirect=false;request.Timeout=1000;request.ReadWriteTimeout=1000;
   request.Headers[HttpRequestHeader.Authorization]="Bearer "+Key(installed);
   using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
    timeout.CancelAfter(1000);
    using(timeout.Token.Register(request.Abort))try{
     using(var response=(HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
     using(var reader=new StreamReader(response.GetResponseStream())){
      char[] buffer=new char[32769];int length=0,count;
      while(length<buffer.Length&&(count=await reader.ReadAsync(buffer,length,buffer.Length-length).ConfigureAwait(false))>0)length+=count;
      if(length>32768)return false;
      var data=new JavaScriptSerializer().DeserializeObject(new string(buffer,0,length)) as Dictionary<string,object>;
      object value;var models=data!=null&&data.TryGetValue("data",out value)?value as object[]:null;
      if(models!=null)foreach(object model in models){var row=model as Dictionary<string,object>;if(row!=null&&row.TryGetValue("id",out value)&&String.Equals(value as string,installed.ModelId,StringComparison.Ordinal))return true;}
     }
    }catch(WebException){}catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}
   }
   token.ThrowIfCancellationRequested();return false;
  }
  public static async Task EnsureRunningAsync(LocalTranslationSettings settings,CancellationToken token){
   Installation installed=ReadInstallation(settings);if(installed==null)return;
   await starting.WaitAsync(token).ConfigureAwait(false);
   try{
    if(await Ready(installed,token).ConfigureAwait(false))return;
    token.ThrowIfCancellationRequested();
    Process running;
    lock(lifetime){
     token.ThrowIfCancellationRequested();if(stopping)throw new OperationCanceledException();
     if(owned==null||owned.HasExited){
      var info=new ProcessStartInfo(installed.Executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Root,RedirectStandardError=true,RedirectStandardOutput=true};
      info.Arguments="-m \""+installed.ModelPath+"\" --host 127.0.0.1 --port "+new Uri(installed.BaseUrl).Port+
       " --alias "+installed.ModelId+" -c 16384 -np 1 -ngl 99 --device "+installed.GpuDevice+" --no-webui --offline --sleep-idle-seconds "+installed.IdleSeconds+" --reasoning off --reasoning-budget 0 --verbosity 0 --api-key-file \""+installed.ApiKeyFile+"\"";
      if(owned!=null)owned.Dispose();
      var child=new Process{StartInfo=info,EnableRaisingEvents=true};
      DataReceivedEventHandler errorLine=delegate(object sender,DataReceivedEventArgs e){try{string kind=AppDiagnostics.ClassifyRuntimeError(e.Data);if(kind!=null)AppDiagnostics.Runtime(kind,child.Id);}catch(Exception){}};
      child.ErrorDataReceived+=errorLine;child.OutputDataReceived+=errorLine;
      child.Exited+=delegate{try{AppDiagnostics.Runtime(stopping?"stopped":"exited",child.Id,child.ExitCode);}catch(Exception){}};
      try{if(!child.Start())throw new InvalidOperationException("Model process did not start");owned=child;child.BeginErrorReadLine();child.BeginOutputReadLine();AppDiagnostics.Runtime("started",child.Id);}
      catch(Exception error){AppDiagnostics.Exception("model-start",error);child.Dispose();owned=null;throw;}
     }
     running=owned;
    }
    Stopwatch watch=Stopwatch.StartNew();
    while(watch.ElapsedMilliseconds<90000){
     token.ThrowIfCancellationRequested();
     if(running.HasExited)throw new InvalidOperationException(L10n.T("번역 모델을 시작하지 못했습니다. GPU와 로컬 서버 포트를 확인하세요."));
     if(await Ready(installed,token).ConfigureAwait(false))return;
     await Task.Delay(250,token).ConfigureAwait(false);
    }
    throw new TimeoutException(L10n.T("번역 모델을 준비하는 데 시간이 오래 걸립니다. 잠시 후 다시 시도해 주세요."));
   }finally{starting.Release();}
  }
  public static void Stop(){
   Process process;lock(lifetime){stopping=true;process=owned;owned=null;}
   if(process!=null){try{AppDiagnostics.Runtime("stop-requested",process.Id);if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}finally{process.Dispose();}}
  }
 }
}
