using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml;
using Svg;

namespace MiniDeck {
 static class Typography {
  static System.Drawing.Text.PrivateFontCollection fonts=new System.Drawing.Text.PrivateFontCollection();
  [System.Runtime.InteropServices.DllImport("gdi32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern int AddFontResourceEx(string file,uint flags,IntPtr reserved);
  public static void Initialize(){foreach(string path in Directory.GetFiles(Path.Combine(Application.StartupPath,"fonts"),"*.otf")){AddFontResourceEx(path,0x10,IntPtr.Zero);fonts.AddFontFile(path);}}
  public static Font Create(float size,bool strong=false,GraphicsUnit unit=GraphicsUnit.Point){
   string name=strong?"Pretendard SemiBold":"Pretendard";foreach(FontFamily f in fonts.Families)if(f.Name==name)return new Font(f,size,FontStyle.Regular,unit);
   return new Font("Pretendard",size,strong?FontStyle.Bold:FontStyle.Regular,unit);
  }
  public static Font Latin(float size,bool strong=false,GraphicsUnit unit=GraphicsUnit.Point){string name=strong?"Inter SemiBold":"Inter";foreach(FontFamily f in fonts.Families)if(f.Name==name)return new Font(f,size,FontStyle.Regular,unit);return new Font("Inter",size,FontStyle.Regular,unit);}
  public static Font ForText(string text,float size,bool strong=false,GraphicsUnit unit=GraphicsUnit.Point){return System.Text.RegularExpressions.Regex.IsMatch(text??"","[가-힣ㄱ-ㅎㅏ-ㅣ]")?Create(size,strong,unit):Latin(size,strong,unit);}
 }
 static class Brand {
  public static readonly Icon Icon=Load();
  public static readonly Bitmap Image=LoadImage();
  static Bitmap LoadImage(){using(Stream stream=typeof(Brand).Assembly.GetManifestResourceStream("MiniDeck.AppImage"))using(Bitmap bitmap=new Bitmap(stream)){return new Bitmap(bitmap);}}
  static Icon Load(){using(Stream stream=typeof(Brand).Assembly.GetManifestResourceStream("MiniDeck.AppIcon"))using(Icon icon=new Icon(stream)){return (Icon)icon.Clone();}}
 }
 static class EngineIcons {
  static readonly Dictionary<string,Bitmap> cache=new Dictionary<string,Bitmap>();
  public static string For(TextEngine engine){
   Uri url;if(engine==null||!Uri.TryCreate(engine.Url,UriKind.Absolute,out url))return "search";
   switch(url.Host.ToLowerInvariant()){
    case "google.com":case "www.google.com":return "google";
    case "translate.google.com":return "translate";
    case "naver.com":case "www.naver.com":case "search.naver.com":return "naver";
    case "bing.com":case "www.bing.com":return "bing";
    default:return "search";
   }
  }
  public static Bitmap Get(string name){
   Bitmap icon;if(cache.TryGetValue(name,out icon))return icon;
   using(Stream source=typeof(EngineIcons).Assembly.GetManifestResourceStream("MiniDeck.Engine."+name+".svg")){
    if(source==null)throw new InvalidOperationException("Missing bundled engine icon: "+name);
    icon=IconStore.DecodeSvg(source);
   }
   cache[name]=icon;return icon;
  }
 }
 class DarkComboBox : ComboBox {
  public DarkComboBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(Theme.Surface);using(Pen p=new Pen(Focused?Theme.Accent:Theme.Border))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);
   TextRenderer.DrawText(e.Graphics,SelectedItem==null?L10n.T("(할당 없음)"):SelectedItem.ToString(),Font,new Rectangle(10,0,Width-34,Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   using(Brush b=new SolidBrush(Theme.Muted))e.Graphics.FillPolygon(b,new Point[]{new Point(Width-21,Height/2-2),new Point(Width-11,Height/2-2),new Point(Width-16,Height/2+3)});
  }
  protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
 }
 static class Theme {
  public static readonly Color Background=Color.FromArgb(18,21,28), Surface=Color.FromArgb(28,33,43), Border=Color.FromArgb(67,78,97), Text=Color.FromArgb(239,243,250), Muted=Color.FromArgb(177,188,207), Accent=Color.FromArgb(160,187,255);
  public static void Apply(Control root) {
   root.BackColor=Background;root.ForeColor=Text;
   foreach(Control c in root.Controls) {
    Apply(c);
    if(c is TextBox || c is ComboBox || c is ListBox) {c.BackColor=Surface;c.ForeColor=Text;}
    Button b=c as Button;if(b!=null) {b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Border;b.FlatAppearance.MouseOverBackColor=Color.FromArgb(49,60,81);b.FlatAppearance.MouseDownBackColor=Color.FromArgb(66,81,109);b.BackColor=Surface;b.Cursor=Cursors.Hand;}
    if(c is Label && c.Font.Size<18)c.ForeColor=Muted;
   }
  }
 }
 static class IconStore {
  static Dictionary<string,Bitmap> cache=new Dictionary<string,Bitmap>();
  public static Bitmap Decode(string path) {
   FileInfo file=new FileInfo(path);if(!file.Exists || file.Length>4*1024*1024)throw new Exception(L10n.T("4MB 이하 PNG 또는 SVG 파일을 선택하세요."));
   string ext=Path.GetExtension(path).ToLowerInvariant();
   if(ext==".svg") {
    using(Stream input=File.OpenRead(path))return DecodeSvg(input);
   }
   if(ext!=".png")throw new Exception(L10n.T("PNG 또는 SVG 파일만 지원합니다."));
   using(Image input=Image.FromFile(path)) {if(input.Width>4096||input.Height>4096)throw new Exception(L10n.T("4096px 이하 이미지를 선택하세요."));return Normalize(input);}
  }
  public static Bitmap DecodeSvg(Stream input){
    SvgDocument.ResolveExternalXmlEntites=ExternalType.None;SvgDocument.ResolveExternalImages=ExternalType.None;SvgDocument.ResolveExternalElements=ExternalType.None;
    XmlReaderSettings settings=new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024};
    using(XmlReader reader=XmlReader.Create(input,settings)) {
     SvgDocument doc=SvgDocument.Open<SvgDocument>(reader);
     if(doc==null)throw new Exception(L10n.T("SVG 문서를 읽을 수 없습니다."));
     SizeF size=doc.GetDimensions();if(size.Width<=0||size.Height<=0)throw new Exception(L10n.T("SVG에 viewBox 또는 크기가 필요합니다."));
     float scale=256f/Math.Max(size.Width,size.Height);
     using(Bitmap rendered=doc.Draw(Math.Max(1,(int)(size.Width*scale)),Math.Max(1,(int)(size.Height*scale)))) {if(rendered==null)throw new Exception(L10n.T("SVG를 표시할 수 없습니다."));return Normalize(rendered);}
    }
  }
  static Bitmap Normalize(Image input) {
   Bitmap result=new Bitmap(256,256);using(Graphics g=Graphics.FromImage(result)) {
    g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    float scale=Math.Min(256f/input.Width,256f/input.Height);float w=input.Width*scale,h=input.Height*scale;
    g.DrawImage(input,new RectangleF((256-w)/2,(256-h)/2,w,h));
   }return result;
  }
  public static string Import(string source) {
   using(Bitmap image=Decode(source)) {
    string hash;using(SHA256 sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source))).Replace("-","").ToLowerInvariant();
    string folder=Path.Combine(Storage.Folder,"icons");Directory.CreateDirectory(folder);
    string dest=Path.Combine(folder,hash+".png");if(!File.Exists(dest))image.Save(dest,System.Drawing.Imaging.ImageFormat.Png);
    if(Path.GetExtension(source).Equals(".svg",StringComparison.OrdinalIgnoreCase)) {string original=Path.Combine(folder,hash+".svg");if(!File.Exists(original))File.Copy(source,original);}
    return dest;
   }
  }
  public static Bitmap Get(AppEntry app) {
   string key=(app.IconPath??"")+"|"+app.Target+"|"+app.Name;
   Bitmap result;if(cache.TryGetValue(key,out result))return result;
   try {if(!String.IsNullOrWhiteSpace(app.IconPath)&&File.Exists(app.IconPath)) {using(Image i=Image.FromFile(app.IconPath))result=new Bitmap(i);}}
   catch {result=null;}
   if(result==null&&String.Equals(app.Name,"Codex",StringComparison.OrdinalIgnoreCase)) {
    result=new Bitmap(128,128);using(Graphics g=Graphics.FromImage(result)) {g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);using(Pen p=new Pen(Theme.Text,8)){p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;g.DrawLines(p,new Point[]{new Point(30,40),new Point(51,60),new Point(30,80)});g.DrawLine(p,63,82,94,82);}}
   }
   if(result==null) {
    try {string path=Environment.ExpandEnvironmentVariables(app.Target);if(!File.Exists(path)&&!Path.IsPathRooted(path)) {string p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),path);path=File.Exists(p)?p:Path.Combine(Environment.SystemDirectory,path);}if(File.Exists(path))using(Icon icon=Icon.ExtractAssociatedIcon(path))if(icon!=null)result=icon.ToBitmap();}catch {}
   }
   if(result==null)result=SystemIcons.Application.ToBitmap();cache[key]=result;return result;
  }
  public static void SelfTest() {
   Directory.CreateDirectory(Storage.Folder);string source=Path.Combine(Storage.Folder,"source.svg");
   File.WriteAllText(source,"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 50'><defs><linearGradient id='g'><stop stop-color='#9bbcff'/><stop offset='1' stop-color='#52cfb0'/></linearGradient></defs><rect x='5' y='5' width='90' height='40' rx='8' fill='url(#g)'/></svg>");
   string imported=Import(source);File.Delete(source);
   using(Bitmap b=new Bitmap(imported))if(b.Width!=256||b.GetPixel(0,0).A!=0||b.GetPixel(128,128).A==0)throw new Exception("SVG transparency/aspect render failed");
   string png=Path.Combine(Storage.Folder,"source.png");using(Bitmap b=new Bitmap(60,30)){using(Graphics g=Graphics.FromImage(b))g.Clear(Color.Coral);b.Save(png,System.Drawing.Imaging.ImageFormat.Png);}
   string importedPng=Import(png);File.Delete(png);using(Bitmap b=new Bitmap(importedPng))if(b.GetPixel(128,0).A!=0||b.GetPixel(128,128).A==0)throw new Exception("PNG aspect ratio failed");
   Settings s=Storage.Defaults();s.Apps[0].IconPath=imported;Storage.Save(s);if(Storage.Load().Apps[0].IconPath!=imported)throw new Exception("Icon persistence failed");
   string bad=Path.Combine(Storage.Folder,"bad.svg");File.WriteAllText(bad,"not svg");bool rejected=false;try{using(Bitmap b=Decode(bad)){}}catch{rejected=true;}if(!rejected)throw new Exception("Invalid SVG accepted");
  }
 }
}
