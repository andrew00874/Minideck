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
  readonly Label message=new Label(),addressLabel=new Label(),modelLabel=new Label(),help=new Label(),languageLabel=new Label();
  readonly Button refresh=new Button(),save=new Button();
  readonly Button cancel=new Button();
  readonly LocalTranslationSettings installed;
  readonly bool installedAvailable;
  readonly Dictionary<int,string> addresses=new Dictionary<int,string>(),models=new Dictionary<int,string>();
  int selectedProvider;
  CancellationTokenSource loading;
  public TranslationSettingsForm(LocalTranslationSettings current){
   LocalTranslationSettings settings=(current??new LocalTranslationSettings()).Clone();
   installed=settings.Clone();installedAvailable=LocalModelRuntime.ApplyInstalledDefault(installed);
   Text=L10n.T("로컬 번역 설정");Icon=Brand.Icon;Font=Typography.Create(12);AutoScaleMode=AutoScaleMode.None;
   ClientSize=new Size(760,650);StartPosition=FormStartPosition.CenterParent;
   FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
   enabled.Text=L10n.T("3번 키로 바로 로컬 번역");enabled.SetBounds(24,22,712,34);enabled.Checked=settings.Enabled;
   Controls.Add(enabled);
   Controls.Add(new Label{Text=L10n.T("체크를 끄면 기존 검색·번역 다이얼을 사용합니다."),Left=24,Top=64,Width=712,Height=30});
   AddLabel(L10n.T("번역 엔진"),110);
   provider.SetBounds(24,138,712,34);provider.DropDownStyle=ComboBoxStyle.DropDownList;provider.AccessibleName=L10n.T("번역 엔진");
   provider.Items.AddRange(new object[]{L10n.T("MiniDeck 내장 번역"),"Ollama","LM Studio / llama.cpp"});Controls.Add(provider);
   addressLabel.Text=L10n.T("로컬 서버 주소");addressLabel.SetBounds(24,190,712,28);
   address.SetBounds(24,218,712,32);address.AccessibleName=L10n.T("로컬 서버 주소");
   modelLabel.Text=L10n.T("모델 이름");modelLabel.SetBounds(24,266,712,28);
   model.SetBounds(24,294,498,34);model.DropDownStyle=ComboBoxStyle.DropDown;model.AccessibleName=L10n.T("모델 이름");
   refresh.SetBounds(534,291,202,42);
   languageLabel.Text=L10n.T("번역 언어 (ko / en / ja ...)");languageLabel.SetBounds(24,428,712,28);
   language.SetBounds(24,456,220,34);language.DropDownStyle=ComboBoxStyle.DropDown;language.AccessibleName=languageLabel.Text;
   language.Items.AddRange(new object[]{"ko","en","ja","zh","fr","de","es"});language.Text=settings.TargetLanguage;Controls.Add(language);
   help.SetBounds(24,350,712,66);message.SetBounds(24,506,712,58);
   save.Text=L10n.T("저장");save.SetBounds(504,578,110,44);
   cancel.Text=L10n.T("취소");cancel.SetBounds(626,578,110,44);cancel.DialogResult=DialogResult.Cancel;
   Controls.AddRange(new Control[]{addressLabel,address,modelLabel,model,refresh,help,languageLabel,message,save,cancel});AcceptButton=save;CancelButton=cancel;
   bool managed=settings.Provider=="MiniDeck"||(installedAvailable&&settings.Provider=="OpenAI"&&String.Equals((settings.BaseUrl??"").TrimEnd('/'),installed.BaseUrl.TrimEnd('/'),StringComparison.OrdinalIgnoreCase));
   selectedProvider=managed?0:settings.Provider=="OpenAI"?2:1;
   if(selectedProvider>0){addresses[selectedProvider]=settings.BaseUrl;models[selectedProvider]=settings.Model;}
   provider.SelectedIndex=selectedProvider;UpdateEngineView();
   provider.SelectedIndexChanged+=delegate{
    if(selectedProvider>0){addresses[selectedProvider]=address.Text;models[selectedProvider]=model.Text;}
    selectedProvider=provider.SelectedIndex;model.Items.Clear();message.Text="";UpdateEngineView();
   };
   refresh.Click+=async delegate{await LoadModels();};
   save.Click+=delegate{
    try{LocalTranslationSettings next=ReadSettings();LocalTranslationClient.Validate(next,next.Enabled);Result=next;DialogResult=DialogResult.OK;}
    catch(Exception ex){ShowMessage(ex.Message,true);}
   };
   Theme.Apply(this);
  }
  void AddLabel(string text,int top){Controls.Add(new Label{Text=text,Left=24,Top=top,Width=712,Height=28});}
  void UpdateEngineView(){
   bool managed=provider.SelectedIndex==0;int offset=managed?76:0;
   addressLabel.Visible=address.Visible=!managed;
   modelLabel.Top=266-offset;model.Top=294-offset;refresh.Top=291-offset;
   help.Top=350-offset;languageLabel.Top=428-offset;language.Top=456-offset;message.Top=506-offset;
   save.Top=cancel.Top=578-offset;ClientSize=new Size(760,650-offset);
   refresh.Text=managed?L10n.T("엔진 확인"):L10n.T("모델 불러오기");refresh.AccessibleName=refresh.Text;
   string value;
   address.Text=managed?(installedAvailable?installed.BaseUrl:""):(addresses.TryGetValue(selectedProvider,out value)?value:selectedProvider==1?"http://127.0.0.1:11434":"http://127.0.0.1:1234");
   model.Text=managed?(installedAvailable?installed.Model:""):(models.TryGetValue(selectedProvider,out value)?value:"");
   help.Text=managed?(installedAvailable?L10n.T("MiniDeck이 번역 엔진을 자동으로 시작하고 복구합니다.\n별도 프로그램을 실행하거나 서버 주소를 입력할 필요가 없습니다."):L10n.T("내장 번역 모델을 먼저 설치해 주세요. 설치 후 MiniDeck이 자동으로 관리합니다.")):
    L10n.T("Ollama 또는 LM Studio에서 서버를 실행하고 설치된 모델을 선택하세요.\n서버 주소는 이 PC의 localhost 또는 루프백 IP만 사용할 수 있습니다.");
   model.Enabled=!managed&&loading==null;address.Enabled=!managed&&loading==null;
  }
  LocalTranslationSettings ReadSettings(){
   var next=new LocalTranslationSettings{Enabled=enabled.Checked,Provider=provider.SelectedIndex==0?"MiniDeck":provider.SelectedIndex==2?"OpenAI":"Ollama",BaseUrl=address.Text.Trim(),Model=model.Text.Trim(),TargetLanguage=language.Text.Trim()};
   if(provider.SelectedIndex==0&&!LocalModelRuntime.ApplyInstalledDefault(next)){
    if(next.Enabled)throw new InvalidOperationException(L10n.T("번역 모델 설치 파일을 찾지 못했습니다. 로컬 번역 설치를 다시 실행하세요."));
    next.BaseUrl="http://127.0.0.1:17865";next.Model="";
   }
   return next;
  }
  void ShowMessage(string text,bool error){message.Text=text;message.ForeColor=error?Color.FromArgb(255,166,166):Theme.Muted;}
  async Task LoadModels(){
   CancellationTokenSource request=new CancellationTokenSource();loading=request;
   refresh.Enabled=false;save.Enabled=false;provider.Enabled=false;address.Enabled=false;model.Enabled=false;
   ShowMessage(provider.SelectedIndex==0?L10n.T("내장 번역 엔진을 준비하는 중… 자동으로 시작합니다."):L10n.T("로컬 모델 목록을 불러오는 중…"),false);
   try{
    LocalTranslationSettings settings=ReadSettings();LocalTranslationClient.Validate(settings,false);
    List<string> found=await LocalTranslationClient.ListModelsAsync(settings,request.Token);
    if(IsDisposed||request.IsCancellationRequested)return;
    string selected=model.Text;model.Items.Clear();foreach(string item in found)model.Items.Add(item);
    model.Text=selected;if(String.IsNullOrWhiteSpace(selected)&&found.Count>0)model.SelectedIndex=0;
    ShowMessage(found.Count==0?L10n.T("설치된 모델이 없습니다. 모델 실행 프로그램에서 먼저 모델을 내려받으세요."):provider.SelectedIndex==0?L10n.T("내장 번역 엔진이 준비됐습니다."):String.Format(L10n.T("사용 가능한 모델 {0}개를 찾았습니다."),found.Count),found.Count==0);
   }catch(OperationCanceledException){}
   catch(Exception ex){if(!IsDisposed&&!request.IsCancellationRequested)ShowMessage(ex.Message,true);}
   finally{
    if(loading==request)loading=null;request.Dispose();
    if(!IsDisposed){refresh.Enabled=true;save.Enabled=true;provider.Enabled=true;address.Enabled=provider.SelectedIndex!=0;model.Enabled=provider.SelectedIndex!=0;}
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
   openSettings=settingsAction;Text=L10n.T("MiniDeck · 로컬 번역");Icon=Brand.Icon;Font=Typography.Create(12);AutoScaleMode=AutoScaleMode.None;
   ClientSize=new Size(780,600);MinimumSize=new Size(796,639);StartPosition=FormStartPosition.Manual;
   status.SetBounds(24,18,732,60);status.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(status);
   Controls.Add(new Label{Text=L10n.T("선택한 글"),Left=24,Top=90,Width=732,Height=28});
   ConfigureText(source);source.Font=Typography.Create(14);source.AccessibleName=L10n.T("선택한 글");source.SetBounds(24,122,732,120);source.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(source);
   Controls.Add(new Label{Text=L10n.T("번역 결과"),Left=24,Top=258,Width=732,Height=28});
   ConfigureText(result);result.Font=Typography.Create(14);result.AccessibleName=L10n.T("번역 결과");result.SetBounds(24,290,732,234);result.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(result);
   copy.Text=L10n.T("결과 복사");copy.SetBounds(24,542,122,44);
   retry.Text=L10n.T("다시 번역");retry.SetBounds(156,542,140,44);
   stop.Text=L10n.T("취소");stop.SetBounds(306,542,86,44);
   Button settings=new Button{Text=L10n.T("로컬 번역 설정"),Left=402,Top=542,Width=204,Height=44};
   Button close=new Button{Text=L10n.T("닫기"),Left=616,Top=542,Width=140,Height=44};
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
  static string DisplayText(string text){return (text??"").Replace("\r\n","\n").Replace("\r","\n").Replace("\n","\r\n");}
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
   completed=false;selectedText=text;lastSettings=(settings??new LocalTranslationSettings()).Clone();source.Text=DisplayText(text);result.Clear();
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
    var preview=new WindowProgress(value=>QueueUpdate(request,()=>{result.Text=DisplayText(value);}));
    string translated=await LocalTranslationClient.TranslateAsync(settings,text,cancellation.Token,progress,preview);
    if(IsDisposed||request!=version||cancellation.IsCancellationRequested)return;
    result.Text=DisplayText(translated);completed=true;SetStatus(L10n.T("번역 완료 · Esc로 닫기"),false);
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
