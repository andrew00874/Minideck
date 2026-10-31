// Compiled separately by CI. This fixture never reads real models or user text.
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

static class RuntimeFixture {
 static string Value(string[] args,string name){for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];throw new ArgumentException(name);}
 static void Reply(NetworkStream stream,int status,string body,string type="application/json"){
  byte[] bytes=Encoding.UTF8.GetBytes(body);
  byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+" Test\r\nContent-Type: "+type+"\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");
  stream.Write(header,0,header.Length);stream.Write(bytes,0,bytes.Length);stream.Flush();
 }
 static void Main(string[] args){
  string root=Environment.CurrentDirectory;
  string key=File.ReadAllText(Value(args,"--api-key-file")).Trim();
  File.AppendAllText(Path.Combine(root,"starts.txt"),System.Diagnostics.Process.GetCurrentProcess().Id+"\n");
  if(File.ReadAllText(Path.Combine(root,"mode.txt")).Trim()=="load-fail")Environment.Exit(42);
  var listener=new TcpListener(IPAddress.Loopback,Int32.Parse(Value(args,"--port")));listener.Start();
  while(true)using(var client=listener.AcceptTcpClient())using(NetworkStream stream=client.GetStream())try{
   client.ReceiveTimeout=5000;client.SendTimeout=5000;
   var header=new MemoryStream();int match=0;
   while(match<4){int ch=stream.ReadByte();if(ch<0)throw new EndOfStreamException();header.WriteByte((byte)ch);if(header.Length>16384)throw new IOException();match=ch==new byte[]{13,10,13,10}[match]?match+1:ch==13?1:0;}
   string headers=Encoding.ASCII.GetString(header.ToArray());int length=0;
   foreach(string line in headers.Split(new[]{"\r\n"},StringSplitOptions.None))if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))length=Int32.Parse(line.Substring(15).Trim());
   if(headers.IndexOf("Expect: 100-continue",StringComparison.OrdinalIgnoreCase)>=0){byte[] interim=Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");stream.Write(interim,0,interim.Length);stream.Flush();}
   if(length>65536)throw new IOException();
   for(int i=0;i<length;i++)if(stream.ReadByte()<0)throw new EndOfStreamException();
   string path=headers.Split(' ')[1];
   if(!headers.Contains("Bearer "+key)){Reply(stream,401,"{}");continue;}
   if(path=="/v1/models"){Reply(stream,200,"{\"data\":[{\"id\":\"test-model\"}]}");continue;}
   if(path=="/test/exit"){Reply(stream,200,"{}");Environment.Exit(42);}
   if(path!="/v1/chat/completions"){Reply(stream,404,"{}");continue;}
   File.AppendAllText(Path.Combine(root,"requests.txt"),"request\n");
   string mode=File.ReadAllText(Path.Combine(root,"mode.txt")).Trim();
   if(mode=="missing-model"){Reply(stream,404,"{}");continue;}
   if(mode=="cancel"){while(stream.ReadByte()!=-1){}continue;}
   if(mode=="crash-once"&&!File.Exists(Path.Combine(root,"crashed-once"))){
    File.WriteAllText(Path.Combine(root,"crashed-once"),"yes");
    Reply(stream,200,"data: {\"choices\":[{\"delta\":{\"content\":\"Partial\"},\"finish_reason\":null}]}\n\n","text/event-stream");
    Thread.Sleep(100);Environment.Exit(42);
   }
   if(mode=="drop-always"){Reply(stream,200,"data: {\"choices\":[{\"delta\":{\"content\":\"Partial\"},\"finish_reason\":null}]}\n\n","text/event-stream");continue;}
   Reply(stream,200,"data: {\"choices\":[{\"delta\":{\"content\":\"Bonjour\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n","text/event-stream");
  }catch(IOException){}catch(SocketException){}
 }
}
