using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MiniDeck {
 static class TranslationStream {
  static InvalidOperationException Invalid(){return new InvalidOperationException(L10n.T("로컬 서버의 응답을 읽을 수 없습니다. 서버 종류와 주소를 확인하세요."));}
  static Dictionary<string,object> Json(string text){
   try{var root=new JavaScriptSerializer{MaxJsonLength=131072,RecursionLimit=32}.DeserializeObject(text) as Dictionary<string,object>;if(root==null||root.ContainsKey("error"))throw Invalid();return root;}
   catch(ArgumentException){throw Invalid();}
  }
  static string Text(Dictionary<string,object> root,string key){object value;return root!=null&&root.TryGetValue(key,out value)?value as string:null;}
  static Dictionary<string,object> Object(Dictionary<string,object> root,string key){object value;return root!=null&&root.TryGetValue(key,out value)?value as Dictionary<string,object>:null;}
  public static async Task<string> ReadAsync(Stream input,bool ollama,CancellationToken token,Action<string> update){
   var text=new StringBuilder();var line=new StringBuilder();var data=new StringBuilder();
   bool finished=false,stopped=false,reported=false;var clock=Stopwatch.StartNew();
   Action<string> append=part=>{
    if(String.IsNullOrEmpty(part))return;
    if(text.Length+part.Length>1024*1024)throw Invalid();text.Append(part);
    if(!reported||clock.ElapsedMilliseconds>=80){update(text.ToString());reported=true;clock.Restart();}
   };
   Action<string> payload=value=>{
    if(ollama){
     var root=Json(value);append(Text(Object(root,"message"),"content"));object done;
     if(root.TryGetValue("done",out done)&&done is bool&&(bool)done){
      string reason=Text(root,"done_reason");if(reason!=null&&reason!="stop")throw LocalTranslationClient.IncompleteTranslation();finished=true;
     }
    }else{
     if(value.Trim()=="[DONE]"){if(!stopped)throw LocalTranslationClient.IncompleteTranslation();finished=true;return;}
     var root=Json(value);object choices;
     var items=root.TryGetValue("choices",out choices)?choices as object[]:null;
     if(items==null||items.Length==0)return;
     var choice=items[0] as Dictionary<string,object>;append(Text(Object(choice,"delta"),"content"));
     string reason=Text(choice,"finish_reason");
     if(reason!=null){if(reason!="stop")throw LocalTranslationClient.IncompleteTranslation();stopped=true;}
    }
   };
   Action<string> consume=value=>{
    value=value.TrimEnd('\r').TrimStart('\uFEFF');
    if(ollama){if(!String.IsNullOrWhiteSpace(value))payload(value);return;}
    if(value.Length==0){if(data.Length>0){payload(data.ToString());data.Clear();}return;}
    if(value.StartsWith("data:",StringComparison.Ordinal)){if(data.Length>0)data.Append('\n');data.Append(value.Substring(5).TrimStart(' '));if(data.Length>131072)throw Invalid();}
   };
   byte[] bytes=new byte[4096];char[] characters=new char[8192];var decoder=new UTF8Encoding(false,true).GetDecoder();int wire=0,count;
   try{
    while(!finished&&(count=await input.ReadAsync(bytes,0,bytes.Length,token).ConfigureAwait(false))>0){
     wire+=count;if(wire>8*1024*1024)throw Invalid();
     int decoded=decoder.GetChars(bytes,0,count,characters,0,false);
     for(int i=0;i<decoded&&!finished;i++){
      if(characters[i]=='\n'){consume(line.ToString());line.Clear();}
      else{if(line.Length>=131072)throw Invalid();line.Append(characters[i]);}
     }
    }
    if(!finished){decoder.GetChars(new byte[0],0,0,characters,0,true);if(line.Length>0)consume(line.ToString());if(!ollama&&data.Length>0)payload(data.ToString());}
   }catch(DecoderFallbackException){throw Invalid();}
   token.ThrowIfCancellationRequested();if(!finished)throw LocalTranslationClient.IncompleteTranslation();
   update(text.ToString());
   object result=ollama?(object)new Dictionary<string,object>{{"message",new Dictionary<string,object>{{"content",text.ToString()}}},{"done",true},{"done_reason","stop"}}:
    new Dictionary<string,object>{{"choices",new[]{new Dictionary<string,object>{{"message",new Dictionary<string,object>{{"content",text.ToString()}}},{"finish_reason","stop"}}}}};
   return new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.Serialize(result);
  }
 }
}
