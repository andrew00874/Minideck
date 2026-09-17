using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace MiniDeck {
 public class BoardBinding {public int Slot{get;set;} public int Usage{get;set;} public int Modifier{get;set;} public bool Media{get;set;} }
 public class BoardProfile {
  public int Layer{get;set;}
  public List<BoardBinding> Bindings{get;set;}
  public static BoardProfile Default(){return new BoardProfile{Layer=1,Bindings=new List<BoardBinding>{new BoardBinding{Slot=1,Usage=0x3a,Modifier=7},new BoardBinding{Slot=2,Usage=0x3b,Modifier=7},new BoardBinding{Slot=3,Usage=0x3c,Modifier=7},new BoardBinding{Slot=13,Usage=0x3d,Modifier=7},new BoardBinding{Slot=14,Usage=0x3f,Modifier=7},new BoardBinding{Slot=15,Usage=0x3e,Modifier=7}}};}
 }
 static class BoardProtocol {
  public static readonly int[] Slots={1,2,3,13,14,15};
  public static readonly string[] Names={L10n.T("키 1"),L10n.T("키 2"),L10n.T("키 3"),L10n.T("노브 왼쪽"),L10n.T("노브 누르기"),L10n.T("노브 오른쪽")};
  static byte[] Report(params byte[] payload){byte[] frame=new byte[65];frame[0]=3;Array.Copy(payload,0,frame,1,payload.Length);return frame;}
  public static List<byte[]> Encode(int layer,BoardBinding b){
   if(layer<1||layer>3||!Slots.Contains(b.Slot)||b.Usage<1||b.Usage>255||b.Modifier<0||b.Modifier>15||b.Media&&b.Modifier!=0)throw new ArgumentException(L10n.T("장치 매핑 값이 올바르지 않습니다."));
   List<byte[]> frames=new List<byte[]>();frames.Add(Report(0xa1,(byte)layer));
   if(b.Media)frames.Add(Report((byte)b.Slot,(byte)((layer<<4)|2),(byte)b.Usage,0));
   else {frames.Add(Report((byte)b.Slot,(byte)((layer<<4)|1),1,0,(byte)b.Modifier,0));frames.Add(Report((byte)b.Slot,(byte)((layer<<4)|1),1,1,(byte)b.Modifier,(byte)b.Usage));}
   frames.Add(Report(0xaa,0xaa));return frames;
  }
  public static void Validate(BoardProfile p){if(p==null||p.Bindings==null||p.Bindings.Count!=6||!p.Bindings.Select(b=>b.Slot).OrderBy(x=>x).SequenceEqual(Slots.OrderBy(x=>x)))throw new ArgumentException(L10n.T("6개 조작을 모두 설정해야 합니다."));foreach(BoardBinding b in p.Bindings)Encode(p.Layer,b);}
  public static string Preview(BoardProfile p){Validate(p);StringBuilder text=new StringBuilder();foreach(BoardBinding b in p.Bindings){text.AppendLine("Layer "+p.Layer+" / "+Names[Array.IndexOf(Slots,b.Slot)]);foreach(byte[] frame in Encode(p.Layer,b))text.AppendLine(BitConverter.ToString(frame));}return text.ToString();}
  public static void SelfTest(){
   BoardProfile p=BoardProfile.Default();Validate(p);List<byte[]> f=Encode(1,p.Bindings[0]);
   if(f.Count!=4||BitConverter.ToString(f[1],0,7)!="03-01-11-01-00-07-00"||BitConverter.ToString(f[2],0,7)!="03-01-11-01-01-07-3A"||f[3][1]!=0xaa||f[3][2]!=0xaa||f.Any(x=>x.Length!=65||x.Skip(9).Any(v=>v!=0)))throw new Exception("Keyboard protocol mismatch");
   if(p.Bindings.Single(x=>x.Slot==14).Usage!=0x3f||p.Bindings.Single(x=>x.Slot==15).Usage!=0x3e)throw new Exception("Knob slot order mismatch");
   f=Encode(3,new BoardBinding{Slot=15,Usage=0xe9,Media=true});if(f.Count!=3||BitConverter.ToString(f[1],0,5)!="03-0F-32-E9-00")throw new Exception("Media protocol mismatch");
   bool rejected=false;try{Encode(1,new BoardBinding{Slot=99,Usage=1});}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Invalid slot accepted");
  }
 }
 sealed class BoardDevice {
  public string Path;public override string ToString(){return "MINI KeyBoard · 1189:8890";}
  [StructLayout(LayoutKind.Sequential)]struct RawDevice{public IntPtr Handle;public uint Type;}
  [DllImport("user32.dll")]static extern uint GetRawInputDeviceList([Out]RawDevice[] devices,ref uint count,uint size);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint GetRawInputDeviceInfo(IntPtr handle,uint command,StringBuilder name,ref uint count);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
  [DllImport("hid.dll")]static extern bool HidD_GetPreparsedData(SafeFileHandle file,out IntPtr data);
  [DllImport("hid.dll")]static extern bool HidD_FreePreparsedData(IntPtr data);
  [DllImport("hid.dll")]static extern int HidP_GetCaps(IntPtr data,IntPtr caps);
  [DllImport("hid.dll")]static extern int HidP_GetValueCaps(int type,IntPtr caps,ref ushort count,IntPtr data);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteFile(SafeFileHandle file,IntPtr buffer,uint count,IntPtr written,IntPtr overlapped);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetOverlappedResult(SafeFileHandle file,IntPtr overlapped,out uint count,bool wait);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool CancelIoEx(SafeFileHandle file,IntPtr overlapped);
  public static List<BoardDevice> Find(){
   uint count=0,size=(uint)Marshal.SizeOf(typeof(RawDevice));if(GetRawInputDeviceList(null,ref count,size)==uint.MaxValue)throw new Exception(L10n.T("USB 장치 목록을 읽지 못했습니다."));RawDevice[] devices=new RawDevice[count];if(GetRawInputDeviceList(devices,ref count,size)==uint.MaxValue)throw new Exception(L10n.T("장치를 다시 연결한 뒤 검색하세요."));
   List<BoardDevice> found=new List<BoardDevice>();foreach(RawDevice d in devices){uint n=1024;StringBuilder name=new StringBuilder(1024);GetRawInputDeviceInfo(d.Handle,0x20000007,name,ref n);string path=name.ToString();if(path.IndexOf("vid_1189&pid_8890&mi_01#",StringComparison.OrdinalIgnoreCase)>=0&&!found.Any(x=>x.Path==path)){BoardDevice b=new BoardDevice{Path=path};using(SafeFileHandle h=b.Open(false))b.ValidateHandle(h);found.Add(b);}}return found;
  }
  SafeFileHandle Open(bool write){SafeFileHandle h=CreateFile(Path,write?0x40000000u:0,3,IntPtr.Zero,3,write?0x40000000u:0,IntPtr.Zero);if(h.IsInvalid){h.Dispose();throw new Exception(L10n.T("장치를 열지 못했습니다. 연결 및 다른 설정 프로그램을 확인하세요. (")+Marshal.GetLastWin32Error()+")");}return h;}
  void ValidateHandle(SafeFileHandle h){IntPtr pp;if(!HidD_GetPreparsedData(h,out pp))throw new Exception(L10n.T("HID 정보를 읽지 못했습니다."));IntPtr caps=Marshal.AllocHGlobal(64);try{
    if(HidP_GetCaps(pp,caps)<0||(ushort)Marshal.ReadInt16(caps,0)!=1||(ushort)Marshal.ReadInt16(caps,2)!=0xff00||Marshal.ReadInt16(caps,6)!=65)throw new Exception(L10n.T("이 장치의 프로토콜 형식은 지원하지 않습니다."));
    ushort count=(ushort)Marshal.ReadInt16(caps,54);if(count==0||count>128)throw new Exception(L10n.T("출력 보고서 형식을 확인할 수 없습니다."));IntPtr values=Marshal.AllocHGlobal(count*72);try{if(HidP_GetValueCaps(1,values,ref count,pp)<0)throw new Exception(L10n.T("출력 보고서를 읽지 못했습니다."));for(int i=0;i<count;i++)if(Marshal.ReadByte(values,i*72+2)!=3)throw new Exception(L10n.T("Report ID 3 장치만 지원합니다. 설정은 변경하지 않았습니다."));}finally{Marshal.FreeHGlobal(values);}
   }finally{Marshal.FreeHGlobal(caps);HidD_FreePreparsedData(pp);}}
  static void Write(SafeFileHandle h,byte[] frame){byte[] bytes=(byte[])frame.Clone();GCHandle pin=GCHandle.Alloc(bytes,GCHandleType.Pinned);int size=IntPtr.Size==8?32:20;IntPtr ov=Marshal.AllocHGlobal(size);
   using(EventWaitHandle ready=new EventWaitHandle(false,EventResetMode.ManualReset))try{
    for(int i=0;i<size;i++)Marshal.WriteByte(ov,i,0);Marshal.WriteIntPtr(ov,IntPtr.Size==8?24:16,ready.SafeWaitHandle.DangerousGetHandle());
    bool sent=WriteFile(h,pin.AddrOfPinnedObject(),65,IntPtr.Zero,ov);int error=sent?0:Marshal.GetLastWin32Error();if(!sent&&error!=997)throw new Exception(L10n.T("USB 쓰기 실패 (")+error+")");
    uint count;if(!sent&&!ready.WaitOne(1500)){CancelIoEx(h,ov);GetOverlappedResult(h,ov,out count,true);throw new Exception(L10n.T("USB 전송 시간이 초과됐습니다."));}
    if(!GetOverlappedResult(h,ov,out count,true)||count!=65)throw new Exception(L10n.T("USB 전송이 완료되지 않았습니다."));
   }finally{Marshal.FreeHGlobal(ov);pin.Free();}}
  public void Download(BoardProfile profile,string journal){
   BoardProtocol.Validate(profile);int completed=0;using(SafeFileHandle h=Open(true)){ValidateHandle(h);try{
    foreach(BoardBinding binding in profile.Bindings){foreach(byte[] frame in BoardProtocol.Encode(profile.Layer,binding)){Write(h,frame);Thread.Sleep(25);}completed++;File.AppendAllText(journal,DateTime.Now.ToString("s")+" slot="+binding.Slot+" sent incl. commit"+Environment.NewLine);}
   }catch(Exception ex){throw new Exception(L10n.T("전송 중단: ")+completed+L10n.T("/6개 조작의 저장 명령까지 전송했습니다. 일부 설정이 바뀌었을 수 있습니다.\n")+ex.Message,ex);}}
  }
 }
 sealed class BoardChoice {
  public string Name;public int Usage;public bool Media;public override string ToString(){return Name;}
  public static List<BoardChoice> All(){List<BoardChoice> r=new List<BoardChoice>();for(int i=0;i<26;i++)r.Add(new BoardChoice{Name=((char)('A'+i)).ToString(),Usage=4+i});for(int i=1;i<=9;i++)r.Add(new BoardChoice{Name=i.ToString(),Usage=29+i});r.Add(new BoardChoice{Name="0",Usage=39});for(int i=1;i<=24;i++)r.Add(new BoardChoice{Name="F"+i,Usage=i<=12?57+i:91+i});
   string[] names={"Enter","Esc","Backspace","Tab","Space","-","=","[","]","\\","#",";","'","`",",",".","/"};for(int i=0;i<names.Length;i++)r.Add(new BoardChoice{Name=names[i],Usage=40+i});
   string[] navigation={"Insert","Home","Page Up","Delete","End","Page Down","Right","Left","Down","Up"};for(int i=0;i<navigation.Length;i++)r.Add(new BoardChoice{Name=navigation[i],Usage=73+i});
   string[] media={L10n.T("재생 / 일시정지"),L10n.T("이전 곡"),L10n.T("다음 곡"),L10n.T("음소거"),L10n.T("볼륨 +"),L10n.T("볼륨 -")};int[] codes={0xcd,0xb6,0xb5,0xe2,0xe9,0xea};for(int i=0;i<codes.Length;i++)r.Add(new BoardChoice{Name=media[i],Usage=codes[i],Media=true});return r;
  }
 }
 class OnboardForm:Form {
  ComboBox[] choice=new ComboBox[6];CheckBox[,] mods=new CheckBox[6,4];ComboBox device=new ComboBox();NumericUpDown layer=new NumericUpDown();Label state=new Label();Button download;bool writing;
  string ProfilePath{get{return System.IO.Path.Combine(Storage.Folder,"onboard-draft.json");}}
  public OnboardForm(){Text=L10n.T("MiniDeck · 키보드 온보드 설정");Icon=Brand.Icon;Font=Typography.Create(10);ClientSize=new Size(780,670);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
   Controls.Add(new Label{Text=L10n.T("키보드에 저장"),Font=Typography.Create(21,true),Left=24,Top=18,Width=600,Height=42});
   Controls.Add(new Label{Text=L10n.T("키보드가 직접 내보낼 입력을 설정합니다. 앱 실행 목록은 PC에 저장됩니다."),Left=26,Top=66,Width=730});
   device.SetBounds(24,105,510,30);device.DropDownStyle=ComboBoxStyle.DropDownList;Controls.Add(device);AddButton(L10n.T("장치 다시 검색"),548,101,184,delegate{Discover();});
   Controls.Add(new Label{Text=L10n.T("저장할 레이어"),Left=24,Top=157,Width=130});layer.SetBounds(164,153,65,30);layer.Minimum=1;layer.Maximum=3;Controls.Add(layer);
   Controls.Add(new Label{Text=L10n.T("기존 값을 읽어온 화면이 아닙니다. 선택한 레이어의 6개 매핑을 덮어씁니다."),Left=24,Top=194,Width=730,Height=34});
   for(int i=0;i<6;i++){int n=i,y=234+i*44;Controls.Add(new Label{Text=BoardProtocol.Names[i],Left=24,Top=y+5,Width=114});choice[i]=new ComboBox{Left=142,Top=y,Width=244,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName=BoardProtocol.Names[i]+L10n.T(" 입력")};foreach(BoardChoice c in BoardChoice.All())choice[i].Items.Add(c);Controls.Add(choice[i]);string[] labels={"Ctrl","Shift","Alt","Win"};for(int j=0;j<4;j++){mods[i,j]=new CheckBox{Text=labels[j],Left=408+j*82,Top=y+3,Width=80,AccessibleName=BoardProtocol.Names[i]+" "+labels[j]};Controls.Add(mods[i,j]);}choice[i].SelectedIndexChanged+=delegate{BoardChoice selected=choice[n].SelectedItem as BoardChoice;for(int j=0;j<4;j++)mods[n,j].Enabled=selected!=null&&!selected.Media;};}
   AddButton(L10n.T("MiniDeck 연동값"),24,508,165,delegate{LoadProfile(BoardProfile.Default());state.Text=L10n.T("Ctrl+Alt+Shift+F1~F6 초안입니다. 아직 장치에 쓰지 않았습니다.");});
   AddButton(L10n.T("초안 저장"),201,508,130,delegate{try{SaveDraft(Collect());state.Text=L10n.T("PC에 초안만 저장했습니다. 장치는 변경하지 않았습니다.");}catch(Exception ex){state.Text=ex.Message;}});
   AddButton(L10n.T("전송 내용 보기"),343,508,160,delegate{try{using(Form f=new Form{Text=L10n.T("HID 전송 미리보기 · 장치 쓰기 없음"),Width=760,Height=500,StartPosition=FormStartPosition.CenterParent}){f.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both,WordWrap=false,Text=BoardProtocol.Preview(Collect())});f.ShowDialog(this);}}catch(Exception ex){state.Text=ex.Message;}});
   download=AddButton(L10n.T("온보드에 저장"),535,508,197,delegate{Download();});
   state.SetBounds(24,562,730,86);Controls.Add(state);Theme.Apply(this);download.BackColor=Theme.Accent;download.ForeColor=Theme.Background;
   BoardProfile p=BoardProfile.Default();if(File.Exists(ProfilePath))try{p=new JavaScriptSerializer().Deserialize<BoardProfile>(File.ReadAllText(ProfilePath));BoardProtocol.Validate(p);}catch{p=BoardProfile.Default();state.Text=L10n.T("초안을 읽지 못해 연동 기본값을 표시합니다. 기존 파일은 유지됩니다.");}LoadProfile(p);Discover();
  }
  Button AddButton(string text,int x,int y,int width,Action action){Button b=new Button{Text=text,Left=x,Top=y,Width=width,Height=38};b.Click+=delegate{action();};Controls.Add(b);return b;}
  void Discover(){try{device.Items.Clear();foreach(BoardDevice d in BoardDevice.Find())device.Items.Add(d);if(device.Items.Count>0)device.SelectedIndex=0;download.Enabled=device.Items.Count==1;state.Text=device.Items.Count==1?L10n.T("장치 확인: 1189:8890 · Report ID 3\n앱 실행·다이얼 연동은 ‘MiniDeck 연동값’을 사용하세요.\n임의의 키·미디어 설정은 키보드 기능으로 저장되며 런처에 자동 연결되지 않습니다."):device.Items.Count==0?L10n.T("지원하는 키보드가 연결되어 있지 않습니다."):L10n.T("키보드가 여러 대입니다. 대상 한 대만 연결하세요.");}catch(Exception ex){download.Enabled=false;state.Text=ex.Message;}}
  void LoadProfile(BoardProfile p){layer.Value=p.Layer;for(int i=0;i<6;i++){BoardBinding b=p.Bindings.Single(x=>x.Slot==BoardProtocol.Slots[i]);int index=-1;for(int j=0;j<choice[i].Items.Count;j++){BoardChoice c=(BoardChoice)choice[i].Items[j];if(c.Usage==b.Usage&&c.Media==b.Media){index=j;break;}}if(index<0){choice[i].Items.Add(new BoardChoice{Name="HID "+b.Usage,Usage=b.Usage,Media=b.Media});index=choice[i].Items.Count-1;}choice[i].SelectedIndex=index;for(int j=0;j<4;j++)mods[i,j].Checked=(b.Modifier&(1<<j))!=0;}}
  BoardProfile Collect(){BoardProfile p=new BoardProfile{Layer=(int)layer.Value,Bindings=new List<BoardBinding>()};for(int i=0;i<6;i++){BoardChoice c=choice[i].SelectedItem as BoardChoice;if(c==null)throw new Exception(L10n.T("각 조작의 키를 선택하세요."));int modifier=0;if(!c.Media)for(int j=0;j<4;j++)if(mods[i,j].Checked)modifier|=1<<j;p.Bindings.Add(new BoardBinding{Slot=BoardProtocol.Slots[i],Usage=c.Usage,Media=c.Media,Modifier=modifier});}BoardProtocol.Validate(p);return p;}
  void SaveDraft(BoardProfile p){Directory.CreateDirectory(Storage.Folder);string tmp=ProfilePath+".tmp";File.WriteAllText(tmp,new JavaScriptSerializer().Serialize(p));if(File.Exists(ProfilePath))File.Replace(tmp,ProfilePath,ProfilePath+".bak");else File.Move(tmp,ProfilePath);}
  async void Download(){try{BoardProfile p=Collect();BoardDevice d=device.SelectedItem as BoardDevice;if(d==null)throw new Exception(L10n.T("키보드를 먼저 연결하세요."));SaveDraft(p);string journal=System.IO.Path.Combine(Storage.Folder,"onboard-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".log");File.WriteAllText(journal,"Planned mapping (not a device backup):\n"+new JavaScriptSerializer().Serialize(p)+"\n"+BoardProtocol.Preview(p));writing=true;foreach(Control c in Controls)c.Enabled=false;state.Enabled=true;state.Text=L10n.T("장치에 저장 명령을 전송하고 있습니다…");await Task.Run(delegate{d.Download(p,journal);});state.Text=L10n.T("6개 조작의 저장 명령 전송 완료. 분리·재연결 후 실제 동작을 확인하세요.\n장치 설정 읽기/검증 명령은 확인되지 않아 저장값을 재조회하지는 못합니다.");}catch(Exception ex){state.Text=ex.Message;}finally{writing=false;foreach(Control c in Controls)c.Enabled=true;for(int i=0;i<6;i++){BoardChoice c=choice[i].SelectedItem as BoardChoice;for(int j=0;j<4;j++)mods[i,j].Enabled=c!=null&&!c.Media;}}}
  protected override void OnFormClosing(FormClosingEventArgs e){if(writing){e.Cancel=true;return;}base.OnFormClosing(e);}
 }
}
