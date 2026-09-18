using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MiniDeck {
 public class AppEntry {
  public string Name { get; set; }
  public string Target { get; set; }
  public string Arguments { get; set; }
  public string IconPath { get; set; }
  public override string ToString() { return Name; }
 }
 public class Settings {
  public List<AppEntry> Apps { get; set; }
  public int[] Keys { get; set; }
  public int? OverlayX { get; set; }
  public int? OverlayY { get; set; }
  public int DialSize {get;set;}
  public Settings() { Apps = new List<AppEntry>(); Keys = new int[] {0,1,2}; }
 }
 static class Storage {
  public static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniDeck");
  public static string FileName = Path.Combine(Folder, "settings.json");
  public static Settings Load() {
   if (!File.Exists(FileName)) return Defaults();
   try {
    Settings s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FileName));
    if (s == null || s.Apps == null || s.Keys == null || s.Keys.Length != 3 || s.Apps.Any(a => a == null || String.IsNullOrWhiteSpace(a.Target))) throw new Exception(L10n.T("설정 형식이 올바르지 않습니다."));
    for (int i=0;i<3;i++) if(s.Keys[i] < -1 || s.Keys[i]>=s.Apps.Count) s.Keys[i]=-1;
    return s;
   } catch(Exception ex) {
    MessageBox.Show(L10n.T("설정을 읽지 못했습니다. 기존 파일은 유지합니다.\n") + FileName + "\n" + ex.Message, "MiniDeck");
    throw;
   }
  }
  public static Settings Defaults() {
   Settings s = new Settings();
   s.Apps.Add(new AppEntry { Name="Codex", Target="explorer.exe", Arguments=@"shell:AppsFolder\OpenAI.Codex_2p2nqsd0c76g0!App" });
   s.Apps.Add(new AppEntry { Name=L10n.T("탐색기"), Target="explorer.exe", Arguments="" });
   s.Apps.Add(new AppEntry { Name=L10n.T("메모장"), Target="notepad.exe", Arguments="" });
   s.Apps.Add(new AppEntry { Name=L10n.T("계산기"), Target="calc.exe", Arguments="" });
   return s;
  }
  public static void Save(Settings s) {
   Directory.CreateDirectory(Folder);
   string temp = FileName + ".tmp";
   File.WriteAllText(temp, new JavaScriptSerializer().Serialize(s), Encoding.UTF8);
   if(File.Exists(FileName)) File.Replace(temp,FileName,FileName+".bak"); else File.Move(temp,FileName);
  }
 }
 static class Native {
  [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int cmd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 }
 static class Launcher {
  public static void Run(AppEntry a) {
   try {
    string target = Environment.ExpandEnvironmentVariables(a.Target.Trim());
    if(BrowserLink.Open(target)) return;
    if (Path.IsPathRooted(target) && String.Equals(Path.GetExtension(target),".exe",StringComparison.OrdinalIgnoreCase) && String.IsNullOrWhiteSpace(a.Arguments)) {
     foreach(Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target))) {
      using(p) { try {
       if(p.MainWindowHandle != IntPtr.Zero && String.Equals(p.MainModule.FileName,target,StringComparison.OrdinalIgnoreCase)) {
        if(Native.IsIconic(p.MainWindowHandle)) Native.ShowWindowAsync(p.MainWindowHandle,9);
        if(Native.SetForegroundWindow(p.MainWindowHandle)) return;
       }
      } catch {} }
     }
    }
    ProcessStartInfo info = new ProcessStartInfo(target, a.Arguments ?? "");
    info.UseShellExecute = true;
    if(File.Exists(target)) info.WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(target));
    Process.Start(info);
   } catch(Exception ex) { MessageBox.Show(a.Name+L10n.T(" 실행 실패\n")+ex.Message,"MiniDeck",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
  }
 }
 class Overlay : Form {
  public Settings Data;
  public int Selected;
  public Action ClosedOverlay;
  public Action PositionChanged;
  int Diameter {get{return Math.Max(280,Math.Min(560,Data.DialSize==0?360:Data.DialSize));}}
  static readonly Color DialBackground=Color.FromArgb(247,247,242), DialText=Color.FromArgb(33,47,48), DialMuted=Color.FromArgb(86,104,103);
  const float DesignSize=540f;
  bool pointerDown,dragging;
  Point pointerStart,windowStart;
  Panel caption=new Panel();Label fullName=new Label();
  int captionScroll;bool presenting;
  Timer expiry = new Timer();
  Timer animation = new Timer();
  Dictionary<int,float> emphasis = new Dictionary<int,float>();
  Dictionary<string,Icon> icons = new Dictionary<string,Icon>();
  public Overlay(Settings s) {
   Data=s; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
   BackColor=DialBackground; ForeColor=DialText; DoubleBuffered=true;
   Font=Typography.Create(10); AutoScaleMode=AutoScaleMode.None; StartPosition=FormStartPosition.Manual; ClientSize=new Size(Diameter,Diameter);
   caption.BackColor=DialBackground;caption.AutoScroll=true;caption.SetBounds(0,Diameter+8,Diameter,70);
   fullName.Font=Typography.Create(17,true,GraphicsUnit.Pixel);fullName.ForeColor=DialText;fullName.AutoSize=true;fullName.MaximumSize=new Size(Diameter-56,0);fullName.Location=new Point(24,16);caption.Controls.Add(fullName);
   caption.MouseEnter+=delegate{expiry.Stop();};fullName.MouseEnter+=delegate{expiry.Stop();};caption.MouseLeave+=delegate{ResetTimer();};fullName.MouseLeave+=delegate{ResetTimer();};
   fullName.MouseDown+=delegate(object sender,MouseEventArgs e){Point p=PointToClient(fullName.PointToScreen(e.Location));OnMouseDown(new MouseEventArgs(e.Button,e.Clicks,p.X,p.Y,e.Delta));};
   caption.MouseDown+=delegate(object sender,MouseEventArgs e){Point p=PointToClient(caption.PointToScreen(e.Location));OnMouseDown(new MouseEventArgs(e.Button,e.Clicks,p.X,p.Y,e.Delta));};
   animation.Interval=16; animation.Tick+=delegate {
    bool moving=false;
    for(int i=StartIndex();i<Math.Min(StartIndex()+8,Data.Apps.Count);i++) {
     float value=emphasis.ContainsKey(i)?emphasis[i]:0, target=i==Selected?1:0;
     value+=(target-value)*0.24f; if(Math.Abs(target-value)<0.01f)value=target;else moving=true;emphasis[i]=value;
    }
    Present();if(!moving)animation.Stop();
   };
   expiry.Interval=4500; expiry.Tick += delegate { Dismiss(); };
  }
  protected override void OnMouseDown(MouseEventArgs e) {
   base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;
   pointerDown=true;dragging=false;pointerStart=PointToScreen(e.Location);windowStart=Location;
   expiry.Stop();Capture=true;
  }
  protected override void OnMouseMove(MouseEventArgs e) {
   base.OnMouseMove(e);if(!pointerDown){Cursor=Cursors.Hand;if(e.Y>=Diameter)expiry.Stop();return;}
   Point current=PointToScreen(e.Location);int dx=current.X-pointerStart.X,dy=current.Y-pointerStart.Y;
   Size threshold=SystemInformation.DragSize;
   if(!dragging && (Math.Abs(dx)>threshold.Width/2 || Math.Abs(dy)>threshold.Height/2))dragging=true;
   if(dragging) {Cursor=Cursors.SizeAll;Location=new Point(windowStart.X+dx,windowStart.Y+dy);}
  }
  void FinishPointer() {
   bool moved=dragging;pointerDown=false;dragging=false;Capture=false;Cursor=Cursors.Default;
   if(moved) {
    Location=VisibleLocation(Location);Data.OverlayX=Left;Data.OverlayY=Top;
    if(PositionChanged!=null)PositionChanged();
   }
   if(Visible)ResetTimer();
  }
  protected override void OnMouseCaptureChanged(EventArgs e) {base.OnMouseCaptureChanged(e);if(pointerDown&&!Capture)FinishPointer();}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);if(Visible)ResetTimer();}
  protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(e.Y>=Diameter){captionScroll=Math.Max(0,Math.Min(Math.Max(0,fullName.Height-caption.Height+20),captionScroll-e.Delta/3));expiry.Stop();Present();}}
  protected override void OnMouseUp(MouseEventArgs e) {
   base.OnMouseUp(e);if(e.Button!=MouseButtons.Left||!pointerDown)return;
   bool moved=dragging;FinishPointer();if(moved)return;
   if(e.Y>=Diameter){if(fullName.Height>caption.Height-20){captionScroll=Math.Max(0,Math.Min(fullName.Height-caption.Height+20,captionScroll+(e.Y<(Diameter+3+caption.Height/2)?-48:48)));Present();expiry.Stop();}return;}
   float x=e.X*DesignSize/Diameter,y=e.Y*DesignSize/Diameter;
   if(Distance(x,y,270,270)<77) {Execute();return;}
   int start=StartIndex(),count=Math.Min(8,Data.Apps.Count-start);
   for(int n=0;n<count;n++) {PointF p=Position(n,count);int i=start+n;float size=74+26*Level(i);if(Distance(x,y,p.X,p.Y)<=size/2) {Selected=i;Execute();return;} }
  }
  Point VisibleLocation(Point requested) {
   Rectangle work=Screen.FromPoint(requested).WorkingArea;
   return new Point(Math.Max(work.Left,Math.Min(requested.X,work.Right-Width)),Math.Max(work.Top,Math.Min(requested.Y,work.Bottom-Height)));
  }
  protected override bool ShowWithoutActivation { get { return true; } }
  protected override CreateParams CreateParams { get { CreateParams c=base.CreateParams; c.ExStyle |= 0x08000000 | 0x80 | 0x80000; return c; } }
  int StartIndex() { return Math.Max(0,Selected/8*8); }
  float Level(int i) {return emphasis.ContainsKey(i)?emphasis[i]:(i==Selected?1:0);}
  static double Distance(float x,float y,float cx,float cy) {return Math.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy));}
  PointF Position(int n,int count) {double a=-Math.PI/2+n*2*Math.PI/Math.Max(1,count);return new PointF(270+(float)Math.Cos(a)*171,270+(float)Math.Sin(a)*171);}
  void LayoutCaption(){
   fullName.Text=Data.Apps.Count==0?L10n.T("설정에서 앱을 추가하세요"):Data.Apps[Selected].Name;
   Font nextFont=Typography.ForText(fullName.Text,16,true,GraphicsUnit.Pixel);
   if(fullName.Font.Equals(nextFont))nextFont.Dispose();else {Font oldFont=fullName.Font;fullName.Font=nextFont;oldFont.Dispose();}
   using(Bitmap measure=new Bitmap(1,1))using(Graphics g=Graphics.FromImage(measure)){
    int width=Math.Max(120,Math.Min((int)(Diameter*.78f),(int)Math.Ceiling(g.MeasureString(fullName.Text,fullName.Font).Width)+40));
    caption.SetBounds((Diameter-width)/2,Diameter+3,width,caption.Height);
    fullName.MaximumSize=new Size(width-32,0);fullName.AutoSize=false;fullName.Size=new Size(width-32,(int)Math.Ceiling(g.MeasureString(fullName.Text,fullName.Font,width-32).Height)+4);
   }
   caption.AutoScrollPosition=Point.Empty;captionScroll=0;
   caption.Height=Math.Min(128,Math.Max(42,fullName.Height+20));
   ClientSize=new Size(Diameter,Diameter+7+caption.Height);
  }
  public void ResizeDial(){LayoutCaption();Location=VisibleLocation(Location);Present();}
  public void Reveal() {
   Selected=Math.Max(0,Math.Min(Selected,Data.Apps.Count-1));
   LayoutCaption();
   emphasis.Clear();for(int i=0;i<Data.Apps.Count;i++)emphasis[i]=i==Selected?1:0;
   Rectangle work=Screen.FromPoint(Cursor.Position).WorkingArea;
   Point desired=Data.OverlayX.HasValue&&Data.OverlayY.HasValue?new Point(Data.OverlayX.Value,Data.OverlayY.Value):new Point(work.Right-Width-24,work.Top+(work.Height-Height)/2);
   Location=VisibleLocation(desired);
   Show(); ResetTimer(); Present();
  }
  void ResetTimer() { expiry.Stop(); if(!pointerDown)expiry.Start(); }
  public void Turn(int delta) {
   if(!Visible) Reveal();
   if(Data.Apps.Count>0) Selected=(Selected+delta+Data.Apps.Count)%Data.Apps.Count;
   LayoutCaption();Location=VisibleLocation(Location);
   if(SystemInformation.IsMenuAnimationEnabled)animation.Start();else {emphasis.Clear();for(int i=0;i<Data.Apps.Count;i++)emphasis[i]=i==Selected?1:0;} ResetTimer(); Present();
  }
  public void Push() { if(!Visible) Reveal(); else Execute(); }
  void Execute() { if(pointerDown||Data.Apps.Count==0) return; AppEntry a=Data.Apps[Selected]; Dismiss(); Launcher.Run(a); }
  public void Dismiss() { if(pointerDown)FinishPointer();expiry.Stop(); animation.Stop(); Hide(); if(ClosedOverlay!=null) ClosedOverlay(); }
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){Present();}
  void Present(){if(!Visible||presenting)return;presenting=true;try{using(Bitmap frame=RenderFrame())LayeredWindow.Present(Handle,frame,Location);}finally{presenting=false;}}
  public Bitmap RenderFrame(){
   using(Bitmap high=new Bitmap(Width*2,Height*2,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) {
    using(Graphics g=Graphics.FromImage(high)){
     g.Clear(Color.Transparent);g.ScaleTransform(2,2);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
     GraphicsState state=g.Save();PaintDial(g);g.Restore(state);
     RectangleF card=new RectangleF(caption.Left,Diameter+3,caption.Width,caption.Height);
     using(GraphicsPath path=RoundRect(card,20)){
      using(LinearGradientBrush b=new LinearGradientBrush(card,Color.FromArgb(248,255,255,255),Color.FromArgb(241,236,246,241),80f))g.FillPath(b,path);
      using(Pen p=new Pen(Color.FromArgb(225,255,255,255),1.3f))g.DrawPath(p,path);
     }
     bool overflow=fullName.Height>card.Height-20;
     GraphicsState clip=g.Save();g.SetClip(new RectangleF(card.X+16,card.Y+10,card.Width-32,card.Height-20));
     using(Brush b=new SolidBrush(DialText))using(StringFormat f=new StringFormat()){
      f.Trimming=StringTrimming.None;f.Alignment=StringAlignment.Center;f.LineAlignment=overflow?StringAlignment.Near:StringAlignment.Center;
      g.DrawString(fullName.Text,fullName.Font,b,new RectangleF(card.X+16,card.Y+10-captionScroll,card.Width-32,overflow?fullName.Height+4:card.Height-20),f);
     }g.Restore(clip);
     if(overflow){float track=card.Height-20;using(Brush b=new SolidBrush(Color.FromArgb(175,193,183)))g.FillRectangle(b,card.Right-8,card.Y+10+captionScroll/(float)Math.Max(1,fullName.Height-caption.Height+20)*(track-20),2,20);}
    }
    Bitmap frame=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(Graphics g=Graphics.FromImage(frame)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(high,new Rectangle(0,0,Width,Height),0,0,high.Width,high.Height,GraphicsUnit.Pixel);}return frame;
   }
  }
  static GraphicsPath RoundRect(RectangleF r,float radius){GraphicsPath p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  void PaintDial(Graphics g) {
   g.ScaleTransform(Diameter/DesignSize,Diameter/DesignSize);
   g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   for(int n=12;n>=1;n--)using(Brush shadow=new SolidBrush(Color.FromArgb(2,26,58,45)))g.FillEllipse(shadow,16-n,21-n,508+n*2,508+n*2);
   using(LinearGradientBrush surface=new LinearGradientBrush(new Rectangle(16,16,508,508),Color.FromArgb(224,255,255,255),Color.FromArgb(199,223,239,233),55f))g.FillEllipse(surface,16,16,508,508);
   using(LinearGradientBrush edge=new LinearGradientBrush(new Rectangle(16,16,508,508),Color.White,Color.Transparent,55f)){
    edge.InterpolationColors=new ColorBlend{Colors=new Color[]{Color.FromArgb(252,255,255,255),Color.FromArgb(110,255,255,255),Color.FromArgb(75,67,113,104),Color.FromArgb(220,255,255,255)},Positions=new float[]{0,.32f,.7f,1}};
    using(Pen rim=new Pen(edge,3f))g.DrawEllipse(rim,17,17,506,506);
   }
   using(Pen inner=new Pen(Color.FromArgb(100,255,255,255),1.2f))g.DrawEllipse(inner,21,21,498,498);
   GraphicsState glassClip=g.Save();using(GraphicsPath shape=new GraphicsPath()){shape.AddEllipse(18,18,504,504);g.SetClip(shape,CombineMode.Intersect);}
   using(LinearGradientBrush gleam=new LinearGradientBrush(new Rectangle(38,30,464,270),Color.FromArgb(38,255,255,255),Color.FromArgb(0,255,255,255),90f))g.FillEllipse(gleam,38,30,464,270);
   g.Restore(glassClip);
   using(LinearGradientBrush center=new LinearGradientBrush(new Rectangle(181,181,178,178),Color.FromArgb(248,255,255,255),Color.FromArgb(235,237,246,240),65f))g.FillEllipse(center,181,181,178,178);
   using(Pen centerBorder=new Pen(Color.FromArgb(215,255,255,255),2))g.DrawEllipse(centerBorder,181,181,178,178);
   int start=StartIndex(),count=Math.Min(8,Data.Apps.Count-start);
   for(int n=0;n<count;n++) {
    int i=start+n;float level=Level(i),size=74+26*level;PointF p=Position(n,count);
    RectangleF box=new RectangleF(p.X-size/2,p.Y-size/2,size,size);
    using(Brush shadow=new SolidBrush(Color.FromArgb(12,41,67,52)))g.FillEllipse(shadow,box.X-2,box.Y+4,size+4,size+4);
    using(LinearGradientBrush fill=new LinearGradientBrush(box,Color.FromArgb(248,255,255,255),Color.FromArgb(231,(int)(245-16*level),(int)(249-7*level),(int)(246-12*level)),75f))g.FillEllipse(fill,box);
    using(Pen edge=new Pen(Color.FromArgb(218,255,255,255),1.8f))g.DrawEllipse(edge,box);
    if(i==Selected){using(Pen rim=new Pen(Color.FromArgb(135,69,139,111),1.8f))g.DrawEllipse(rim,box);using(Brush dot=new SolidBrush(Color.FromArgb(69,139,111)))g.FillEllipse(dot,p.X-3,p.Y+size/2+13,6,6);}
    DrawApp(g,Data.Apps[i],p.X,p.Y,49+13*level);
   }
   if(Data.Apps.Count>0)DrawApp(g,Data.Apps[Selected],270,251,58);
   using(Font action=Typography.Create(Math.Max(20,12*DesignSize/Diameter),false,GraphicsUnit.Pixel))PaintText(g,Data.Apps.Count==0?L10n.T("앱 추가"):L10n.T("눌러서 열기"),action,new RectangleF(193,292,154,30),DialText);
   string hint=Data.Apps.Count==0?"":(Selected+1)+" / "+Data.Apps.Count;
   using(Font small=Typography.Latin(Math.Max(16,11*DesignSize/Diameter),false,GraphicsUnit.Pixel))PaintText(g,hint,small,new RectangleF(198,322,144,28),DialMuted);
  }
  static void PaintText(Graphics g,string text,Font font,RectangleF bounds,Color color) {
   using(Brush brush=new SolidBrush(color))using(StringFormat format=new StringFormat()) {
    format.Alignment=StringAlignment.Center;format.LineAlignment=StringAlignment.Center;
    format.Trimming=StringTrimming.None;
    g.DrawString(text,font,brush,bounds,format);
   }
  }
  void DrawApp(Graphics g,AppEntry app,float x,float y,float size) {
   if(!String.IsNullOrWhiteSpace(app.IconPath)) {g.DrawImage(IconStore.Get(app),new RectangleF(x-size/2,y-size/2,size,size));return;}
   if(app.Name.Equals("Codex",StringComparison.OrdinalIgnoreCase)) {
    using(Pen pen=new Pen(DialText,size*0.08f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawLines(pen,new PointF[]{new PointF(x-size*.35f,y-size*.25f),new PointF(x-size*.10f,y),new PointF(x-size*.35f,y+size*.25f)});g.DrawLine(pen,x+size*.05f,y+size*.25f,x+size*.36f,y+size*.25f);}return;
   }
   using(Bitmap bitmap=GetIcon(app.Target).ToBitmap())g.DrawImage(bitmap,new RectangleF(x-size/2,y-size/2,size,size));
  }
  Icon GetIcon(string target) {
   if(icons.ContainsKey(target)) return icons[target];
   Icon icon=null; try { string p=Environment.ExpandEnvironmentVariables(target); if(!File.Exists(p)&&!Path.IsPathRooted(p)) {string candidate=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),p);if(File.Exists(candidate))p=candidate;else p=Path.Combine(Environment.SystemDirectory,p);} if(File.Exists(p)) icon=Icon.ExtractAssociatedIcon(p); } catch {}
   if(icon==null) icon=(Icon)SystemIcons.Application.Clone(); icons[target]=icon; return icon;
  }
  protected override void Dispose(bool disposing) { if(disposing) { expiry.Dispose(); animation.Dispose();caption.Dispose(); foreach(Icon icon in icons.Values) icon.Dispose(); } base.Dispose(disposing); }
  public void TestLayout(string folder){
   int previous=Data.DialSize;string oldName=Data.Apps[Selected].Name;
   string longName="Very Long Application Name / 프로젝트 개발 환경과 문서 도구를 여는 아주 긴 프로그램 이름 ";
   Data.Apps[Selected].Name=String.Concat(Enumerable.Repeat(longName,12));
   foreach(int size in new int[]{280,360,560}) {
    Data.DialSize=size;Reveal();
    if(Width!=size||fullName.Text!=Data.Apps[Selected].Name||fullName.AutoEllipsis||fullName.Height<=caption.Height)throw new Exception("Long title or resize layout failed");
    using(Bitmap b=RenderFrame()){
     if(b.GetPixel(0,0).A!=0)throw new Exception("Layer corner is opaque");
     bool partial=false;for(int x=0;x<b.Width;x++)if(b.GetPixel(x,10).A>0&&b.GetPixel(x,10).A<255)partial=true;
     if(!partial)throw new Exception("Smooth alpha edge missing");b.Save(Path.Combine(folder,"long-title-"+size+".png"));
    }
    Storage.Save(Data);if(Storage.Load().DialSize!=size)throw new Exception("Size persistence failed");Dismiss();
   }
   Data.Apps[Selected].Name=oldName;Data.DialSize=previous;
  }
  public void TestPosition() {
   Reveal();Point before=Location;
   OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,Width/2,Height/2,0));
   OnMouseMove(new MouseEventArgs(MouseButtons.Left,0,Width/2-60,Height/2+25,0));
   if(Location==before||expiry.Enabled)throw new Exception("Drag movement or timeout pause failed");
   OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,Width/2,Height/2,0));
   if(!Visible||!Data.OverlayX.HasValue||!expiry.Enabled)throw new Exception("Drag triggered launch or did not save");
   Point saved=Location;Dismiss();Reveal();if(Location!=saved)throw new Exception("Reopen position failed");
   Settings loaded=Storage.Load();using(Overlay reopened=new Overlay(loaded)) {reopened.Reveal();if(reopened.Location!=saved)throw new Exception("Restart position failed");reopened.Dismiss();}
   Point recovered=VisibleLocation(new Point(-100000,-100000));
   if(!Screen.AllScreens.Any(s=>s.WorkingArea.Contains(new Rectangle(recovered,Size))))throw new Exception("Offscreen position recovery failed");
   Dismiss();
  }
 }
 class EntryEditor : Form {
  public AppEntry Result;
  TextBox name=new TextBox(),target=new TextBox(),arguments=new TextBox();
  string iconPath, pendingIcon;
  PictureBox iconPreview=new PictureBox();Label iconStatus=new Label(),error=new Label();
  public EntryEditor(AppEntry current) {
   Icon=Brand.Icon;
   Text=L10n.T("앱과 아이콘"); Font=Typography.Create(10); ClientSize=new Size(570,490); StartPosition=FormStartPosition.CenterParent;
   FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
   AddField(L10n.T("표시 이름"),name,20); AddField(L10n.T("실행 파일 / 바로가기 / 폴더 / URL"),target,85); AddField(L10n.T("실행 인수 (선택)"),arguments,150);
   target.Width=435;
   Button browse=new Button { Text=L10n.T("찾기"),Left=470,Top=111,Width=75 };
   browse.Click+=delegate { using(OpenFileDialog d=new OpenFileDialog { Filter=L10n.T("실행 파일 및 바로가기|*.exe;*.lnk;*.url|모든 파일|*.*"),DereferenceLinks=false }) { if(d.ShowDialog(this)==DialogResult.OK) { target.Text=d.FileName; if(name.Text.Trim()=="") name.Text=Path.GetFileNameWithoutExtension(d.FileName); } } };
   iconPreview.SetBounds(24,240,80,80);iconPreview.SizeMode=PictureBoxSizeMode.Zoom;iconPreview.AccessibleName=L10n.T("아이콘 미리보기");Controls.Add(iconPreview);
   Controls.Add(new Label {Text=L10n.T("앱 아이콘"),Left=124,Top=233,Width=350});
   Button choose=new Button {Text=L10n.T("PNG / SVG 선택"),Left=124,Top=263,Width=175,Height=38};
   Button reset=new Button {Text=L10n.T("기본 아이콘"),Left=311,Top=263,Width=135,Height=38};
   iconStatus.SetBounds(124,309,420,44);iconStatus.Text=L10n.T("투명 배경 지원 · 최대 4MB");
   error.SetBounds(24,366,520,46);
   Controls.AddRange(new Control[]{choose,reset,iconStatus,error});
   choose.Click+=delegate {using(OpenFileDialog d=new OpenFileDialog {Filter=L10n.T("아이콘 이미지|*.png;*.svg"),Title=L10n.T("앱 아이콘 선택")})if(d.ShowDialog(this)==DialogResult.OK)try {
    Bitmap decoded=IconStore.Decode(d.FileName);if(iconPreview.Image!=null)iconPreview.Image.Dispose();iconPreview.Image=decoded;pendingIcon=d.FileName;iconStatus.Text=Path.GetFileName(d.FileName)+L10n.T("\n저장하면 원본을 옮겨도 유지됩니다.");error.Text="";
   }catch(Exception ex){error.Text=L10n.T("아이콘을 불러오지 못했습니다. ")+ex.Message;}};
   reset.Click+=delegate {pendingIcon=null;iconPath=null;RefreshIcon();iconStatus.Text=L10n.T("앱의 기본 아이콘을 사용합니다.");error.Text="";};
   Button save=new Button { Text=L10n.T("저장"),Left=354,Top=430,Width=90,Height=38 }; Button cancel=new Button { Text=L10n.T("취소"),Left=456,Top=430,Width=90,Height=38,DialogResult=DialogResult.Cancel };
   save.Click+=delegate { if(String.IsNullOrWhiteSpace(name.Text)||String.IsNullOrWhiteSpace(target.Text)) {error.Text=L10n.T("표시 이름과 실행 대상을 입력하세요.");return;}try {
    if(pendingIcon!=null)iconPath=IconStore.Import(pendingIcon);
    Result=new AppEntry { Name=name.Text.Trim(),Target=target.Text.Trim().Trim('"'),Arguments=arguments.Text.Trim(),IconPath=iconPath }; DialogResult=DialogResult.OK;
   }catch(Exception ex){error.Text=L10n.T("저장 실패: ")+ex.Message;} };
   Controls.AddRange(new Control[]{browse,save,cancel}); AcceptButton=save; CancelButton=cancel;
   if(current!=null) { name.Text=current.Name;target.Text=current.Target;arguments.Text=current.Arguments;iconPath=current.IconPath; }
   Theme.Apply(this);save.BackColor=Theme.Accent;save.ForeColor=Theme.Background;error.ForeColor=Color.FromArgb(255,166,166);RefreshIcon();
  }
  void RefreshIcon(){if(iconPreview.Image!=null)iconPreview.Image.Dispose();iconPreview.Image=new Bitmap(IconStore.Get(new AppEntry {Target=target.Text,IconPath=iconPath}));}
  protected override void Dispose(bool disposing){if(disposing&&iconPreview.Image!=null)iconPreview.Image.Dispose();base.Dispose(disposing);}
  void AddField(string text,TextBox field,int y) { Controls.Add(new Label {Text=text,Left=20,Top=y,Width=520}); field.AccessibleName=text;field.SetBounds(20,y+26,525,26); Controls.Add(field); }
 }
 class MainForm : Form {
  Settings data; Overlay overlay; NotifyIcon tray; ListBox apps=new ListBox(); ComboBox[] keys=new ComboBox[3];
  Label status=new Label(); bool refresh,exiting; bool escapeRegistered; List<int> registered=new List<int>();
  public MainForm(Settings settings) {
   Icon=Brand.Icon;
   data=settings; Text=L10n.T("MiniDeck · 3키 + 노브 런처"); Font=Typography.Create(10); ClientSize=new Size(800,720);
   StartPosition=FormStartPosition.CenterScreen; FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false;
   BackColor=Color.FromArgb(245,247,251);
   Controls.Add(new PictureBox {Image=Brand.Image,SizeMode=PictureBoxSizeMode.Zoom,Left=30,Top=22,Width=44,Height=44,AccessibleName=L10n.T("MiniDeck 아이콘")});
   Controls.Add(new Label {Text="MiniDeck",Font=Typography.Latin(26,true),Left=86,Top=20,Width=400,Height=48});
   Controls.Add(new Label {Text=L10n.T("자주 쓰는 앱을, 손끝에서.   /   3 KEYS + 1 DIAL"),Left=29,Top=76,Width=700});
   apps.SetBounds(30,122,440,290); apps.DisplayMember="Name"; Controls.Add(apps);
   apps.AccessibleName=L10n.T("등록한 앱 목록");
   apps.BorderStyle=BorderStyle.None;apps.DrawMode=DrawMode.OwnerDrawFixed;apps.ItemHeight=64;apps.IntegralHeight=false;
   apps.DrawItem+=delegate(object sender,DrawItemEventArgs e){if(e.Index<0)return;AppEntry a=(AppEntry)apps.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;
    using(Brush b=new SolidBrush(selected?Color.FromArgb(43,56,80):Theme.Surface))e.Graphics.FillRectangle(b,e.Bounds);
    if(selected)using(Brush b=new SolidBrush(Theme.Accent))e.Graphics.FillRectangle(b,e.Bounds.X,e.Bounds.Y+12,3,40);
    e.Graphics.DrawImage(IconStore.Get(a),new Rectangle(e.Bounds.X+18,e.Bounds.Y+16,32,32));
    TextRenderer.DrawText(e.Graphics,a.Name,Font,new Rectangle(e.Bounds.X+64,e.Bounds.Y+10,e.Bounds.Width-80,25),Theme.Text,TextFormatFlags.EndEllipsis);
    using(Font small=Typography.Create(8.5f))TextRenderer.DrawText(e.Graphics,String.IsNullOrWhiteSpace(a.IconPath)?L10n.T("기본 아이콘"):L10n.T("커스텀 아이콘"),small,new Rectangle(e.Bounds.X+64,e.Bounds.Y+35,e.Bounds.Width-80,22),Theme.Muted,TextFormatFlags.EndEllipsis);
    if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
   };
   apps.DoubleClick+=delegate {EditEntry(true);};
   ButtonAt(L10n.T("앱 추가"),490,122,delegate { EditEntry(false); });
   ButtonAt(L10n.T("앱 / 아이콘 수정"),490,168,delegate { EditEntry(true); });
   ButtonAt(L10n.T("삭제"),490,214,delegate { DeleteEntry(); });
   ButtonAt(L10n.T("위로 ↑"),490,260,delegate { MoveEntry(-1); });
   ButtonAt(L10n.T("아래로 ↓"),490,306,delegate { MoveEntry(1); });
   ButtonAt(L10n.T("선택 앱 실행"),490,352,delegate { if(apps.SelectedIndex>=0) Launcher.Run(data.Apps[apps.SelectedIndex]); });
   ButtonAt(L10n.T("키보드 온보드 설정"),620,22,delegate {overlay.Dismiss();using(OnboardForm form=new OnboardForm())form.ShowDialog(this);});
   for(int i=0;i<3;i++) {
    int n=i; int x=30+i*250;
    Controls.Add(new Label {Text=L10n.T("키 ")+(i+1)+"  ·  Ctrl+Alt+Shift+F"+(i+1),Left=x,Top=433,Width=240});
    keys[i]=new DarkComboBox {Left=x,Top=461,Width=233,DropDownStyle=ComboBoxStyle.DropDownList};
    keys[i].AccessibleName=L10n.T("키 ")+(i+1)+L10n.T("에 할당할 앱");
    keys[i].DrawMode=DrawMode.OwnerDrawFixed;keys[i].ItemHeight=26;keys[i].FlatStyle=FlatStyle.Flat;
    keys[i].DrawItem+=delegate(object sender,DrawItemEventArgs e){ComboBox box=(ComboBox)sender;using(Brush b=new SolidBrush(Theme.Surface))e.Graphics.FillRectangle(b,e.Bounds);if(e.Index>=0)TextRenderer.DrawText(e.Graphics,box.Items[e.Index].ToString(),box.Font,e.Bounds,Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};
    keys[i].SelectedIndexChanged+=delegate { if(!refresh) {data.Keys[n]=keys[n].SelectedIndex-1; Save();} }; Controls.Add(keys[i]);
   }
   ButtonAt(L10n.T("◀ 회전"),30,510,delegate { OpenOverlay(-1); });
   ButtonAt(L10n.T("누르기 / 열기"),200,510,delegate { OpenOverlay(0); });
   ButtonAt(L10n.T("회전 ▶"),370,510,delegate { OpenOverlay(1); });
   CheckBox startup=new CheckBox {Text=L10n.T("Windows 로그인 시 자동 시작"),Left=540,Top=514,Width=250,Checked=HasStartup()};
   startup.CheckedChanged+=delegate { try { using(RegistryKey k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {if(startup.Checked) k.SetValue("MiniDeck","\""+Application.ExecutablePath+"\" --background"); else k.DeleteValue("MiniDeck",false);} } catch(Exception ex) {MessageBox.Show(ex.Message);} }; Controls.Add(startup);
   Controls.Add(new Label {Text=L10n.T("노브 매핑: Ctrl+Alt+Shift + F4(왼쪽) / F5(오른쪽) / F6(누르기)\n닫기 버튼을 누르면 트레이에 남습니다. 종료는 트레이 메뉴에서 선택하세요."),Left=30,Top=564,Width=745,Height=45});
   status.SetBounds(30,612,745,30); Controls.Add(status);
   overlay=new Overlay(data); overlay.PositionChanged=Save; overlay.ClosedOverlay=delegate { if(escapeRegistered) Native.UnregisterHotKey(Handle,99); escapeRegistered=false; };
   Controls.Add(new Label{Text=L10n.T("다이얼 크기"),Left=30,Top=660,Width=110});
   TrackBar dialSize=new TrackBar{Minimum=280,Maximum=560,TickFrequency=40,SmallChange=10,LargeChange=40,Value=Math.Max(280,Math.Min(560,data.DialSize==0?360:data.DialSize)),Left=140,Top=648,Width=410,Height=45,AccessibleName=L10n.T("다이얼 크기")};
   Label sizeValue=new Label{Text=dialSize.Value+" px",Left=560,Top=660,Width=100};Controls.Add(dialSize);Controls.Add(sizeValue);
   dialSize.ValueChanged+=delegate{data.DialSize=dialSize.Value;sizeValue.Text=dialSize.Value+" px";overlay.ResizeDial();Save();};
   tray=new NotifyIcon {Icon=Brand.Icon,Text="MiniDeck",Visible=true};
   ContextMenuStrip menu=new ContextMenuStrip(); menu.Items.Add(L10n.T("설정 열기"),null,delegate {ShowSettings();}); menu.Items.Add(L10n.T("앱 목록"),null,delegate {OpenOverlay(0);}); menu.Items.Add(L10n.T("종료"),null,delegate {exiting=true;Close();}); tray.ContextMenuStrip=menu; tray.DoubleClick+=delegate {ShowSettings();};
   RefreshList(0);
   Theme.Apply(this);
  }
  void ButtonAt(string text,int x,int y,Action action) { Button b=new Button {Text=text,Left=x,Top=y,Width=150,Height=35}; b.Click+=delegate { action(); }; Controls.Add(b); }
  bool HasStartup() { using(RegistryKey k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return k!=null && k.GetValue("MiniDeck")!=null; }
  void ShowSettings() { Show(); WindowState=FormWindowState.Normal; Activate(); }
  void Save() { try {Storage.Save(data);} catch(Exception ex) {MessageBox.Show(this,L10n.T("설정 저장 실패\n")+ex.Message);} }
  void RefreshList(int selected) {
   refresh=true; apps.Items.Clear(); foreach(AppEntry a in data.Apps) apps.Items.Add(a);
   if(data.Apps.Count>0) apps.SelectedIndex=Math.Max(0,Math.Min(selected,data.Apps.Count-1));
   for(int i=0;i<3;i++) { keys[i].Items.Clear(); keys[i].Items.Add(L10n.T("(할당 없음)")); foreach(AppEntry a in data.Apps) keys[i].Items.Add(a.Name); keys[i].SelectedIndex=data.Keys[i]+1; }
   refresh=false;
  }
  void EditEntry(bool edit) {
   int i=apps.SelectedIndex; if(edit&&i<0) return;
   using(EntryEditor f=new EntryEditor(edit?data.Apps[i]:null)) if(f.ShowDialog(this)==DialogResult.OK) {
    overlay.Dismiss(); if(edit) data.Apps[i]=f.Result; else {data.Apps.Add(f.Result);i=data.Apps.Count-1;} Save();RefreshList(i);
   }
  }
  void DeleteEntry() {
   int i=apps.SelectedIndex;if(i<0)return; overlay.Dismiss(); data.Apps.RemoveAt(i);
   for(int n=0;n<3;n++) {if(data.Keys[n]==i)data.Keys[n]=-1;else if(data.Keys[n]>i)data.Keys[n]--;}
   Save();RefreshList(i);
  }
  void MoveEntry(int delta) {
   int i=apps.SelectedIndex,j=i+delta;if(i<0||j<0||j>=data.Apps.Count)return;overlay.Dismiss();
   AppEntry a=data.Apps[i];data.Apps[i]=data.Apps[j];data.Apps[j]=a;
   for(int n=0;n<3;n++) {if(data.Keys[n]==i)data.Keys[n]=j;else if(data.Keys[n]==j)data.Keys[n]=i;}
   Save();RefreshList(j);
  }
  void OpenOverlay(int direction) {
   if(direction==0)overlay.Push();else overlay.Turn(direction);
   if(overlay.Visible&&!escapeRegistered)escapeRegistered=Native.RegisterHotKey(Handle,99,0,27);
  }
  protected override void OnHandleCreated(EventArgs e) {
   base.OnHandleCreated(e); List<string> failed=new List<string>();
   for(int i=0;i<6;i++) { if(Native.RegisterHotKey(Handle,i+1,0x4007,(uint)(112+i)))registered.Add(i+1);else failed.Add("F"+(i+1)); }
   status.Text=failed.Count==0?L10n.T("● 단축키 6개 준비됨 · 현재는 모든 키보드의 지정 단축키에 반응합니다."):L10n.T("단축키 등록 실패: ")+String.Join(", ",failed.ToArray())+L10n.T(" (다른 프로그램 사용 여부 확인)");
   status.ForeColor=failed.Count==0?Color.FromArgb(146,221,183):Color.FromArgb(255,166,166);
  }
  protected override void WndProc(ref Message m) {
   if(m.Msg==0x0312) {
    int id=m.WParam.ToInt32();
    if(id>=1&&id<=3) { int i=data.Keys[id-1];if(i>=0&&i<data.Apps.Count){overlay.Dismiss();Launcher.Run(data.Apps[i]);} }
    else if(id==4)OpenOverlay(-1);else if(id==5)OpenOverlay(1);else if(id==6)OpenOverlay(0);else if(id==99)overlay.Dismiss();
   }
   base.WndProc(ref m);
  }
  protected override void OnFormClosing(FormClosingEventArgs e) {
   if(!exiting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();return;}
   overlay.Dismiss(); foreach(int id in registered)Native.UnregisterHotKey(Handle,id);
   overlay.Dispose();tray.Visible=false;tray.Dispose();base.OnFormClosing(e);
  }
  public void Preview(string folder) {
   Show();Application.DoEvents();using(Bitmap b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(Point.Empty,Size));b.Save(Path.Combine(folder,"settings-preview.png"));}
   using(EntryEditor editor=new EntryEditor(data.Apps.FirstOrDefault())) {editor.Show();Application.DoEvents();using(Bitmap b=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(b,new Rectangle(Point.Empty,editor.Size));b.Save(Path.Combine(folder,"icon-editor-preview.png"));}editor.Close();}
   using(OnboardForm onboard=new OnboardForm()){onboard.Show();Application.DoEvents();using(Bitmap b=new Bitmap(onboard.Width,onboard.Height)){onboard.DrawToBitmap(b,new Rectangle(Point.Empty,onboard.Size));b.Save(Path.Combine(folder,"onboard-preview.png"));}onboard.Close();}
   overlay.Reveal();Application.DoEvents();using(Bitmap b=overlay.RenderFrame()){b.Save(Path.Combine(folder,"overlay-preview.png"));}
   exiting=true;Close();
  }
  public void SelfTest(string folder) {
   List<string> results=new List<string>();
   BrowserForeground.SelfTest();results.Add("PASS: Chrome foreground target resolves from the connected native host; unrelated and cyclic ancestors rejected");
   BoardProtocol.SelfTest();results.Add("PASS: onboard report encoding, slot order, media keys, invalid slot rejection (no USB writes)");
   IconStore.SelfTest();results.Add("PASS: SVG/PNG import, transparency, aspect ratio, source independence, invalid icon rejection, settings roundtrip");
   Show(); Application.DoEvents();
   if(registered.Count!=6)throw new Exception("Hotkey registration failed"); results.Add("PASS: six global hotkeys registered");
   apps.SelectedIndex=0; MoveEntry(1);
   if(data.Keys[0]!=1 || data.Keys[1]!=0)throw new Exception("Reorder lost key bindings"); results.Add("PASS: reorder preserves key bindings");
   DeleteEntry(); if(data.Keys[0]!=-1 || data.Keys[2]!=1)throw new Exception("Delete lost key bindings"); results.Add("PASS: delete clears and shifts bindings");
   Settings restored=Storage.Load(); if(restored.Apps.Count!=3 || restored.Keys[0]!=-1)throw new Exception("Persistence failed"); results.Add("PASS: settings save and reload");
   Message right=Message.Create(Handle,0x0312,new IntPtr(5),IntPtr.Zero); WndProc(ref right);
   if(!overlay.Visible || overlay.Selected!=1)throw new Exception("Right turn failed");
   Message left=Message.Create(Handle,0x0312,new IntPtr(4),IntPtr.Zero);WndProc(ref left);
   if(overlay.Selected!=0)throw new Exception("Left turn failed"); WndProc(ref left);
   if(overlay.Selected!=2)throw new Exception("Wrap failed");results.Add("PASS: hotkey routing, overlay selection, wrap");
   Message esc=Message.Create(Handle,0x0312,new IntPtr(99),IntPtr.Zero);WndProc(ref esc);
   if(overlay.Visible || escapeRegistered)throw new Exception("Dismiss failed");results.Add("PASS: Escape dismisses and unregisters");
   Message push=Message.Create(Handle,0x0312,new IntPtr(6),IntPtr.Zero);WndProc(ref push);
   if(!overlay.Visible)throw new Exception("Push open failed");
   Stopwatch watch=Stopwatch.StartNew();while(overlay.Visible&&watch.ElapsedMilliseconds<8000){Application.DoEvents();System.Threading.Thread.Sleep(15);}
   if(overlay.Visible)throw new Exception("Timeout failed");results.Add("PASS: push opens, idle timeout dismisses");
   overlay.TestPosition();results.Add("PASS: drag pauses expiry, does not launch, persists across reopen and reload; offscreen recovery");
   overlay.TestLayout(folder);results.Add("PASS: 280/360/560px layouts, long untruncated scrollable titles, persisted size");
   data.Apps.Clear();overlay.Turn(1);overlay.Push();overlay.Dismiss();results.Add("PASS: empty app list safe");
   File.WriteAllLines(Path.Combine(folder,"test-results.txt"),results.ToArray());exiting=true;Close();
  }
 }
 static class Program {
  [STAThread] static void Main(string[] args) {
   Native.SetProcessDPIAware();Typography.Initialize();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   bool fresh;using(System.Threading.Mutex mutex=new System.Threading.Mutex(true,"Local\\MiniDeck.Launcher",out fresh)) {
    if(!fresh){MessageBox.Show(L10n.T("MiniDeck이 이미 실행 중입니다. 작업 표시줄 트레이에서 설정을 여세요."));return;}
    try {
     if(args.Length==2&&args[0]=="--self-test") {
      Storage.Folder=Path.Combine(args[1],"test-data");Storage.FileName=Path.Combine(Storage.Folder,"settings.json");
      new MainForm(Storage.Defaults()).SelfTest(args[1]);return;
     }
     Settings s=(args.Length==2&&args[0]=="--preview")?Storage.Defaults():Storage.Load(); MainForm form=new MainForm(s);
     if(args.Length==2&&args[0]=="--preview"){form.Preview(args[1]);return;}
     if(args.Contains("--background")) form.Shown+=delegate {form.Hide();};
     Application.Run(form);
    } catch(Exception ex) {if(args.Length==2&&(args[0]=="--self-test"||args[0]=="--preview")){File.WriteAllText(Path.Combine(args[1],"diagnostic-error.txt"),ex.ToString());Environment.ExitCode=1;}else MessageBox.Show(L10n.T("MiniDeck 시작 실패\n")+ex.Message);}
   }
  }
 }
}
