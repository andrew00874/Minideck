using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MiniDeck {
 sealed class LocalTranslationUnavailableException:InvalidOperationException {
  public LocalTranslationUnavailableException(string message):base(message){}
 }
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
  internal static string TestRoot;
  static string Root {get{return TestRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiniDeck","translation");}}
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
   if(settings==null||(settings.Provider!="MiniDeck"&&settings.Provider!="OpenAI"))return null;
   Installation installed=ReadManifest();
   if(settings.Provider!="MiniDeck"&&(installed==null||String.IsNullOrWhiteSpace(installed.BaseUrl)||!String.Equals(installed.BaseUrl.TrimEnd('/'),(settings.BaseUrl??"").TrimEnd('/'),StringComparison.OrdinalIgnoreCase)))return null;
   if(installed==null)throw new InvalidOperationException(L10n.T("번역 모델 설치 파일을 찾지 못했습니다. 로컬 번역 설치를 다시 실행하세요."));
   ValidateInstallation(installed);
   return installed;
  }
  public static bool ApplyInstalledDefault(LocalTranslationSettings settings){
   try{
    Installation installed=ReadManifest();if(installed==null)return false;
    ValidateInstallation(installed);Key(installed);
    settings.Provider="MiniDeck";settings.BaseUrl=installed.BaseUrl;settings.Model=installed.ModelId;return true;
   }catch(Exception){return false;/* An incomplete optional installation must not break startup. */}
  }
  public static bool IsManaged(LocalTranslationSettings settings){return ReadInstallation(settings)!=null;}
  // Resolve managed settings on every request; stale saved ports/model names must
  // never turn the installed engine into a manually managed external connection.
  public static void Resolve(LocalTranslationSettings settings){
   Installation installed=ReadInstallation(settings);if(installed==null)return;
   settings.Provider="MiniDeck";settings.BaseUrl=installed.BaseUrl;settings.Model=installed.ModelId;
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
  public static async Task EnsureRunningAsync(LocalTranslationSettings settings,CancellationToken token,IProgress<string> progress=null){
   Installation installed=ReadInstallation(settings);if(installed==null)return;
   await starting.WaitAsync(token).ConfigureAwait(false);
   try{
    if(await Ready(installed,token).ConfigureAwait(false))return;
    if(progress!=null)progress.Report(L10n.T("내장 번역 엔진을 준비하는 중… 자동으로 시작합니다."));
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
      catch(Exception error){
       AppDiagnostics.Exception("model-start",error);
       try{if(!child.HasExited)child.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
       child.Dispose();owned=null;throw new LocalTranslationUnavailableException(L10n.T("내장 번역 엔진을 준비하지 못했습니다. 다시 번역을 눌러 주세요."));
      }
     }
     running=owned;
    }
    Stopwatch watch=Stopwatch.StartNew();
    while(watch.ElapsedMilliseconds<90000){
     token.ThrowIfCancellationRequested();
     if(running.HasExited)throw new LocalTranslationUnavailableException(L10n.T("내장 번역 엔진을 준비하지 못했습니다. 다시 번역을 눌러 주세요."));
     if(await Ready(installed,token).ConfigureAwait(false))return;
     await Task.Delay(250,token).ConfigureAwait(false);
    }
    // Reap only our failed child. An unrelated listener is never terminated.
    ReleaseFailed(running);
    throw new LocalTranslationUnavailableException(L10n.T("내장 번역 엔진을 준비하지 못했습니다. 다시 번역을 눌러 주세요."));
   }finally{starting.Release();}
  }
  static bool OwnedExited(){lock(lifetime){return owned!=null&&owned.HasExited;}}
  static void ReleaseFailed(Process failed){
   lock(lifetime){if(owned!=failed)return;owned=null;}
   try{if(!failed.HasExited)failed.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}finally{failed.Dispose();}
  }
  // Check process lifetime only: readiness polling would wake an idle model.
  // Limit background recovery so a persistent GPU failure cannot restart forever.
  public static async Task SuperviseAsync(LocalTranslationSettings settings,CancellationToken token){
   settings=settings.Clone();Resolve(settings);if(!IsManaged(settings))return;
   bool initial=true;var attempts=new Queue<DateTime>();
   while(true){
    token.ThrowIfCancellationRequested();
    while(attempts.Count>0&&DateTime.UtcNow-attempts.Peek()>TimeSpan.FromMinutes(5))attempts.Dequeue();
    if((initial||OwnedExited())&&attempts.Count<3){
     initial=false;attempts.Enqueue(DateTime.UtcNow);
     try{await EnsureRunningAsync(settings,token).ConfigureAwait(false);}
     catch(OperationCanceledException){throw;}
     catch(Exception error){initial=true;AppDiagnostics.Exception("model-supervision",error);}
    }
    await Task.Delay(3000,token).ConfigureAwait(false);
   }
  }
  public static void Stop(){
   Process process;lock(lifetime){stopping=true;process=owned;owned=null;}
   if(process!=null){try{AppDiagnostics.Runtime("stop-requested",process.Id);if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}finally{process.Dispose();}}
  }
 }
}
