using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
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
      // The extension can select a tab while Windows still denies activation.
      // Hand foreground access to the Chrome process that owns this pipe before
      // requesting a tab switch. No extension reload or protocol change is needed.
      BrowserForeground.Prepare(pipe);
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
 static class BrowserForeground {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
  struct ProcessEntry {
   public uint Size,Usage,Id;public UIntPtr Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;
  }
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint id);
  [DllImport("kernel32.dll",SetLastError=true)]static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateToolhelp32Snapshot(uint flags,uint id);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32FirstW(Microsoft.Win32.SafeHandles.SafeFileHandle snapshot,ref ProcessEntry entry);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32NextW(Microsoft.Win32.SafeHandles.SafeFileHandle snapshot,ref ProcessEntry entry);
  [DllImport("user32.dll",SetLastError=true)]static extern bool AllowSetForegroundWindow(uint id);

  internal static uint FindChrome(uint host,Dictionary<uint,uint> parents,Dictionary<uint,string> names) {
   string name;
   if(!names.TryGetValue(host,out name)||!String.Equals(name,"MiniDeck.BrowserHost.exe",StringComparison.OrdinalIgnoreCase))return 0;
   var seen=new HashSet<uint>();
   for(int depth=0;depth<8&&seen.Add(host);depth++) {
    uint parent;if(!parents.TryGetValue(host,out parent)||!names.TryGetValue(parent,out name))return 0;
    if(String.Equals(name,"chrome.exe",StringComparison.OrdinalIgnoreCase))return parent;
    // Chrome on Windows starts native hosts through cmd.exe. Do not walk
    // past unrelated launchers and accidentally activate another application.
    if(!String.Equals(name,"cmd.exe",StringComparison.OrdinalIgnoreCase))return 0;
    host=parent;
   }
   return 0;
  }
  public static uint ConnectedChrome(System.IO.Pipes.NamedPipeClientStream pipe) {
   uint host;if(!GetNamedPipeServerProcessId(pipe.SafePipeHandle,out host))return 0;
   var parents=new Dictionary<uint,uint>();var names=new Dictionary<uint,string>();
   using(var snapshot=CreateToolhelp32Snapshot(2,0)) {
    if(snapshot.IsInvalid)return 0;
    var entry=new ProcessEntry{Size=(uint)Marshal.SizeOf(typeof(ProcessEntry))};
    if(!Process32FirstW(snapshot,ref entry))return 0;
    do{parents[entry.Id]=entry.Parent;names[entry.Id]=entry.Name;}while(Process32NextW(snapshot,ref entry));
   }
   return FindChrome(host,parents,names);
  }
  public static void Prepare(System.IO.Pipes.NamedPipeClientStream pipe) {
   try {
    uint id=ConnectedChrome(pipe);if(id==0)return;
    AllowSetForegroundWindow(id);
    using(Process browser=Process.GetProcessById((int)id)) {
     IntPtr window=browser.MainWindowHandle;
     if(window!=IntPtr.Zero) {
      if(Native.IsIconic(window))Native.ShowWindowAsync(window,9);
      Native.SetForegroundWindow(window);
     }
    }
   }catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}catch(ArgumentException){}
  }
  public static void SelfTest() {
   var parents=new Dictionary<uint,uint>{{10,20},{20,30}};
   var names=new Dictionary<uint,string>{{10,"MiniDeck.BrowserHost.exe"},{20,"cmd.exe"},{30,"chrome.exe"}};
   if(FindChrome(10,parents,names)!=30)throw new Exception("Chrome parent through cmd not resolved");
   parents[10]=30;if(FindChrome(10,parents,names)!=30)throw new Exception("Direct Chrome parent not resolved");
   parents[10]=20;names[20]="other.exe";if(FindChrome(10,parents,names)!=0)throw new Exception("Unrelated ancestor accepted");
   names[20]="cmd.exe";parents[20]=20;if(FindChrome(10,parents,names)!=0)throw new Exception("Cyclic ancestry accepted");
   names[10]="other.exe";if(FindChrome(10,parents,names)!=0)throw new Exception("Unexpected pipe server accepted");
  }
 }
}
