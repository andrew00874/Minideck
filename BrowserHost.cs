using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using MiniDeck;

// Chrome Native Messaging host. No sockets, web server, browsing history log,
// page content access, or arbitrary command execution.
class BrowserHost {
 static readonly BlockingCollection<string> Responses=new BlockingCollection<string>();
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 static bool ReadExact(Stream input,byte[] data) {
  int offset=0;while(offset<data.Length){int n=input.Read(data,offset,data.Length-offset);if(n==0)return false;offset+=n;}return true;
 }
 static void Main() {
  Stream input=Console.OpenStandardInput(),output=Console.OpenStandardOutput();
  var inputThread=new Thread(delegate() {
   try {while(true){byte[] size=new byte[4];if(!ReadExact(input,size))break;int n=BitConverter.ToInt32(size,0);if(n<1||n>65536)break;byte[] bytes=new byte[n];if(!ReadExact(input,bytes))break;Responses.Add(Encoding.UTF8.GetString(bytes));}}catch{}
   Environment.Exit(0);
  });inputThread.IsBackground=true;inputThread.Start();
  SecurityIdentifier user=WindowsIdentity.GetCurrent().User;
  var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new PipeAccessRule(user,PipeAccessRights.FullControl,AccessControlType.Allow));
  while(true) {
   try {
    using(var pipe=new NamedPipeServerStream("MiniDeck.Browser."+user.Value,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,8192,8192,security)) {
     pipe.WaitForConnection();
     var reader=new StreamReader(pipe,Encoding.UTF8);var writer=new StreamWriter(pipe,new UTF8Encoding(false));writer.AutoFlush=true;
     var read=reader.ReadLineAsync();if(!read.Wait(5000))continue;
     string request=read.Result;if(request==null || request.Length>16000)continue;
     var value=Json.Deserialize<Dictionary<string,object>>(request);Uri uri;
     if(!value.ContainsKey("id") || !value.ContainsKey("url") || !Uri.TryCreate(Convert.ToString(value["url"]),UriKind.Absolute,out uri) || (uri.Scheme!="https"&&uri.Scheme!="http"))continue;
     byte[] bytes=Encoding.UTF8.GetBytes(request);byte[] length=BitConverter.GetBytes(bytes.Length);output.Write(length,0,4);output.Write(bytes,0,bytes.Length);output.Flush();
     DateTime deadline=DateTime.UtcNow.AddSeconds(7);bool received=false;
     while(DateTime.UtcNow<deadline) {
      string reply;if(!Responses.TryTake(out reply,Math.Max(1,(int)(deadline-DateTime.UtcNow).TotalMilliseconds)))break;
      var response=Json.Deserialize<Dictionary<string,object>>(reply);
      if(response.ContainsKey("id") && Convert.ToString(response["id"])==Convert.ToString(value["id"])) {writer.WriteLine(reply);received=true;break;}
     }
     if(!received)writer.WriteLine(Json.Serialize(new {ok=false,error=L10n.T("브라우저 응답 시간 초과. 중복 실행은 하지 않았습니다.")}));
    }
   }catch(IOException){Thread.Sleep(250);}catch{Thread.Sleep(250);}
  }
 }
}
