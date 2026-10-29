using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace MiniDeck {
 static class AppDiagnostics {
  static readonly object sync=new object();
  internal static Action<string> TestSink;
  static string Folder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiniDeck","diagnostics");}}
  // Exception messages and process output can contain selected text. Only log
  // structural exception/runtime metadata, never messages, prompts or tokens.
  public static void Exception(string component,Exception error){
   Write(new Dictionary<string,object>{{"component",component},{"type",error.GetType().FullName},{"hresult",error.HResult},{"stack",error.StackTrace??""}});
  }
  public static void Runtime(string state,int pid,int? code=null){Write(new Dictionary<string,object>{{"component","model"},{"state",state},{"pid",pid},{"exitCode",code}});}
  public static void App(string state){Write(new Dictionary<string,object>{{"component","app"},{"state",state},{"pid",System.Diagnostics.Process.GetCurrentProcess().Id},{"module",typeof(AppDiagnostics).Assembly.ManifestModule.ModuleVersionId.ToString()}});}
  public static string ClassifyRuntimeError(string line){
   if(String.IsNullOrEmpty(line))return null;string text=line.ToLowerInvariant();
   if(text.Contains("out of memory")||text.Contains("out_of_device_memory")||text.Contains("out of device memory"))return "gpu-memory";
   if(text.Contains("device_lost")||text.Contains("device lost"))return "gpu-device-lost";
   if(text.Contains("address already in use")||text.Contains("failed to bind"))return "port-busy";
   if(text.Contains("failed to load model")||text.Contains("error loading model"))return "model-load";
   if(text.Contains("error")||text.Contains("failed"))return "runtime-error";
   return null;
  }
  static void Write(Dictionary<string,object> entry){
   try{lock(sync){
    entry["utc"]=DateTime.UtcNow.ToString("o");string json=new JavaScriptSerializer().Serialize(entry)+Environment.NewLine;
    if(TestSink!=null){TestSink(json);return;}
    Directory.CreateDirectory(Folder);string file=Path.Combine(Folder,"events.jsonl");
    if(File.Exists(file)&&new FileInfo(file).Length>131072)File.WriteAllText(file,"",new UTF8Encoding(false));
    File.AppendAllText(file,json,new UTF8Encoding(false));
   }}catch(Exception){/* Diagnostics must never terminate the application. */}
  }
 }
}
