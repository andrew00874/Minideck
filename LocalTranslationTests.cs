using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MiniDeck {
 // Exercises the real HTTP transport against a short-lived loopback server.
 // No model, browser, settings file or external network is used by these tests.
 static class LocalTranslationTests {
  public static void Run(){Task.Run((Func<Task>)RunAsync).GetAwaiter().GetResult();}
  // Call on the UI thread after WinForms has installed its synchronization context.
  public static void RunUi(){
   string newest="private latest selection 4219",translation="Latest translation 4219";
   using(var oldServer=new Server(200,"",null,true))
   using(var nextServer=new Server(200,"{\"message\":{\"content\":\""+translation+"\"}}"))
   using(var retryServer=new Server(200,"{\"choices\":[{\"message\":{\"content\":\"Retried translation 4219\"}}]}"))
   using(var form=new TranslationResultForm(delegate{})){
    LocalTranslationSettings oldSettings=Settings(oldServer.Address,"Ollama"),nextSettings=Settings(nextServer.Address,"Ollama");
    Settings persisted=new Settings{LocalTranslation=nextSettings};
    string before=new JavaScriptSerializer().Serialize(persisted);
    form.BeginTranslation("private previous selection 4219",oldSettings);
    PumpUntil(()=>oldServer.Received.IsCompleted,3000,"first UI request did not reach local server");
    Check(form.IsTranslating,"loading state was not shown");
    form.BeginTranslation(newest,nextSettings);
    PumpUntil(()=>!form.IsTranslating,5000,"latest UI request did not complete");
    PumpUntil(()=>oldServer.Completion.IsCompleted,3000,"new selection did not abort previous request");
    Application.DoEvents();
    Check(form.ResultText==translation&&form.SourceText==newest,"an older request overwrote the latest selection or result");
    Check(!String.IsNullOrWhiteSpace(form.StatusText),"completion state was not shown");
    LocalTranslationSettings retrySettings=nextSettings.Clone();
    retrySettings.Provider="OpenAI";retrySettings.BaseUrl=retryServer.Address;retrySettings.Model="retry-model";
    form.RetryWithSettings(retrySettings);
    PumpUntil(()=>!form.IsTranslating,5000,"retry with updated settings did not complete");
    Check(form.ResultText=="Retried translation 4219"&&form.SourceText==newest,"retry did not reuse the selected text with updated settings");
    Check(retryServer.Received.IsCompleted&&!retryServer.Received.IsFaulted,"retry did not use the new endpoint");
    CheckTranslationRequest(retryServer.Received.GetAwaiter().GetResult(),"/v1/chat/completions",newest,"retry-model");
    Check(nextSettings.Provider=="Ollama"&&nextSettings.BaseUrl==nextServer.Address&&nextSettings.Model=="test-model","retry mutated the original settings snapshot");
    string after=new JavaScriptSerializer().Serialize(persisted);
    Check(before==after&&!after.Contains(newest)&&!after.Contains(translation),"selection or result leaked into persistent settings");
    form.Close();
    Check(!form.HasSelection&&form.SourceText==""&&form.ResultText=="","closing did not clear selection and translation");
   }
   using(var server=new Server(200,"",null,true))using(var form=new TranslationResultForm(delegate{})){
    form.BeginTranslation("private close selection 4219",Settings(server.Address,"Ollama"));
    PumpUntil(()=>server.Received.IsCompleted,3000,"closing test did not reach local server");
    form.Close();
    PumpUntil(()=>server.Completion.IsCompleted,3000,"closing result window did not abort HTTP request");
    Check(!form.HasSelection&&form.SourceText==""&&form.ResultText=="","closing an active request retained private text");
   }
   using(var server=new Server(200,"{\"message\":{\"content\":\"must not appear\"}}"))
   using(var form=new TranslationResultForm(delegate{})){
    LocalTranslationSettings settings=Settings(server.Address,"Ollama");settings.Model="";
    form.BeginTranslation("private no-model selection 4219",settings);
    PumpUntil(()=>!form.IsTranslating,3000,"missing model left the result window busy");
    Check(!server.Received.IsCompleted&&form.ResultText==""&&!String.IsNullOrWhiteSpace(form.StatusText),"missing model was not displayed as a local setup error");
    form.Close();
   }
  }
  static void PumpUntil(Func<bool> condition,int milliseconds,string failure){
   Stopwatch timer=Stopwatch.StartNew();
   while(!condition()&&timer.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(10);}
   Application.DoEvents();Check(condition(),failure);
  }
  static void Check(bool value,string message){if(!value)throw new Exception("Local translation: "+message);}
  static LocalTranslationSettings Settings(string address,string provider){return new LocalTranslationSettings{BaseUrl=address,Provider=provider,Model="test-model",TargetLanguage="French"};}
  static async Task MustFail(Func<Task> action,string message){
   bool failed=false;try{await action();}catch(Exception){failed=true;}
   Check(failed,message);
  }
  static async Task MustRejectArgument(Func<Task> action,string message){
   bool failed=false;try{await action();}catch(ArgumentException){failed=true;}
   Check(failed,message);
  }
  static Dictionary<string,object> Json(string body){return (Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(body);}
  static void CheckTranslationRequest(Request request,string path,string sample,string requestedModel="test-model"){
   Check(request.Method=="POST"&&request.Path==path,"incorrect translation route");
   Check(request.Headers.IndexOf("application/json",StringComparison.OrdinalIgnoreCase)>=0,"missing JSON content type");
   Dictionary<string,object> body=Json(request.Body);
   Check((string)body["model"]==requestedModel,"model was not passed through");
   Check(body.ContainsKey("stream")&&body["stream"] is bool&&!(bool)body["stream"],"translation must request a complete JSON response");
   object[] messages=(object[])body["messages"];
   Check(messages.Length>=2,"missing translation instruction or source");
   Dictionary<string,object> instruction=(Dictionary<string,object>)messages[0];
   Dictionary<string,object> source=(Dictionary<string,object>)messages[messages.Length-1];
   Check((string)instruction["role"]=="system"&&((string)instruction["content"]).Contains("French"),"target language missing from system instruction");
   Check((string)source["role"]=="user"&&(string)source["content"]==sample,"selected text changed during JSON encoding");
  }
  static async Task RunAsync(){
   string sample="Selected \"text\" & \uD55C\uAE00\nsecond line \\ end";
   foreach(string provider in new[]{"Ollama","OpenAI"}){
    string response=provider=="Ollama"?"{\"message\":{\"content\":\"Bonjour\\nmonde\"}}":"{\"choices\":[{\"message\":{\"content\":\"Bonjour\\nmonde\"}}]}";
    using(var server=new Server(200,response)){
     string result=await LocalTranslationClient.TranslateAsync(Settings(server.Address,provider),sample,CancellationToken.None);
     Check(result=="Bonjour\nmonde","translation response was not parsed");
     CheckTranslationRequest(await server.Received,provider=="Ollama"?"/api/chat":"/v1/chat/completions",sample);
    }
    string models=provider=="Ollama"?"{\"models\":[{\"name\":\"test-model\"},{\"name\":\"other-model\"}]}":"{\"data\":[{\"id\":\"test-model\"},{\"id\":\"other-model\"}]}";
    using(var server=new Server(200,models)){
     LocalTranslationSettings settings=Settings(server.Address,provider);settings.Model="";
     List<string> result=await LocalTranslationClient.ListModelsAsync(settings,CancellationToken.None);
     Check(result.Count==2&&result.Contains("test-model")&&result.Contains("other-model"),"installed models were not parsed");
     Request request=await server.Received;
     Check(request.Method=="GET"&&request.Path==(provider=="Ollama"?"/api/tags":"/v1/models"),"incorrect model-list route");
    }
   }
   foreach(string address in new[]{"https://example.com","http://127.0.0.1.example.com","http://192.168.1.20:11434","file:///tmp/model","http://user@localhost:11434","http://localhost:11434/path","http://localhost:11434/?q=x","http://localhost:11434/#fragment"}){
    bool failed=false;try{LocalTranslationClient.Validate(Settings(address,"Ollama"));}catch(ArgumentException){failed=true;}
    Check(failed,"nonlocal or ambiguous endpoint accepted: "+address);
   }
   LocalTranslationClient.Validate(Settings("http://localhost:11434/","Ollama"));
   LocalTranslationClient.Validate(Settings("http://[::1]:11434","Ollama"));
   LocalTranslationSettings original=Settings("http://127.0.0.1:11434","Ollama");
   foreach(string name in new[]{"test-model:cloud","test-model-cloud","test-model:CLOUD"," test-model-cloud "}){
    var remoteModel=original.Clone();remoteModel.Model=name;
    bool rejected=false;try{LocalTranslationClient.Validate(remoteModel);}catch(ArgumentException){rejected=true;}
    Check(rejected,"Ollama cloud model accepted: "+name);
   }
   var localModel=original.Clone();localModel.Model="custom-local-model:latest";LocalTranslationClient.Validate(localModel);
   LocalTranslationSettings copy=original.Clone();copy.Model="changed";
   Check(original.Model=="test-model"&&copy!=original,"settings edit must be isolated until saved");
   await MustRejectArgument(async delegate{var s=original.Clone();s.Model="";await LocalTranslationClient.TranslateAsync(s,"hello",CancellationToken.None);},"empty model accepted");
   await MustRejectArgument(async delegate{await LocalTranslationClient.TranslateAsync(original,"  ",CancellationToken.None);},"empty selection accepted");
   await MustRejectArgument(async delegate{await LocalTranslationClient.TranslateAsync(original,new string('x',2001),CancellationToken.None);},"oversize selection accepted");
   using(var server=new Server(404,"{\"error\":\"model not found\"}"))
    await MustFail(async delegate{await LocalTranslationClient.TranslateAsync(Settings(server.Address,"Ollama"),sample,CancellationToken.None);},"HTTP failure reported as success");
   foreach(string response in new[]{"not JSON","{}","{\"message\":{\"content\":\"\"}}","{\"message\":{\"content\":\"  \"}}"})
    using(var server=new Server(200,response))
     await MustFail(async delegate{await LocalTranslationClient.TranslateAsync(Settings(server.Address,"Ollama"),sample,CancellationToken.None);},"malformed or empty result accepted");
   using(var target=new Server(200,"{\"message\":{\"content\":\"unexpected\"}}"))
   using(var redirect=new Server(302,"",target.Address+"/api/chat")){
    await MustFail(async delegate{await LocalTranslationClient.TranslateAsync(Settings(redirect.Address,"Ollama"),sample,CancellationToken.None);},"redirect followed or accepted");
    Check(!target.Received.IsCompleted,"selection was sent to a redirected endpoint");
   }
   using(var server=new Server(200,"{\"message\":{\"content\":\""+new string('x',1024*1024+100)+"\"}}"))
    await MustFail(async delegate{await LocalTranslationClient.TranslateAsync(Settings(server.Address,"Ollama"),sample,CancellationToken.None);},"unbounded response accepted");
   using(var server=new Server(200,"",null,true))using(var cancellation=new CancellationTokenSource()){
    Task<string> pending=LocalTranslationClient.TranslateAsync(Settings(server.Address,"Ollama"),sample,cancellation.Token);
    Check(await Task.WhenAny(server.Received,Task.Delay(5000))==server.Received,"translation did not reach loopback server");
    cancellation.Cancel();Stopwatch timer=Stopwatch.StartNew();
    Check(await Task.WhenAny(pending,Task.Delay(3000))==pending,"cancellation did not abort the HTTP request");
    bool cancelled=false;try{await pending;}catch(OperationCanceledException){cancelled=true;}
    Check(cancelled&&timer.ElapsedMilliseconds<3000,"request cancellation was not propagated");
    Check(await Task.WhenAny(server.Completion,Task.Delay(3000))==server.Completion,"cancellation left the server connection open");
   }
   using(var cancellation=new CancellationTokenSource()){
    cancellation.Cancel();bool cancelled=false;
    try{await LocalTranslationClient.TranslateAsync(original,sample,cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
    Check(cancelled,"already cancelled request was started");
   }
  }
  sealed class Request {public string Method,Path,Headers,Body;}
  sealed class Server:IDisposable {
   readonly TcpListener listener;readonly Task worker;readonly TaskCompletionSource<Request> received=new TaskCompletionSource<Request>();
   TcpClient client;
   public readonly string Address;
   public Task<Request> Received {get{return received.Task;}}
   public Task Completion {get{return worker;}}
   public Server(int status,string body,string redirect=null,bool hold=false){
    listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
    Address="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;
    worker=Task.Run(delegate{Serve(status,body,redirect,hold);});
   }
   void Serve(int status,string body,string redirect,bool hold){
    try{
     client=listener.AcceptTcpClient();client.ReceiveTimeout=5000;client.SendTimeout=5000;
     using(NetworkStream stream=client.GetStream()){
      var header=new MemoryStream();int matched=0;
      while(matched<4){int value=stream.ReadByte();if(value<0)throw new EndOfStreamException();header.WriteByte((byte)value);if(header.Length>16384)throw new IOException("Stub header limit exceeded");matched=value==new byte[]{13,10,13,10}[matched]?matched+1:(value==13?1:0);}
      string text=Encoding.ASCII.GetString(header.ToArray());int length=0;
      foreach(string line in text.Split(new[]{"\r\n"},StringSplitOptions.None))if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))length=Int32.Parse(line.Substring(15).Trim());
      if(text.IndexOf("Expect: 100-continue",StringComparison.OrdinalIgnoreCase)>=0){byte[] interim=Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");stream.Write(interim,0,interim.Length);stream.Flush();}
      if(length>65536)throw new IOException("Stub request body limit exceeded");
      byte[] content=new byte[length];int offset=0;
      while(offset<length){int count=stream.Read(content,offset,length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}
      string[] first=text.Substring(0,text.IndexOf("\r\n",StringComparison.Ordinal)).Split(' ');
      received.TrySetResult(new Request{Method=first[0],Path=first[1],Headers=text,Body=Encoding.UTF8.GetString(content)});
      if(hold){while(stream.ReadByte()!=-1){}return;}
      byte[] payload=Encoding.UTF8.GetBytes(body);
      string response="HTTP/1.1 "+status+" Test\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: "+payload.Length+"\r\nConnection: close\r\n"+(redirect==null?"":"Location: "+redirect+"\r\n")+"\r\n";
      byte[] responseHeaders=Encoding.ASCII.GetBytes(response);stream.Write(responseHeaders,0,responseHeaders.Length);stream.Write(payload,0,payload.Length);stream.Flush();
     }
    }catch(Exception ex){received.TrySetException(ex);}
   }
   public void Dispose(){
    listener.Stop();if(client!=null)client.Close();
    // A failed client must never leave a blocked accept/read behind in self-test.
    try{worker.Wait(1000);}catch(AggregateException){}
   }
  }
 }
}
