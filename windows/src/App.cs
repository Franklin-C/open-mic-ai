using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LocalFlow {
public sealed class Settings {
    public string Shortcut="Ctrl + Shift + Space", Style="Keep my wording", Custom="", Vocabulary="";
    public string ActionShortcut="Ctrl + Shift + A",QuickAction="Choose action",TranslationLanguage="English";
    public string RecordingMode="Click to start / stop";
    public bool WindowsNotifications=false;
    public bool PolishReworded=true;
    public int Microphone=-1;
    public int BarX=int.MinValue,BarY=int.MinValue;
    public bool Rewrite=true;
    public bool LaunchAtLogin=false;
    public bool SaveHistory=true;
    public bool SaveInsights=true;
    public List<AppStyle> AppStyles=new List<AppStyle>();
    public bool ShowBar=true,Sounds=false,MutePlayback=false;
    public string HoldMouse="Off",Cleanup="Light";
    public string Speech="tdt-0.6b-v2-q8_0.gguf", Text="Qwen3-4B-Instruct-2507-Q3_K_S.gguf";
    public static Settings Load(string root) {
        try { Settings value=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(Path.Combine(root,"preferences.json")));if(value==null)return new Settings();if(value.AppStyles==null)value.AppStyles=new List<AppStyle>();value.Style=value.Rewrite?WritingStyles.Normalize(value.Style):"Original transcript";if(!value.Rewrite||value.Style=="Original transcript")value.Cleanup="None";if(value.Cleanup!="None"&&value.Cleanup!="Medium")value.Cleanup="Light";value.PolishReworded=true;return value; }
        catch { return new Settings(); }
    }
    public void Save(string root) { File.WriteAllText(Path.Combine(root,"preferences.json"),new JavaScriptSerializer().Serialize(this)); }
}

public sealed class DictationSession {
    public readonly CancellationTokenSource Cancel=new CancellationTokenSource();
    public readonly List<Task<string>> Pending=new List<Task<string>>();
    readonly object pendingGate=new object();
    public void AddPending(Task<string> task){lock(pendingGate)Pending.Add(task);}
    public Task<string>[] PendingSnapshot(){lock(pendingGate)return Pending.ToArray();}
    public readonly Utterances Segments=new Utterances();
    public FocusTarget Target;
    public Microphone Mic;
    public PlaybackMute Playback;
    public bool Stopped;
    public bool PreviewBusy;public long LastPreviewAt;public readonly CancellationTokenSource PreviewCancel;
    public DictationSession(){PreviewCancel=CancellationTokenSource.CreateLinkedTokenSource(Cancel.Token);}
    public string Vocabulary,Style,Custom,SelectedText,Cleanup="Light",RecordingMode="Click to start / stop",ActionName,ActionLanguage;
    public TextRules Rules;
    public bool Rewrite,PolishReworded;
    public string Application="Other";
    public double RecordingSeconds;
    public readonly Stopwatch RecordingTime=new Stopwatch();
    public void Stop() {
        if(Stopped)return;Stopped=true;PreviewCancel.Cancel();RecordingTime.Stop();RecordingSeconds=RecordingTime.Elapsed.TotalSeconds;
        if(Mic!=null){Mic.Dispose();Mic=null;}
        if(Playback!=null){Playback.Dispose();Playback=null;}
        Segments.Finish();
    }
}

public sealed class FlowContext : ApplicationContext {
    readonly string root;
    readonly HistoryStore history;
    readonly PersonalizationStore personalization;
    readonly UsageStore usage;
    readonly HoldMouseInput holdMouse;
    HubWindow hub;
    DictationSession heldSession,keyboardHeld;
    DateTime lastNotification=DateTime.MinValue;string lastNotificationText="";
    Settings settings;
    Models models;
    ComputerSpecs specs;
    readonly ShortcutWindow shortcut=new ShortcutWindow();
    readonly Pill pill=new Pill();
    readonly NotifyIcon tray;
    readonly ToolStripMenuItem status=new ToolStripMenuItem("Loading local models…") {Enabled=false};
    readonly ToolStripMenuItem copyResult=new ToolStripMenuItem("Copy last result") {Enabled=false};
    readonly ToolStripMenuItem copyRaw=new ToolStripMenuItem("Copy last transcript") {Enabled=false};
    readonly SynchronizationContext ui;
    readonly RegisteredWaitHandle reopenWait;
    StreamDeckBridge streamDeck;
    bool streamDeckHolding;
    readonly System.Windows.Forms.Timer level=new System.Windows.Forms.Timer {Interval=80};
    readonly System.Windows.Forms.Timer health=new System.Windows.Forms.Timer {Interval=30000};
    DictationSession session;
    TextActionSession actionSession;
    CancellationTokenSource selectionProbe;
    bool pendingHoldReleased;
    readonly MediaImportController importer=new MediaImportController();
    bool Busy {get{return session!=null||actionSession!=null||selectionProbe!=null||importer.Busy;}}
    string lastResult="",lastRaw="";
    bool exiting,booting;
    public FlowContext(string directory,bool openWindow=false,EventWaitHandle reopen=null) {
        root=directory;settings=Settings.Load(root);history=new HistoryStore(root);personalization=new PersonalizationStore(root);usage=new UsageStore(root);
        ui=SynchronizationContext.Current??new WindowsFormsSynchronizationContext();HistoryEntry recent=history.Snapshot().FirstOrDefault();if(recent!=null)pill.RememberResult(!String.IsNullOrEmpty(recent.Result)?recent.Result:recent.VoiceEdit?recent.SelectedText:recent.Transcript);
        importer.CanStart=()=>!exiting&&!Busy&&!booting&&models!=null&&models.Alive;
        importer.Preferences=()=>new JavaScriptSerializer().Deserialize<Settings>(new JavaScriptSerializer().Serialize(settings));importer.Rules=()=>personalization.Snapshot();
        importer.Transcribe=(pcm,vocabulary,token)=>models.Transcribe(pcm,vocabulary,token);importer.Rewrite=(text,prefs,vocabulary,token)=>models.Rewrite(text,prefs.Style,prefs.Custom,token,vocabulary,prefs.Cleanup,prefs.PolishReworded);
        importer.Save=(source,result,mode)=>{if(exiting)return;history.Add(source,result,mode);lastRaw=source;lastResult=result;pill.RememberResult(String.IsNullOrEmpty(result)?source:result);copyResult.Enabled=copyRaw.Enabled=true;};
        holdMouse=new HoldMouseInput(ui);holdMouse.CanStart=()=>!exiting&&!importer.Busy&&!ShortcutWindow.Capturing&&actionSession==null&&(session==null||!session.Stopped)&&models!=null&&models.Alive;
        holdMouse.Down=()=>{if(Hotkey.Mouse(settings.Shortcut)){Toggle();if(settings.RecordingMode=="Hold to talk")heldSession=session;}else if(session==null&&CanDictate()){Begin(false);heldSession=session;}};holdMouse.Up=()=>{if(selectionProbe!=null)pendingHoldReleased=true;DictationSession current=heldSession;heldSession=null;if(current!=null&&session==current&&!current.Stopped)Finish(current);};
        shortcut.Register(settings.Shortcut);settings.Shortcut=shortcut.Label??settings.Shortcut;
        shortcut.Toggle=KeyboardToggle;shortcut.Released=KeyboardRelease;shortcut.Cancel=Cancel;pill.Cancel=Cancel;pill.CopyResult=Copy;pill.Release=()=>{if(selectionProbe!=null)pendingHoldReleased=true;if(session!=null&&!session.Stopped)Finish(session);};pill.RecordingMode=RecordingModes.Normalize(settings.RecordingMode);
        pill.TextAction=ChooseTextAction;pill.SpeakInstruction=SpeakInstruction;pill.TranslationLanguage=language=>{if(actionSession!=null&&!actionSession.Processing)actionSession.Language=language;};
        if(settings.BarX!=int.MinValue&&settings.BarY!=int.MinValue)pill.SetPosition(new Point(settings.BarX,settings.BarY));pill.PositionChanged=point=>{settings.BarX=point.X;settings.BarY=point.Y;SavePreferences();};pill.Persistent=settings.ShowBar;pill.Start=Toggle;pill.History=ShowApp;pill.HideBar=()=>{settings.ShowBar=false;pill.Persistent=false;pill.Conceal();SavePreferences();};
        ContextMenuStrip menu=new ContextMenuStrip();
        menu.Items.Add(status);menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open app",null,(s,e)=>ShowApp());
        copyResult.Click+=(s,e)=>Copy(lastResult);copyRaw.Click+=(s,e)=>Copy(lastRaw);
        menu.Items.Add(copyResult);menu.Items.Add(copyRaw);
        menu.Items.Add("History...",null,(s,e)=>OpenHistory());
        menu.Items.Add("Vocabulary...",null,(s,e)=>OpenHub("Vocabulary"));
        menu.Items.Add("Snippets...",null,(s,e)=>OpenHub("Snippets"));
        menu.Items.Add("Style / Cleanup...",null,(s,e)=>OpenHub("Style"));
        ToolStripMenuItem bar=new ToolStripMenuItem("Show dictation bar");bar.Click+=(s,e)=>{settings.ShowBar=!settings.ShowBar;pill.Persistent=settings.ShowBar;if(session==null){if(settings.ShowBar)pill.ShowIdle();else pill.Conceal();}SavePreferences();};menu.Items.Add(bar);menu.Opening+=(s,e)=>bar.Checked=settings.ShowBar;
        menu.Items.Add("Settings…",null,(s,e)=>OpenSettings());
        menu.Items.Add("Models / Download...",null,(s,e)=>OpenModels());
        menu.Items.Add("Reload and warm up models",null,(s,e)=>{if(!Busy&&!booting)Boot();});
        ToolStripMenuItem modes=new ToolStripMenuItem("Writing mode");menu.Items.Add(modes);
        foreach(string name in StyleOptions.Names){string mode=name;ToolStripMenuItem item=new ToolStripMenuItem(mode);item.Click+=(s,e)=>{if(mode=="Custom"&&String.IsNullOrWhiteSpace(settings.Custom)){OpenHub("Style");return;}StyleOptions.Apply(settings,mode);SavePreferences();};modes.DropDownItems.Add(item);}
        modes.DropDownOpening+=(s,e)=>{foreach(ToolStripMenuItem item in modes.DropDownItems)item.Checked=item.Text==StyleOptions.Selected(settings);};
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Quit",null,(s,e)=>ExitThread());
        tray=new NotifyIcon {Text="Open Mic AI · loading",Icon=MakeIcon(),ContextMenuStrip=menu,Visible=true};
        tray.DoubleClick+=(s,e)=>ShowApp();
        history.SaveFailed+=message=>ui.Post(s=>{if(!exiting)Notify("Could not save text history: "+message+". Copy last result is still available.");},null);
        if(history.LoadError!=null)ui.Post(s=>Notify("Previous history could not be read: "+history.LoadError),null);
        personalization.Failed+=message=>ui.Post(s=>Notify("Could not save vocabulary or snippets: "+message),null);usage.Failed+=message=>ui.Post(s=>Notify("Could not save usage: "+message),null);
        if(personalization.Error!=null)ui.Post(s=>Notify("Saved personalization could not be read. The original file has been preserved."),null);
        try{holdMouse.Configure(Hotkey.Mouse(settings.Shortcut)?settings.Shortcut:settings.HoldMouse);}catch(Exception error){ui.Post(s=>Notify(error.Message),null);}
        if(settings.ShowBar)pill.ShowIdle();
        level.Tick+=(s,e)=>{if(session!=null&&!session.Stopped){DictationSession current=session;bool speech;double duration,silence;current.Segments.Activity(out speech,out duration,out silence);if(RecordingModes.ShouldStop(current.RecordingMode,Math.Max(duration,current.RecordingTime.Elapsed.TotalSeconds),speech,silence)){Finish(current);return;}pill.Level=current.Segments.Level;pill.Invalidate();Preview(current);}};level.Start();
        health.Tick+=(s,e)=>{if(!exiting&&!booting&&!Busy&&models!=null&&models.Ready&&!models.Alive){SetStatus("Reloading local models");Boot();}};health.Start();
        Microsoft.Win32.SystemEvents.PowerModeChanged+=PowerChanged;
        if(settings.LaunchAtLogin)try{ConfigureStartup();}catch(Exception error){ui.Post(s=>Notify("Could not update startup: "+error.Message),null);}
        if(reopen!=null)reopenWait=ThreadPool.RegisterWaitForSingleObject(reopen,(s,t)=>ui.Post(_=>ShowApp(),null),null,Timeout.Infinite,false);
        try{streamDeck=new StreamDeckBridge(ui,StreamDeckCommand);}catch(Exception error){Debug.WriteLine("Stream Deck unavailable: "+error.Message);}
        Boot();
        if(openWindow)ui.Post(s=>ShowApp(),null);
    }
    static Icon MakeIcon(){return Brand.CreateIcon();}
    object StreamDeckCommand(string action){
        if(exiting)return new {ok=false};
        if(action=="open")ShowApp();
        else if(action=="copy"){if(!Copy(pill.RecentText))return new {ok=false,error="No recent text"};}
        else if(action=="cancel"){streamDeckHolding=false;Cancel();}
        else if(action=="toggle"){if(session!=null||selectionProbe!=null||actionSession!=null)Toggle();else if(CanDictate())Begin(false,null,null,"Click to start / stop");}
        else if(action=="hold.down"){
            if(Busy||booting||models==null||!models.Alive)return new {ok=false,error="Not ready"};
            streamDeckHolding=true;Begin(false,null,null,"Hold to talk");
        }else if(action=="hold.up"&&streamDeckHolding){streamDeckHolding=false;if(selectionProbe!=null)pendingHoldReleased=true;if(session!=null&&!session.Stopped)Finish(session);}
        return new {ok=true,ready=models!=null&&models.Alive&&!booting,phase=session!=null&&!session.Stopped?"recording":Busy?"processing":"idle",hasRecent=!String.IsNullOrWhiteSpace(pill.RecentText),pluginConnected=StreamDeckIntegration.Connected};
    }
    async void Boot(){await BootModels();}
    async Task BootModels() {
        if(exiting||booting||Busy)return;
        booting=true;
        try {
            if(specs==null)specs=await Task.Run(()=>ComputerSpecs.Detect());
            ModelPair pair=ModelLibrary.Find(settings.Speech,settings.Text);
            if(pair==null||!ModelLibrary.Installed(root,pair)) {
                if(models!=null){models.Dispose();models=null;}
                SetStatus("Download models to get started");booting=false;
                ui.Post(s=>OpenModels(),null);return;
            }
            if(models!=null)models.Dispose();models=new Models(root);models.Progress=t=>ui.Post(s=>SetStatus(t),null);
            SetStatus("Loading local models");await models.Boot(settings.Speech,settings.Text);if(exiting)return;
            // Subscribe after warm-up; counters describe the user's AI work.
            models.TokensUsed+=counts=>ui.Post(s=>{if(!exiting&&settings.SaveInsights)usage.RecordAiTokens(counts);},null);
            SetStatus("Ready · "+settings.Shortcut);
        }
        catch(OperationCanceledException){if(!exiting){SetStatus("Model warm-up timed out");Notify("Model warm-up took too long. Use Reload and warm up models in the tray to try again.");}}
        catch(Exception e){if(!exiting){SetStatus("Models unavailable");Notify(e.Message);}}
        finally{booting=false;}
    }
    void PowerChanged(object sender,Microsoft.Win32.PowerModeChangedEventArgs e){if(e.Mode==Microsoft.Win32.PowerModes.Resume)ui.Post(s=>{if(!exiting&&!booting&&!Busy&&(models==null||!models.Alive))Boot();},null);}
    void SetStatus(string text){if(exiting)return;status.Text=text;tray.Text=("Open Mic AI · "+text).Substring(0,Math.Min(63,12+text.Length));pill.Available=models!=null&&models.Alive;if(pill.Idle)pill.Invalidate();if(hub!=null&&!hub.IsDisposed)hub.SetStatus(text);}
    void Notify(string text,bool showIndicator=true){if(exiting)return;tray.BalloonTipText=text;if(showIndicator&&session==null&&actionSession==null&&!importer.Busy)pill.ShowNotice("Open Mic AI",text);if(settings.WindowsNotifications&&text!=lastNotificationText&&(DateTime.UtcNow-lastNotification).TotalSeconds>=60){lastNotificationText=text;lastNotification=DateTime.UtcNow;tray.BalloonTipTitle=Brand.Name;tray.ShowBalloonTip(4500);}}
    bool Copy(string text){if(String.IsNullOrEmpty(text))return false;try{TextClipboard.SetText(text);return true;}catch(System.Runtime.InteropServices.ExternalException){if(pill.ResultReady){pill.Caption="Clipboard busy";pill.Invalidate();}else Notify("Clipboard busy. Try again.");return false;}}
    void SavePreferences(){try{settings.Save(root);}catch(Exception error){Notify("Could not save preferences: "+error.Message);}}
    void ConfigureStartup(){using(Microsoft.Win32.RegistryKey key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(settings.LaunchAtLogin)key.SetValue("Open Mic AI","\""+Application.ExecutablePath+"\" --background");else key.DeleteValue("Open Mic AI",false);key.DeleteValue("LocalFlow",false);}}
    void ApplySettings(Settings value){settings=value;shortcut.Register(settings.Shortcut);settings.Shortcut=shortcut.Label??settings.Shortcut;pill.RecordingMode=RecordingModes.Normalize(settings.RecordingMode);try{holdMouse.Configure(Hotkey.Mouse(settings.Shortcut)?settings.Shortcut:settings.HoldMouse);settings.Save(root);ConfigureStartup();}catch(Exception error){Notify("Could not apply settings: "+error.Message);}pill.Persistent=settings.ShowBar;if(!Busy){if(pill.Persistent)pill.ShowIdle();else pill.Conceal();}SetStatus(models!=null&&models.Alive?"Ready · "+settings.Shortcut:"Models not ready");}
    void EnsureHub(){if(hub!=null&&!hub.IsDisposed)return;hub=new HubWindow(history,personalization,usage,()=>settings,ApplySettings,CreateModelWindow,shortcut.Choices,shortcut.EditLabel,importer);hub.FormClosed+=(s,e)=>hub=null;}
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
    void ShowApp(){if(exiting)return;EnsureHub();hub.SetStatus(status.Text);hub.Show();if(hub.WindowState==FormWindowState.Minimized)hub.WindowState=FormWindowState.Normal;ShowWindow(hub.Handle,5);hub.BringToFront();hub.Activate();}
    void OpenHub(string page){if(exiting)return;if(Busy){Notify("Finish or cancel the current action first.");return;}EnsureHub();hub.Navigate(page);ShowApp();}
    void OpenHistory(){OpenHub("History");}
    void SaveToHistory(DictationSession current,string transcript,string result){if(String.IsNullOrWhiteSpace(transcript))return;if(settings.SaveHistory)history.Add(transcript,result,current.SelectedText!=null?(current.ActionName=="Translate"?"Translate to "+current.ActionLanguage:current.ActionName=="Summarize"?"Summarize":"Voice edit"):current.Style,current.SelectedText);if(settings.SaveInsights&&current.SelectedText==null)usage.Record(transcript,String.IsNullOrEmpty(result)?transcript:result,current.RecordingSeconds,current.Application,DateTime.UtcNow);}
    void OpenSettings(){OpenHub("Settings");}
    void OpenModels(){OpenHub("Models");}
    ModelWindow CreateModelWindow(){ModelWindow window=new ModelWindow(root,settings,specs??ComputerSpecs.Detect());window.CanApply=()=>!Busy&&!booting;window.ApplyModels=async pair=>{settings.Speech=pair.Speech.Name;settings.Text=pair.Text.Name;SavePreferences();await BootModels();return models!=null&&models.Alive;};return window;}
    static string ApplicationName(IntPtr window){try{uint id;Native.GetWindowThreadProcessId(window,out id);using(Process process=Process.GetProcessById((int)id))return process.ProcessName;}catch{return "Other";}}
    bool CanDictate(){if(selectionProbe!=null)return false;if(importer.Busy){Notify("Finish or cancel the file import in History first.");return false;}if(actionSession!=null){Notify("Finish or cancel AI tools first.");return false;}if(models==null||!models.Alive){Notify(models==null?"Please download models from Settings first.":"Models are not ready. Wait for Ready or use Reload and warm up models.");return false;}return true;}
    void KeyboardToggle(){Toggle();if(settings.RecordingMode=="Hold to talk"&&session!=null&&!session.Stopped)keyboardHeld=session;}
    void KeyboardRelease(){if(selectionProbe!=null)pendingHoldReleased=true;DictationSession held=keyboardHeld;keyboardHeld=null;if(held!=null&&session==held&&!held.Stopped)Finish(held);}
    void Toggle(){if(selectionProbe!=null){Cancel();return;}if(session!=null){if(!session.Stopped)Finish(session);return;}if(actionSession!=null){if(!actionSession.Processing)SpeakInstruction();return;}if(CanDictate())Begin(false);}
    void ToggleEdit(){if(session!=null){if(!session.Stopped)Finish(session);return;}if(CanDictate())Begin(true);}
    async void ToggleTextActions() {
        if(actionSession!=null){Cancel();return;}if(session!=null){Notify("Finish dictation first.");return;}if(!CanDictate())return;
        FocusTarget target=FocusTarget.Capture();if(target.IsPassword){Notify("Choose text outside a password box.");return;}
        CancellationTokenSource probe=new CancellationTokenSource();selectionProbe=probe;
        try {string source=await target.CaptureSelectionAsync(probe.Token);probe.Token.ThrowIfCancellationRequested();
            if(!await target.MatchesCurrentAsync(probe.Token)){Notify("The focused text box changed. Try again.");return;}
            if(String.IsNullOrWhiteSpace(source)){Notify("Select text first, then press your shortcut.");return;}
            if(source.Length>9000){Notify("Select up to 9,000 characters at a time.");return;}
            ShowSelectedActions(target,source);
        }catch(OperationCanceledException){}catch(Exception error){Notify(error.Message);}
        finally {if(selectionProbe==probe)selectionProbe=null;probe.Dispose();}
    }
    void ShowSelectedActions(FocusTarget target,string source){string language;try{language=TextActions.Language(settings.TranslationLanguage);}catch(Exception error){Notify(error.Message);return;}AppStyle style=StyleRouting.Resolve(settings,ApplicationName(target.Window));actionSession=new TextActionSession {Target=target,Source=source,Style=style.Style,Custom=style.Custom,Language=language,PolishReworded=style.PolishReworded};shortcut.ListenForEscape(true);pill.ShowTextActions(source,style.Style,language,target.Window);SetStatus("Selected text · choose an action");}
    async void SpeakInstruction(){TextActionSession current=actionSession;if(current==null||current.Processing)return;try{if(!await current.Target.SelectionMatchesAsync(current.Cancel.Token)){Cancel();Notify("Selection changed. Select the text again.");return;}if(actionSession!=current)return;actionSession=null;FocusTarget target=current.Target;string source=current.Source;current.Dispose();Begin(true,target,source);}catch(OperationCanceledException){}catch(Exception error){Cancel();Notify(error.Message);}}
    async void RunTextAction(string action) {
        TextActionSession current=actionSession;if(current==null||current.Processing||!TextActions.Names.Contains(action))return;
        current.Processing=true;
        string output=null,errorText=null;bool pasted=false;Stopwatch watch=Stopwatch.StartNew();
        try {
            if(!await current.Target.SelectionMatchesAsync(current.Cancel.Token)){errorText="Selection changed. Select the text again.";return;}
            pill.ShowActionProcessing(action,current.Source);SetStatus(action+" · local AI");
            output=await models.TransformSelection(current.Source,action,current.Style,current.Custom,current.Language,current.Cancel.Token,"",current.PolishReworded);
            current.Cancel.Token.ThrowIfCancellationRequested();if(exiting||actionSession!=current)return;
            lastRaw=current.Source;lastResult=output;copyRaw.Enabled=copyResult.Enabled=true;
            if(settings.SaveHistory)history.Add(current.Source,output,action=="Translate"?"Translate to "+current.Language:action,current.Source);
            for(int i=0;i<100&&Native.ModifiersDown();i++)await Task.Delay(20,current.Cancel.Token);
            if(action=="Rewrite"&&!Native.ModifiersDown()&&current.Target.SelectionWritable)pasted=await Paste(output,current.Target,current.Cancel.Token,true);
            
        }catch(OperationCanceledException){if(!current.Cancel.IsCancellationRequested&&!exiting)errorText="Local AI timed out.";}
        catch(Exception error){errorText=error.Message;}
        finally {
            if(actionSession==current){actionSession=null;shortcut.ListenForEscape(false);if(!exiting){SetStatus("Ready · "+settings.Shortcut+" · "+watch.Elapsed.TotalSeconds.ToString("0.00")+"s");if(!current.Cancel.IsCancellationRequested&&output!=null){if(pasted)pill.CompletePaste(output);else pill.ShowResult(output,false,true,current.Target.Window,true,action=="Translate"?"Translation · "+current.Language:action=="Summarize"?"Summary":null);}else if(!current.Cancel.IsCancellationRequested&&errorText!=null){pill.ShowResult(current.Source,true,true,current.Target.Window);pill.Caption="Original unchanged";pill.UpdateTranscript(errorText);Notify(errorText,false);}else pill.Conceal();}}
            current.Dispose();
        }
    }
    void ChooseTextAction(string action){
        DictationSession current=session;
        if(current!=null&&current.SelectedText!=null&&!current.Stopped){
            FocusTarget target=current.Target;string source=current.SelectedText;current.Cancel.Cancel();current.Stop();session=null;heldSession=keyboardHeld=null;ShowSelectedActions(target,source);
            if(action=="Translate"){pill.OpenLanguages();return;}
        }
        RunTextAction(action);
    }
    async void Begin(bool edit,FocusTarget captured=null,string capturedSelection=null,string recordingMode=null) {
        if(selectionProbe!=null)return;
        CancellationTokenSource probe=new CancellationTokenSource();selectionProbe=probe;pendingHoldReleased=false;
        TextRules rules=personalization.Snapshot();FocusTarget target=captured??FocusTarget.Capture();string application=ApplicationName(target.Window);AppStyle style=StyleRouting.Resolve(settings,application);DictationSession current=new DictationSession {Target=target,Application=application,Vocabulary=rules.Guidance(settings.Vocabulary),Style=style.Style,Custom=style.Custom,Rewrite=style.Cleanup!="None"&&style.Style!="Original transcript",Cleanup=style.Cleanup,PolishReworded=style.PolishReworded,Rules=rules,RecordingMode=RecordingModes.Normalize(recordingMode??settings.RecordingMode)};
        try {
            if(current.Target.IsPassword){Notify("Choose a text field outside a password box.");return;}
            string selection=capturedSelection??await current.Target.CaptureSelectionAsync(probe.Token);
            probe.Token.ThrowIfCancellationRequested();if(exiting)return;
            if(!await target.MatchesCurrentAsync(probe.Token)){Notify("The focused text box changed. Try again.");return;}
            if(!String.IsNullOrWhiteSpace(selection)){if(selection.Length>9000){Notify("Select up to 9,000 characters.");return;}current.SelectedText=selection;}else if(edit){Notify("Select some text, then speak an instruction.");return;}
            current.Segments.Submit=pcm=>current.AddPending(Recognize(current,pcm));
            current.Mic=new Microphone(settings.Microphone);current.Mic.Audio=current.Segments.Push;
            if(settings.Sounds)System.Media.SystemSounds.Beep.Play();
            if(settings.MutePlayback)current.Playback=new PlaybackMute();
            session=current; current.Mic.Start();current.RecordingTime.Start();
            if(current.RecordingMode=="Hold to talk"){if(recordingMode==null)keyboardHeld=heldSession=current;if(pendingHoldReleased){Finish(current);return;}}
            shortcut.ListenForEscape(true);if(current.SelectedText!=null)pill.ShowVoiceInstruction(current.SelectedText,current.Target.Window);else{pill.Prepare("Listening",true);pill.Place(current.Target.Window);pill.Reveal();}SetStatus(current.RecordingMode=="Hold to talk"?"Listening · release to finish":current.RecordingMode=="Automatic"?"Listening · pauses finish recording":"Listening · press again to finish");
        } catch(OperationCanceledException){}catch(Exception e){current.Cancel.Cancel();if(current.Mic!=null)current.Mic.Dispose();if(current.Playback!=null)current.Playback.Dispose();session=null;Notify(e.Message);}
        finally {if(selectionProbe==probe)selectionProbe=null;probe.Dispose();if(session!=current){current.Cancel.Dispose();current.PreviewCancel.Dispose();}}
    }
    string CompletedText(DictationSession current){return String.Join(" ",current.PendingSnapshot().Where(t=>t.Status==TaskStatus.RanToCompletion).Select(t=>t.Result).Where(t=>!String.IsNullOrWhiteSpace(t)));}
    async Task<string> Recognize(DictationSession current,byte[] pcm){string text=await models.Transcribe(pcm,current.Vocabulary,current.Cancel.Token);ui.Post(s=>{if(session==current&&!current.Stopped)pill.UpdateTranscript(CompletedText(current));},null);return text;}
    async void Preview(DictationSession current){if(current.Stopped||current.PreviewBusy||current.RecordingTime.ElapsedMilliseconds-current.LastPreviewAt<850)return;int revision;byte[] pcm=current.Segments.Preview(out revision);if(pcm==null)return;current.PreviewBusy=true;current.LastPreviewAt=current.RecordingTime.ElapsedMilliseconds;try{string text=await models.Transcribe(pcm,current.Vocabulary,current.PreviewCancel.Token);if(session==current&&!current.Stopped&&revision==current.Segments.Revision)pill.UpdateTranscript(String.Join(" ",new[]{CompletedText(current),text}.Where(t=>!String.IsNullOrWhiteSpace(t))));}catch(OperationCanceledException){}catch(Exception){}finally{current.PreviewBusy=false;}}
    async void Finish(DictationSession current) {
        current.Stop();if(settings.Sounds)System.Media.SystemSounds.Beep.Play();pill.ShowProcessing(current.Rewrite||current.SelectedText!=null);SetStatus("Processing");
        Stopwatch watch=Stopwatch.StartNew();
        string recognized="",readyText=null;bool saved=false,pasted=false,noSpeech=false,cardOutput=false;
        try {
            string[] parts=await Task.WhenAll(current.PendingSnapshot());current.Cancel.Token.ThrowIfCancellationRequested();
            string text=String.Join(" ",parts.Where(p=>!String.IsNullOrWhiteSpace(p))).Trim();
            if(text.Length==0){noSpeech=true;return;}
            lastRaw=text;copyRaw.Enabled=true;recognized=text;pill.UpdateTranscript(text);
            TextRules rules=current.Rules??new TextRules(new PersonalData());PreparedDictation prepared=current.SelectedText==null?rules.Prepare(text):null;
            string output;
            if(current.SelectedText!=null){SpokenAction action=SpokenAction.Parse(text,settings.TranslationLanguage);current.ActionName=action.Action;current.ActionLanguage=action.Language;cardOutput=action.ShowsCard;pill.ShowActionProcessing(action.Action,text);if(action.Action=="Dictate"){prepared=rules.Prepare(text);output=current.Rewrite?await models.Rewrite(prepared.Text,current.Style,current.Custom,current.Cancel.Token,current.Vocabulary,current.Cleanup,current.PolishReworded):prepared.Text;}else output=action.Action=="Rewrite"?await models.EditSelection(current.SelectedText,text,current.Cancel.Token,current.Vocabulary):await models.TransformSelection(current.SelectedText,action.Action,current.Style,current.Custom,action.Language,current.Cancel.Token,action.Instruction);}
            else output=current.Rewrite?await models.Rewrite(prepared.Text,current.Style,current.Custom,current.Cancel.Token,current.Vocabulary,current.Cleanup,current.PolishReworded):prepared.Text;
            if(prepared!=null)output=rules.Finish(output,prepared);
            current.Cancel.Token.ThrowIfCancellationRequested();if(exiting)return;
            lastResult=output;copyResult.Enabled=true;readyText=output;
            SaveToHistory(current,text,output);saved=true;if(cardOutput)return;
            // Let the user release the shortcut before sending Ctrl+V.
            for(int i=0;i<100&&Native.ModifiersDown();i++)await Task.Delay(20,current.Cancel.Token);
            if(Native.ModifiersDown()||!await current.Target.MatchesCurrentAsync(current.Cancel.Token)||(current.SelectedText!=null&&!current.Target.SelectionWritable)) {return;}
            pasted=await Paste(output,current.Target,current.Cancel.Token,current.SelectedText!=null);
            if(!pasted){Notify("The field or selection changed. Click Copy on the indicator, then press Ctrl+V in your text box.");return;}
            SetStatus("Ready · "+settings.Shortcut+" · "+watch.Elapsed.TotalSeconds.ToString("0.00")+"s");
        } catch(OperationCanceledException){if(!current.Cancel.IsCancellationRequested&&!exiting)Notify("The local rewrite timed out. Use Copy last transcript from the tray.");}
        catch(Exception e){if(!exiting)Notify(e.Message+(!String.IsNullOrEmpty(lastRaw)?" Use Copy last transcript from the tray.":""));}
        finally {
            if(!saved&&!current.Cancel.IsCancellationRequested&&!exiting)SaveToHistory(current,recognized,"");
            if(session==current){session=null;shortcut.ListenForEscape(false);if(!exiting)SetStatus("Ready · "+settings.Shortcut);
                if(!exiting&&!current.Cancel.IsCancellationRequested&&!String.IsNullOrWhiteSpace(readyText??recognized)){if(pasted)pill.CompletePaste(readyText??recognized);else pill.ShowResult(readyText??recognized,readyText==null,true,current.Target.Window,cardOutput,current.ActionName=="Translate"?"Translation · "+current.ActionLanguage:current.ActionName=="Summarize"?"Summary":null);}else if(noSpeech&&!exiting&&!current.Cancel.IsCancellationRequested)pill.ShowNotice("No speech detected","Try another microphone in Settings.");else pill.Conceal();
            }
        }
    }
    public static async Task<bool> Paste(string output,FocusTarget target,CancellationToken cancel,bool requireSelection=false) {
        // Re-copy a canvas selection BEFORE replacing the clipboard with the result.
        if(!await target.MatchesCurrentAsync(cancel)||(requireSelection?!await target.SelectionMatchesAsync(cancel):!await target.DictationTargetMatchesAsync(cancel)))return false;
        IDataObject previous=null;int sequence=0;
        try {
            IDataObject data=Clipboard.GetDataObject();
            if(data!=null){DataObject snapshot=new DataObject();foreach(string format in data.GetFormats(false))try{snapshot.SetData(format,data.GetData(format,false));}catch{}previous=snapshot;}
            TextClipboard.SetText(output);
            sequence=ClipboardSequence();
            cancel.ThrowIfCancellationRequested();
            if(!await target.MatchesCurrentAsync(cancel)||(requireSelection&&target.CanCheckSelectionDirectly&&!target.SelectionMatches()))return false;
            if(!Native.Paste())throw new InvalidOperationException("Windows blocked pasting into this app. The result is available in the tray.");
            await Task.Delay(350,cancel);
            return true;
        } finally {
            if(sequence!=0&&ClipboardSequence()==sequence)ClipboardBackup.Restore(previous);
        }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetClipboardSequenceNumber")]static extern int ClipboardSequence();
    void Cancel(){bool probing=selectionProbe!=null;if(probing)selectionProbe.Cancel();TextActionSession action=actionSession;if(action!=null){actionSession=null;action.Cancel.Cancel();if(!action.Processing)action.Dispose();}DictationSession current=session;if(current!=null){current.Cancel.Cancel();current.Stop();session=null;}if(action==null&&current==null&&!probing)return;shortcut.ListenForEscape(false);pill.Conceal();SetStatus("Ready · "+settings.Shortcut);}
    protected override void ExitThreadCore(){exiting=true;if(streamDeck!=null)streamDeck.Dispose();importer.Cancel();if(reopenWait!=null)reopenWait.Unregister(null);pill.Persistent=false;Cancel();holdMouse.Dispose();if(hub!=null)hub.Close();history.FlushAsync().GetAwaiter().GetResult();personalization.FlushAsync().GetAwaiter().GetResult();usage.FlushAsync().GetAwaiter().GetResult();level.Dispose();health.Dispose();Microsoft.Win32.SystemEvents.PowerModeChanged-=PowerChanged;shortcut.Dispose();pill.Dispose();tray.Visible=false;tray.Icon.Dispose();tray.Dispose();if(models!=null)models.Dispose();base.ExitThreadCore();}
}

public static class Program {
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(int processId);
    [STAThread] public static void Main(string[] args) {
        bool created;bool openWindow=args.Contains("--open")||!args.Contains("--background");
        using(EventWaitHandle reopen=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\OpenMicAI-ShowWindow"))
        using(Mutex mutex=new Mutex(true,"Local\\LocalFlow-2026-native",out created)) {
            if(!created){if(openWindow){AllowSetForegroundWindow(-1);reopen.Set();}return;}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try{Application.Run(new FlowContext(PortableRuntime.Prepare(),openWindow,reopen));}
            catch(Exception error){MessageBox.Show("Open Mic AI could not start. Keep the app in a writable folder.\n\n"+error.Message,"Open Mic AI",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
    }
}
}
