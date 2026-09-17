using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MiniDeck {
 static class BrowserLink {
  static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  public static string PipeName { get { return "MiniDeck.Browser." + WindowsIdentity.GetCurrent().User.Value; } }
  public static bool Open(string target) {
   Uri url;
   if(!Uri.TryCreate(target,UriKind.Absolute,out url) || (url.Scheme!="https" && url.Scheme!="http")) return false;
   // All requests are serialized by the extension. Repeated presses while a request
   // is pending must not create another tab, including after a response timeout.
   if(!Pending.Add(target)) return true;
   Control owner=Application.OpenForms.Count>0?Application.OpenForms[0]:null;
   Task.Factory.StartNew(delegate {
    string error=null;
    try {
     using(var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut,PipeOptions.Asynchronous)) {
      try {pipe.Connect(1000);} catch(TimeoutException) {throw new Exception(L10n.T("브라우저 연동이 연결되지 않았습니다. Chrome에서 MiniDeck 확장을 켜 주세요.\n\n최초 설치: MiniDeck\\browser-extension 폴더를 chrome://extensions 에서 ‘압축해제된 확장 프로그램을 로드합니다’로 불러오세요.\n여러 프로필을 쓰면 사이트를 사용하는 프로필 한 곳에 설치하세요."));}
      var serializer=new JavaScriptSerializer();
      var writer=new StreamWriter(pipe,new UTF8Encoding(false));writer.AutoFlush=true;
      var reader=new StreamReader(pipe,Encoding.UTF8);
      writer.WriteLine(serializer.Serialize(new {id=Guid.NewGuid().ToString("N"),url=target}));
      Task<string> response=reader.ReadLineAsync();
      if(!response.Wait(8000)) throw new Exception(L10n.T("브라우저 응답을 확인하지 못했습니다. 중복 탭을 막기 위해 다시 열지는 않았습니다. 브라우저 상태를 확인해 주세요."));
      string line=response.Result;
      if(line==null)throw new Exception(L10n.T("브라우저 연결이 종료되었습니다."));
      var result=serializer.Deserialize<Dictionary<string,object>>(line);
      if(!result.ContainsKey("ok") || !Convert.ToBoolean(result["ok"]))throw new Exception(result.ContainsKey("error")?Convert.ToString(result["error"]):L10n.T("브라우저 전환 실패"));
     }
    }catch(Exception ex){error=ex.Message;}
    if(owner!=null && !owner.IsDisposed)try {owner.BeginInvoke((Action)delegate {
     Pending.Remove(target);
     if(error!=null)MessageBox.Show(owner,error,L10n.T("MiniDeck · 브라우저 연결"),MessageBoxButtons.OK,MessageBoxIcon.Information);
    });}catch(InvalidOperationException){}
   });
   return true;
  }
 }
}
