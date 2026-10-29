using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiniDeck {
 class TranslationSettingsForm : Form {
  public LocalTranslationSettings Result;
  readonly CheckBox enabled=new CheckBox();
  readonly ComboBox provider=new ComboBox(),model=new ComboBox(),language=new ComboBox();
  readonly TextBox address=new TextBox();
  readonly Label message=new Label();
  readonly Button refresh=new Button(),save=new Button();
  CancellationTokenSource loading;
  public TranslationSettingsForm(LocalTranslationSettings current){
   LocalTranslationSettings settings=(current??new LocalTranslationSettings()).Clone();
   Text=L10n.T("로컬 번역 설정");Icon=Brand.Icon;Font=Typography.Create(10);
   ClientSize=new Size(660,550);StartPosition=FormStartPosition.CenterParent;
   FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
   enabled.Text=L10n.T("3번 키로 바로 로컬 번역");enabled.SetBounds(24,22,610,30);enabled.Checked=settings.Enabled;
   Controls.Add(enabled);
   Controls.Add(new Label{Text=L10n.T("체크를 끄면 기존 검색·번역 다이얼을 사용합니다."),Left=24,Top=60,Width=610,Height=26});
   AddLabel(L10n.T("모델 실행 프로그램"),100);
   provider.SetBounds(24,128,610,30);provider.DropDownStyle=ComboBoxStyle.DropDownList;provider.AccessibleName=L10n.T("모델 실행 프로그램");
   provider.Items.AddRange(new object[]{"Ollama","OpenAI API (LM Studio / llama.cpp)"});Controls.Add(provider);
   AddLabel(L10n.T("로컬 서버 주소"),174);
   address.SetBounds(24,202,610,28);address.Text=settings.BaseUrl;address.AccessibleName=L10n.T("로컬 서버 주소");Controls.Add(address);
   AddLabel(L10n.T("모델 이름"),246);
   model.SetBounds(24,274,428,30);model.DropDownStyle=ComboBoxStyle.DropDown;model.Text=settings.Model;model.AccessibleName=L10n.T("모델 이름");Controls.Add(model);
   refresh.Text=L10n.T("모델 불러오기");refresh.SetBounds(464,271,170,34);Controls.Add(refresh);
   AddLabel(L10n.T("번역 언어 (ko / en / ja ...)"),322);
   language.SetBounds(24,350,180,30);language.DropDownStyle=ComboBoxStyle.DropDown;language.AccessibleName=L10n.T("번역 언어 (ko / en / ja ...)");
   language.Items.AddRange(new object[]{"ko","en","ja","zh","fr","de","es"});language.Text=settings.TargetLanguage;Controls.Add(language);
   Controls.Add(new Label{Text=L10n.T("Ollama 또는 LM Studio에서 서버를 실행하고 설치된 모델을 선택하세요.\n서버 주소는 이 PC의 localhost 또는 루프백 IP만 사용할 수 있습니다."),Left=24,Top=396,Width=610,Height=48});
   message.SetBounds(24,448,610,44);Controls.Add(message);
   save.Text=L10n.T("저장");save.SetBounds(424,502,100,36);
   Button cancel=new Button{Text=L10n.T("취소"),Left=534,Top=502,Width=100,Height=36,DialogResult=DialogResult.Cancel};
   Controls.AddRange(new Control[]{save,cancel});AcceptButton=save;CancelButton=cancel;
   provider.SelectedIndex=settings.Provider=="OpenAI"?1:0;
   provider.SelectedIndexChanged+=delegate{
    if(String.IsNullOrWhiteSpace(address.Text)||address.Text=="http://127.0.0.1:11434"||address.Text=="http://127.0.0.1:1234")address.Text=provider.SelectedIndex==1?"http://127.0.0.1:1234":"http://127.0.0.1:11434";
    model.Items.Clear();model.Text="";message.Text="";
   };
   refresh.Click+=async delegate{await LoadModels();};
   save.Click+=delegate{
    try{LocalTranslationSettings next=ReadSettings();LocalTranslationClient.Validate(next,next.Enabled);Result=next;DialogResult=DialogResult.OK;}
    catch(Exception ex){ShowMessage(ex.Message,true);}
   };
   Theme.Apply(this);
  }
  void AddLabel(string text,int top){Controls.Add(new Label{Text=text,Left=24,Top=top,Width=610,Height=24});}
  LocalTranslationSettings ReadSettings(){return new LocalTranslationSettings{Enabled=enabled.Checked,Provider=provider.SelectedIndex==1?"OpenAI":"Ollama",BaseUrl=address.Text.Trim(),Model=model.Text.Trim(),TargetLanguage=language.Text.Trim()};}
  void ShowMessage(string text,bool error){message.Text=text;message.ForeColor=error?Color.FromArgb(255,166,166):Theme.Muted;}
  async Task LoadModels(){
   CancellationTokenSource request=new CancellationTokenSource();loading=request;
   refresh.Enabled=false;save.Enabled=false;provider.Enabled=false;address.Enabled=false;model.Enabled=false;
   ShowMessage(L10n.T("로컬 모델 목록을 불러오는 중…"),false);
   try{
    LocalTranslationSettings settings=ReadSettings();LocalTranslationClient.Validate(settings,false);
    List<string> found=await LocalTranslationClient.ListModelsAsync(settings,request.Token);
    if(IsDisposed||request.IsCancellationRequested)return;
    string selected=model.Text;model.Items.Clear();foreach(string item in found)model.Items.Add(item);
    model.Text=selected;if(String.IsNullOrWhiteSpace(selected)&&found.Count>0)model.SelectedIndex=0;
    ShowMessage(found.Count==0?L10n.T("설치된 모델이 없습니다. 모델 실행 프로그램에서 먼저 모델을 내려받으세요."):String.Format(L10n.T("사용 가능한 모델 {0}개를 찾았습니다."),found.Count),found.Count==0);
   }catch(OperationCanceledException){}
   catch(Exception ex){if(!IsDisposed&&!request.IsCancellationRequested)ShowMessage(ex.Message,true);}
   finally{
    if(loading==request)loading=null;request.Dispose();
    if(!IsDisposed){refresh.Enabled=true;save.Enabled=true;provider.Enabled=true;address.Enabled=true;model.Enabled=true;}
   }
  }
  protected override void OnFormClosing(FormClosingEventArgs e){if(loading!=null)loading.Cancel();base.OnFormClosing(e);}
 }

 class TranslationResultForm : Form {
  readonly TextBox source=new TextBox(),result=new TextBox();
  readonly Label status=new Label();
  readonly Button copy=new Button(),retry=new Button(),stop=new Button();
  readonly Action openSettings;
  CancellationTokenSource active;
  LocalTranslationSettings lastSettings;
  string selectedText;
  int version;
  bool completed;
  public bool IsTranslating {get;private set;}
  public bool HasSelection {get{return !String.IsNullOrWhiteSpace(selectedText);}}
  internal string ResultText {get{return result.Text;}}
  internal string SourceText {get{return source.Text;}}
  internal string StatusText {get{return status.Text;}}
  internal bool CanCopy {get{return copy.Enabled;}}
  public TranslationResultForm(Action settingsAction){
   openSettings=settingsAction;Text=L10n.T("MiniDeck · 로컬 번역");Icon=Brand.Icon;Font=Typography.Create(10);
   ClientSize=new Size(650,500);MinimumSize=new Size(666,539);StartPosition=FormStartPosition.Manual;
   status.SetBounds(22,18,606,48);status.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(status);
   Controls.Add(new Label{Text=L10n.T("선택한 글"),Left=22,Top=76,Width=606,Height=24});
   ConfigureText(source);source.AccessibleName=L10n.T("선택한 글");source.SetBounds(22,102,606,106);source.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(source);
   Controls.Add(new Label{Text=L10n.T("번역 결과"),Left=22,Top=222,Width=606,Height=24});
   ConfigureText(result);result.AccessibleName=L10n.T("번역 결과");result.SetBounds(22,248,606,184);result.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(result);
   copy.Text=L10n.T("결과 복사");copy.SetBounds(22,450,100,36);
   retry.Text=L10n.T("다시 번역");retry.SetBounds(132,450,100,36);
   stop.Text=L10n.T("취소");stop.SetBounds(242,450,86,36);
   Button settings=new Button{Text=L10n.T("로컬 번역 설정"),Left=338,Top=450,Width=160,Height=36};
   Button close=new Button{Text=L10n.T("닫기"),Left=508,Top=450,Width=120,Height=36};
   foreach(Button button in new[]{copy,retry,stop,settings,close}){button.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;Controls.Add(button);}
   close.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
   close.Click+=delegate{Close();};
   retry.Click+=delegate{if(HasSelection&&lastSettings!=null)BeginTranslation(selectedText,lastSettings);};
   stop.Click+=delegate{CancelTranslation();};
   settings.Click+=delegate{
    try{if(openSettings!=null)openSettings();}
    catch(Exception ex){SetStatus(ex.Message,true);}
   };
   copy.Click+=delegate{
    try{if(!String.IsNullOrWhiteSpace(result.Text)){Clipboard.SetText(result.Text);SetStatus(L10n.T("번역 결과를 복사했습니다."),false);}}
    catch(Exception){SetStatus(L10n.T("클립보드에 복사하지 못했습니다. 잠시 후 다시 시도해 주세요."),true);}
   };
   Theme.Apply(this);SetBusy(false);copy.Enabled=false;retry.Enabled=false;
  }
  static void ConfigureText(TextBox box){box.Multiline=true;box.ReadOnly=true;box.MaxLength=1024*1024;box.ScrollBars=ScrollBars.Vertical;box.WordWrap=true;box.TabStop=true;box.ShortcutsEnabled=true;}
  void SetStatus(string text,bool error){status.Text=text;status.ForeColor=error?Color.FromArgb(255,166,166):Theme.Muted;}
  sealed class WindowProgress:IProgress<string> {
   readonly Action<string> report;public WindowProgress(Action<string> action){report=action;}
   public void Report(string value){report(value);}
  }
  void QueueUpdate(int request,Action update){
   if(IsDisposed||Disposing||!IsHandleCreated)return;
   if(InvokeRequired){
    try{BeginInvoke((Action)delegate{QueueUpdate(request,update);});}
    catch(InvalidOperationException){/* A closing result window drops pending previews. */}
    return;
   }
   if(request!=version||!IsTranslating)return;
   try{update();}
   catch(Exception error){
    AppDiagnostics.Exception("translation-display",error);
    try{CancelTranslation();completed=false;result.Clear();SetStatus(L10n.T("결과를 표시하지 못했습니다. 다시 번역을 눌러 주세요."),true);}catch(Exception nested){AppDiagnostics.Exception("translation-display-recovery",nested);}
   }
  }
  void SetBusy(bool busy){IsTranslating=busy;copy.Enabled=!busy&&completed&&!String.IsNullOrWhiteSpace(result.Text);retry.Enabled=!busy&&HasSelection;stop.Enabled=busy;}
  public void BeginTranslation(string text,LocalTranslationSettings settings){
   if(IsDisposed)return;
   // Standalone/test hosts may not have entered Application.Run yet.
   // Preview callbacks and the final continuation must share the UI queue.
   if(!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
   CancelActive();int request=version;
   completed=false;selectedText=text;lastSettings=(settings??new LocalTranslationSettings()).Clone();source.Text=text??"";result.Clear();
   if(!Visible){
    Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;Point cursor=Cursor.Position;
    Location=new Point(Math.Max(area.Left,Math.Min(cursor.X+20,area.Right-Width)),Math.Max(area.Top,Math.Min(cursor.Y+20,area.Bottom-Height)));
    Show();
   }
   if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
   Activate();SetBusy(true);SetStatus(L10n.T("번역 중… 처음에는 모델을 불러오는 데 시간이 걸릴 수 있습니다."),false);
   CancellationTokenSource cancellation=new CancellationTokenSource();active=cancellation;
   RunTranslation(text,lastSettings,request,cancellation);
  }
  // This method contains every asynchronous failure; a closed or newer result never receives stale output.
  async void RunTranslation(string text,LocalTranslationSettings settings,int request,CancellationTokenSource cancellation){
   try{
    var progress=new WindowProgress(value=>QueueUpdate(request,()=>SetStatus(value,false)));
    var preview=new WindowProgress(value=>QueueUpdate(request,()=>{result.Text=value;}));
    string translated=await LocalTranslationClient.TranslateAsync(settings,text,cancellation.Token,progress,preview);
    if(IsDisposed||request!=version||cancellation.IsCancellationRequested)return;
    result.Text=translated;completed=true;SetStatus(L10n.T("번역 완료 · Esc로 닫기"),false);
   }catch(OperationCanceledException){if(!IsDisposed&&request==version)SetStatus(L10n.T("번역을 취소했습니다."),false);}
   catch(Exception ex){if(!IsDisposed&&request==version){result.Clear();SetStatus(ex.Message,true);}}
   finally{
    if(!IsDisposed&&request==version){active=null;SetBusy(false);}
    cancellation.Dispose();
   }
  }
  void CancelActive(){version++;CancellationTokenSource request=active;active=null;if(request!=null)request.Cancel();}
  public void CancelTranslation(){if(active==null)return;CancelActive();if(!IsDisposed){SetBusy(false);SetStatus(L10n.T("번역을 취소했습니다."),false);}}
  public void RetryWithSettings(LocalTranslationSettings settings){if(HasSelection)BeginTranslation(selectedText,settings);}
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==Keys.Escape){Close();return true;}return base.ProcessCmdKey(ref msg,keyData);}
  protected override void OnFormClosing(FormClosingEventArgs e){
   CancelActive();selectedText=null;lastSettings=null;source.Clear();result.Clear();base.OnFormClosing(e);
  }
 }
}
