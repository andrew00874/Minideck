using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Windows.Automation;

namespace MiniDeck {
 enum DialMode { Apps, Windows, Text }

 public class TextEngine {
  public string Id {get;set;}
  public string Name {get;set;}
  public string Url {get;set;}
  public string Glyph {get;set;}
  public override string ToString(){return Name;}
 }
 static class TextActions {
  public const int MaxTextLength=2000;
  public static List<TextEngine> Defaults(){return new List<TextEngine>{
   new TextEngine{Id="google",Name=L10n.T("Google 검색"),Glyph="G",Url="https://www.google.com/search?q={text}"},
   new TextEngine{Id="naver",Name=L10n.T("네이버 검색"),Glyph="N",Url="https://search.naver.com/search.naver?query={text}"},
   new TextEngine{Id="bing",Name=L10n.T("Bing 검색"),Glyph="b",Url="https://www.bing.com/search?q={text}"},
   new TextEngine{Id="translate",Name=L10n.T("Google 번역"),Glyph="A",Url=L10n.English?"https://translate.google.com/?sl=auto&tl=en&text={text}&op=translate":"https://translate.google.com/?sl=auto&tl=ko&text={text}&op=translate"}
  };}
  public static string BuildUrl(TextEngine engine,string text){
   if(String.IsNullOrWhiteSpace(text))throw new ArgumentException(L10n.T("먼저 글을 선택해 주세요."));
   if(text.Length>MaxTextLength)throw new ArgumentException(L10n.T("선택한 글이 너무 깁니다. 2,000자 이내로 선택해 주세요."));
   Uri parsed;
   string template=engine.Url??"";
   var authority=System.Text.RegularExpressions.Regex.Match(template,@"^https?://[^/?#]*",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
   if(!authority.Success||authority.Value.Contains("{text}")||!template.Contains("{text}")||!Uri.TryCreate(template.Replace("{text}","minideck"),UriKind.Absolute,out parsed)||
      (parsed.Scheme!="https"&&parsed.Scheme!="http")||!String.IsNullOrEmpty(parsed.UserInfo))
    throw new ArgumentException(L10n.T("http 또는 https 주소의 경로·쿼리에 {text}를 넣어 주세요."));
   string result=template.Replace("{text}",Uri.EscapeDataString(text));
   if(result.Length>15000)throw new ArgumentException(L10n.T("검색 주소가 너무 깁니다. 더 짧은 글을 선택해 주세요."));
   return result;
  }
  public static void SelfTest(){
   var engine=Defaults()[0];string sample="\uD55C\uAE00 & caf\u00E9? # /\nline";
   string url=BuildUrl(engine,sample);
   if(!url.EndsWith(Uri.EscapeDataString(sample))||url.Contains("\n"))throw new Exception("Query encoding failed");
   foreach(string invalid in new[]{"file:///x?{text}","https://{text}.example.com/","https://example.com/"}){
    bool rejected=false;try{BuildUrl(new TextEngine{Url=invalid},"x");}catch(ArgumentException){rejected=true;}
    if(!rejected)throw new Exception("Unsafe template accepted");
   }
   bool tooLong=false;try{BuildUrl(engine,new string('x',MaxTextLength+1));}catch(ArgumentException){tooLong=true;}
   if(!tooLong)throw new Exception("Text length limit missing");
  }
 }

 sealed class WindowChoice {
  public IntPtr Handle;public uint ProcessId;public string Title,Path;
  public AppEntry Entry(){return new AppEntry{Name=Title,Target=Path??"",Invoke=delegate{WindowSwitcher.Activate(this);}};}
 }
 static class WindowSwitcher {
  delegate bool EnumProc(IntPtr handle,IntPtr param);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,IntPtr param);
  [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern bool IsWindow(IntPtr handle);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr handle);
  [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr handle,uint command);
  [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr handle,int index);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr handle,StringBuilder text,int count);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr handle,out uint processId);
  [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr handle,int attribute,out int value,int size);
  public static List<WindowChoice> List(){
   var windows=new List<WindowChoice>();uint own=(uint)Process.GetCurrentProcess().Id;
   EnumWindows(delegate(IntPtr handle,IntPtr unused){
    uint pid;GetWindowThreadProcessId(handle,out pid);
    if(pid==own||!IsWindowVisible(handle))return true;
    int style=GetWindowLong(handle,-20),cloaked=0;
    if((style&0x80)!=0||(style&0x08000000)!=0||(GetWindow(handle,4)!=IntPtr.Zero&&(style&0x40000)==0))return true;
    try{if(DwmGetWindowAttribute(handle,14,out cloaked,4)==0&&cloaked!=0)return true;}catch(DllNotFoundException){}
    var title=new StringBuilder(4096);GetWindowText(handle,title,title.Capacity);if(String.IsNullOrWhiteSpace(title.ToString()))return true;
    string path="";try{using(Process p=Process.GetProcessById((int)pid))path=p.MainModule.FileName;}catch{}
    windows.Add(new WindowChoice{Handle=handle,ProcessId=pid,Title=title.ToString(),Path=path});return true;
   },IntPtr.Zero);
   return windows;
  }
  public static bool IsCurrent(WindowChoice item){uint pid;return IsWindow(item.Handle)&&GetWindowThreadProcessId(item.Handle,out pid)!=0&&pid==item.ProcessId;}
  public static void Activate(WindowChoice item){
   if(!IsCurrent(item))throw new InvalidOperationException(L10n.T("이 창은 닫혔습니다. 2번 키로 목록을 새로 여세요."));
   if(Native.IsIconic(item.Handle))Native.ShowWindowAsync(item.Handle,9);
   if(!Native.SetForegroundWindow(item.Handle))throw new InvalidOperationException(L10n.T("창 전환이 허용되지 않았습니다. 대상 앱의 권한을 확인해 주세요."));
  }
 }

 // Only a newly copied selection is accepted. Never consume stale clipboard text.
 // Run on the UI STA; await yields while the user's hotkey modifiers are released.
 static class SelectedTextCapture {
  [DllImport("user32.dll")]static extern uint GetClipboardSequenceNumber();
  [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")]static extern uint SendInput(uint count,INPUT[] inputs,int size);
  [StructLayout(LayoutKind.Sequential)]struct INPUT {public uint type;public INPUTUNION data;}
  [StructLayout(LayoutKind.Explicit)]struct INPUTUNION {
   [FieldOffset(0)]public KEYBDINPUT keyboard;
   [FieldOffset(0)]public MOUSEINPUT mouse;
  }
  [StructLayout(LayoutKind.Sequential)]struct KEYBDINPUT{public ushort key,scan;public uint flags,time;public UIntPtr extra;}
  [StructLayout(LayoutKind.Sequential)]struct MOUSEINPUT{public int x,y;public uint data,flags,time;public UIntPtr extra;}
  static INPUT Key(ushort key,bool up){return new INPUT{type=1,data=new INPUTUNION{keyboard=new KEYBDINPUT{key=key,flags=up?2u:0u}}};}
  static bool ModifiersDown(){return new[]{16,17,18,91,92,114}.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0);}
  static int automationBusy;
  static string[] AutomationSelection(){
   try{
    return ReadElement(AutomationElement.FocusedElement);
   }catch{return null;}
  }
  internal static string[] ReadElement(AutomationElement element){
    if(element==null)return null;
    if(element.Current.IsPassword)return new[]{""};
    object pattern;
    if(!element.TryGetCurrentPattern(TextPattern.Pattern,out pattern))return null;
    return ((TextPattern)pattern).GetSelection().Select(r=>r.GetText(TextActions.MaxTextLength+1)).ToArray();
  }
  public static async Task<string> Read(IntPtr source,Func<bool> cancelled){
   for(int i=0;i<60&&ModifiersDown();i++)await Task.Delay(20);
   if(cancelled())return null;
   if(ModifiersDown()||WindowSwitcher.GetForegroundWindow()!=source)throw new InvalidOperationException(L10n.T("입력 창이 바뀌었습니다. 글을 다시 선택하고 3번 키를 눌러 주세요."));
   if(System.Threading.Interlocked.CompareExchange(ref automationBusy,1,0)==0){
    Task<string[]> selectionTask=Task.Factory.StartNew<string[]>(delegate{try{return AutomationSelection();}finally{System.Threading.Interlocked.Exchange(ref automationBusy,0);}});
    if(await Task.WhenAny(selectionTask,Task.Delay(600))==selectionTask){
     string[] selection=await selectionTask;
     if(cancelled()||WindowSwitcher.GetForegroundWindow()!=source)return null;
     if(selection!=null){
      string selected=String.Join("\n",selection);
      if(String.IsNullOrWhiteSpace(selected))throw new InvalidOperationException(L10n.T("먼저 글을 선택해 주세요."));
      if(selected.Length>TextActions.MaxTextLength)throw new InvalidOperationException(L10n.T("선택한 글이 너무 깁니다. 2,000자 이내로 선택해 주세요."));
      return selected;
     }
    }
   }
   uint before=GetClipboardSequenceNumber(),copied=before;
   IDataObject previous=Clipboard.GetDataObject();
   // Materialize all formats before another application takes clipboard ownership.
   DataObject backup=new DataObject();
   if(previous!=null)foreach(string format in previous.GetFormats(false)){
    object value=previous.GetData(format,false);
    if(value==null)throw new InvalidOperationException(L10n.T("클립보드를 보존하지 못했습니다. 잠시 후 다시 시도해 주세요."));
    MemoryStream stream=value as MemoryStream;if(stream!=null)value=new MemoryStream(stream.ToArray());
    backup.SetData(format,false,value);
   }
   if(cancelled())return null;
   if(GetClipboardSequenceNumber()!=before||WindowSwitcher.GetForegroundWindow()!=source)throw new InvalidOperationException(L10n.T("입력 창이 바뀌었습니다. 글을 다시 선택하고 3번 키를 눌러 주세요."));
   try{
    INPUT[] input={Key(17,false),Key(67,false),Key(67,true),Key(17,true)};
    if(SendInput(4,input,Marshal.SizeOf(typeof(INPUT)))!=4){SendInput(2,new[]{Key(67,true),Key(17,true)},Marshal.SizeOf(typeof(INPUT)));throw new InvalidOperationException(L10n.T("선택한 글을 복사하지 못했습니다. 대상 앱의 권한을 확인해 주세요."));}
    for(int i=0;i<40;i++){
     await Task.Delay(25);copied=GetClipboardSequenceNumber();
     if(copied!=before){
      string text=Clipboard.ContainsText(TextDataFormat.UnicodeText)?Clipboard.GetText(TextDataFormat.UnicodeText):null;
      if(cancelled()||WindowSwitcher.GetForegroundWindow()!=source)return null;
      if(String.IsNullOrWhiteSpace(text))break;
      if(text.Length>TextActions.MaxTextLength)throw new InvalidOperationException(L10n.T("선택한 글이 너무 깁니다. 2,000자 이내로 선택해 주세요."));
      return text;
     }
     if(cancelled()||WindowSwitcher.GetForegroundWindow()!=source)return null;
    }
    throw new InvalidOperationException(L10n.T("선택한 글을 가져오지 못했습니다. 복사 가능한 글을 선택한 뒤 3번 키를 눌러 주세요."));
   }finally{
    if(copied!=before&&GetClipboardSequenceNumber()==copied){
     if(previous==null)Clipboard.Clear();else Clipboard.SetDataObject(backup,true,3,30);
    }
   }
  }
 }

 class EnginesForm:Form {
  public List<TextEngine> Result;
  ListBox list=new ListBox();TextBox name=new TextBox(),url=new TextBox();Label error=new Label();
  List<TextEngine> engines;
  public EnginesForm(List<TextEngine> current){
   Text=L10n.T("검색·번역 엔진");Icon=Brand.Icon;Font=Typography.Create(10);ClientSize=new Size(650,500);StartPosition=FormStartPosition.CenterParent;
   FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
   engines=current.Select(e=>new TextEngine{Id=e.Id,Name=e.Name,Url=e.Url,Glyph=e.Glyph}).ToList();
   Controls.Add(new Label{Text=L10n.T("노브로 고를 검색·번역 서비스를 등록하세요."),Left=24,Top=24,Width=590,Height=28});
   list.SetBounds(24,68,200,276);list.DisplayMember="Name";list.AccessibleName=L10n.T("검색·번역 엔진");Controls.Add(list);
   list.DrawMode=DrawMode.OwnerDrawFixed;list.ItemHeight=42;list.BorderStyle=BorderStyle.None;
   list.DrawItem+=delegate(object sender,DrawItemEventArgs e){
    if(e.Index<0)return;var engine=(TextEngine)list.Items[e.Index];
    using(Brush b=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(43,56,80):Theme.Surface))e.Graphics.FillRectangle(b,e.Bounds);
    e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
    e.Graphics.DrawImage(EngineIcons.Get(EngineIcons.For(engine)),new Rectangle(e.Bounds.X+10,e.Bounds.Y+8,26,26));
    TextRenderer.DrawText(e.Graphics,engine.Name,list.Font,new Rectangle(e.Bounds.X+44,e.Bounds.Y,e.Bounds.Width-46,e.Bounds.Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
    if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
   };
   Controls.Add(new Label{Text=L10n.T("표시 이름"),Left=246,Top=68,Width=370});name.SetBounds(246,98,378,30);Controls.Add(name);
   Controls.Add(new Label{Text=L10n.T("결과 주소 · {text} = 선택한 글"),Left=246,Top=152,Width=380});url.SetBounds(246,184,378,104);url.Multiline=true;url.ScrollBars=ScrollBars.Vertical;Controls.Add(url);
   Controls.Add(new Label{Text=L10n.T("검색어 위치에 {text}를 넣으세요.\n번역 언어는 주소의 tl=ko 또는 tl=en으로 지정합니다."),Left=246,Top=304,Width=380,Height=58});
   name.AccessibleName=L10n.T("표시 이름");url.AccessibleName=L10n.T("결과 주소 · {text} = 선택한 글");
   Button add=new Button{Text=L10n.T("추가"),Left=24,Top=360,Width=94,Height=36};Button remove=new Button{Text=L10n.T("삭제"),Left=130,Top=360,Width=94,Height=36};
   Button apply=new Button{Text=L10n.T("항목 적용"),Left=246,Top=370,Width=160,Height=36};
   error.SetBounds(24,412,600,28);
   Button save=new Button{Text=L10n.T("저장"),Left=424,Top=452,Width=94,Height=36};Button cancel=new Button{Text=L10n.T("취소"),Left=530,Top=452,Width=94,Height=36,DialogResult=DialogResult.Cancel};
   Controls.AddRange(new Control[]{add,remove,apply,error,save,cancel});CancelButton=cancel;
   list.SelectedIndexChanged+=delegate{if(list.SelectedIndex<0)return;var e=engines[list.SelectedIndex];name.Text=e.Name;url.Text=e.Url;error.Text="";};
   add.Click+=delegate{if(!ApplyCurrent())return;engines.Add(new TextEngine{Id=Guid.NewGuid().ToString("N"),Name=L10n.T("새 엔진"),Glyph="S",Url="https://www.google.com/search?q={text}"});RefreshItems(engines.Count-1);};
   remove.Click+=delegate{int i=list.SelectedIndex;if(i<0)return;engines.RemoveAt(i);RefreshItems(Math.Max(0,i-1));};
   apply.Click+=delegate{if(ApplyCurrent())RefreshItems(list.SelectedIndex);};
   save.Click+=delegate{if(!ApplyCurrent())return;if(engines.Count==0){error.Text=L10n.T("엔진을 하나 이상 등록해 주세요.");return;}Result=engines;DialogResult=DialogResult.OK;};
   Theme.Apply(this);error.ForeColor=Color.FromArgb(255,166,166);RefreshItems(0);
  }
  bool ApplyCurrent(){int i=list.SelectedIndex;if(i<0)return true;try{
   if(String.IsNullOrWhiteSpace(name.Text))throw new Exception(L10n.T("표시 이름을 입력하세요."));
   var entry=new TextEngine{Id=engines[i].Id,Name=name.Text.Trim(),Url=url.Text.Trim(),Glyph=engines[i].Glyph};TextActions.BuildUrl(entry,"test");engines[i]=entry;error.Text="";return true;
  }catch(Exception ex){error.Text=ex.Message;return false;}}
  void RefreshItems(int index){list.Items.Clear();foreach(var e in engines)list.Items.Add(e);if(engines.Count>0)list.SelectedIndex=Math.Min(index,engines.Count-1);else{name.Clear();url.Clear();}}
 }
}
