using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MiniDeck {
 static class LocalModelRuntimeTests {
  sealed class Updates:IProgress<string>{readonly Action<string> next;public Updates(Action<string> action){next=action;}public void Report(string value){next(value);}}
  static void Check(bool value,string message){if(!value)throw new Exception("Managed runtime: "+message);}
  static int Lines(string root,string name){string file=Path.Combine(root,name);if(!File.Exists(file))return 0;using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(input)){int count=0;while(reader.ReadLine()!=null)count++;return count;}}
  static async Task Until(Func<bool> ready,string message){var timer=Stopwatch.StartNew();while(!ready()&&timer.ElapsedMilliseconds<12000)await Task.Delay(50);Check(ready(),message);}
  public static void Run(string folder,string fixture){
   AppDiagnostics.TestSink=delegate{};
   try{RunAsync(folder,fixture).GetAwaiter().GetResult();}finally{AppDiagnostics.TestSink=null;}
  }
  static async Task RunAsync(string folder,string fixture){
   string root=Path.Combine(folder,"runtime-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   string executable=Path.Combine(root,"llama-server.exe");File.Copy(fixture,executable);
   string model=Path.Combine(root,"fixture.gguf"),keyFile=Path.Combine(root,"key.txt"),modeFile=Path.Combine(root,"mode.txt");
   File.WriteAllText(model,"fixture");File.WriteAllText(keyFile,new string('a',64));File.WriteAllText(modeFile,"normal");
   var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
   string endpoint="http://127.0.0.1:"+port;
   var manifest=new LocalModelRuntime.Installation{Executable=executable,ModelPath=model,ModelId="test-model",BaseUrl=endpoint,ApiKeyFile=keyFile,GpuDevice="Vulkan0",IdleSeconds=900};
   File.WriteAllText(Path.Combine(root,"runtime.json"),new JavaScriptSerializer().Serialize(manifest));
   LocalModelRuntime.TestRoot=root;
   try{
    var settings=new LocalTranslationSettings{Provider="MiniDeck",BaseUrl="http://127.0.0.1:1",Model="stale-model",TargetLanguage="fr"};
    var progress=new List<string>();var previews=new List<string>();
    var external=settings.Clone();external.Provider="Ollama";external.BaseUrl=endpoint;bool externalFailed=false;
    try{await LocalTranslationClient.TranslateAsync(external,"hello",CancellationToken.None);}catch(LocalTranslationUnavailableException){externalFailed=true;}
    Check(externalFailed&&Lines(root,"starts.txt")==0,"external endpoints must not start the installed engine");
    string result=await LocalTranslationClient.TranslateAsync(settings,"hello",CancellationToken.None,new Updates(progress.Add),new Updates(previews.Add));
    Check(result=="Bonjour"&&progress.Count>1,"stopped engine did not auto-start with preparation feedback");
    Check(settings.BaseUrl=="http://127.0.0.1:1"&&settings.Model=="stale-model","request resolution mutated saved settings");
    Check(Lines(root,"starts.txt")==1,"initial engine start was duplicated");
    var legacy=settings.Clone();legacy.Provider="OpenAI";legacy.BaseUrl=endpoint;LocalModelRuntime.Resolve(legacy);
    Check(legacy.Provider=="MiniDeck"&&legacy.Model=="test-model","legacy installed endpoint was not recognized");
    await Task.WhenAll(LocalModelRuntime.EnsureRunningAsync(legacy,CancellationToken.None),LocalModelRuntime.EnsureRunningAsync(legacy,CancellationToken.None));
    Check(Lines(root,"starts.txt")==1,"concurrent requests started duplicate engines");
    File.WriteAllText(modeFile,"crash-once");previews.Clear();progress.Clear();
    result=await LocalTranslationClient.TranslateAsync(settings,"hello",CancellationToken.None,new Updates(progress.Add),new Updates(previews.Add));
    Check(result=="Bonjour"&&Lines(root,"starts.txt")==2,"stream crash was not restarted and retried");
    Check(previews.Contains("Partial")&&previews.Contains("")&&previews[previews.Count-1]=="Bonjour","partial output was not cleared before retry");
    Check(progress.Exists(value=>value.Contains(L10n.T("내장 번역 엔진을 다시 연결하는 중… 자동으로 재시도합니다."))),"recovery status was not reported");
    File.WriteAllText(modeFile,"drop-always");int requests=Lines(root,"requests.txt");bool failed=false;
    try{await LocalTranslationClient.TranslateAsync(settings,"hello",CancellationToken.None,null,new Updates(value=>{}));}
    catch(InvalidOperationException error){failed=error.Message==L10n.T("내장 번역 엔진을 복구하지 못했습니다. 다시 번역을 눌러 주세요.");}
    Check(failed&&Lines(root,"requests.txt")==requests+2,"recovery must stop after one retry with a managed-engine error");
    File.WriteAllText(modeFile,"missing-model");requests=Lines(root,"requests.txt");
    try{await LocalTranslationClient.TranslateAsync(settings,"hello",CancellationToken.None,null,new Updates(value=>{}));}catch(InvalidOperationException){}
    Check(Lines(root,"requests.txt")==requests+1,"configuration errors must not be retried");
    File.WriteAllText(modeFile,"cancel");requests=Lines(root,"requests.txt");
    using(var cancel=new CancellationTokenSource()){
     Task<string> pending=LocalTranslationClient.TranslateAsync(settings,"hello",cancel.Token,null,new Updates(value=>{}));
     await Until(()=>Lines(root,"requests.txt")>requests,"cancel test did not reach engine");cancel.Cancel();
     bool cancelled=false;try{await pending;}catch(OperationCanceledException){cancelled=true;}
     await Task.Delay(400);Check(cancelled&&Lines(root,"requests.txt")==requests+1,"cancellation started an automatic retry");
    }
    File.WriteAllText(modeFile,"normal");
    using(var supervisor=new CancellationTokenSource()){
     Task monitor=LocalModelRuntime.SuperviseAsync(legacy,supervisor.Token);
     requests=Lines(root,"starts.txt");
     var request=(HttpWebRequest)WebRequest.Create(endpoint+"/test/exit");request.Proxy=null;request.Headers[HttpRequestHeader.Authorization]="Bearer "+new string('a',64);
     using(var response=(HttpWebResponse)await request.GetResponseAsync()){}
     await Until(()=>Lines(root,"starts.txt")>requests,"idle engine exit was not recovered in background");
     result=await LocalTranslationClient.TranslateAsync(settings,"hello",CancellationToken.None,null,new Updates(value=>{}));
     Check(result=="Bonjour","background recovery did not restore translation");
     supervisor.Cancel();try{await monitor;}catch(OperationCanceledException){}
    }
    File.WriteAllText(modeFile,"load-fail");int starts=Lines(root,"starts.txt");
    var exit=(HttpWebRequest)WebRequest.Create(endpoint+"/test/exit");exit.Proxy=null;exit.Headers[HttpRequestHeader.Authorization]="Bearer "+new string('a',64);
    using(var response=(HttpWebResponse)await exit.GetResponseAsync()){}
    using(var supervisor=new CancellationTokenSource()){
     Task monitor=LocalModelRuntime.SuperviseAsync(legacy,supervisor.Token);
     await Until(()=>Lines(root,"starts.txt")>=starts+3,"background recovery did not retry failed startups");
     await Task.Delay(3500);Check(Lines(root,"starts.txt")==starts+3,"background recovery exceeded its restart budget");
     supervisor.Cancel();try{await monitor;}catch(OperationCanceledException){}
    }
    File.WriteAllText(Path.Combine(folder,"runtime-tests.txt"),"PASS: managed cold start, stale settings, legacy migration, concurrent start, process crash, partial reset, bounded retry, configuration errors, cancellation, background recovery, restart budget, external-server isolation");
   }finally{LocalModelRuntime.Stop();LocalModelRuntime.TestRoot=null;}
  }
 }
}
