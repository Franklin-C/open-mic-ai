using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using LocalFlow;

public static class HistoryChecks {
    static readonly Dictionary<string,object> report=new Dictionary<string,object>();
    static string fixture,reportPath,previewPath;
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);report[name]=true;Console.WriteLine("PASS "+name);}
    static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static object Call(object value,string name,params object[] args){return value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);}
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr handle,int command);
    [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]static extern bool AttachThreadInput(uint from,uint to,bool attach);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr handle,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,uint message,IntPtr w,IntPtr l);
    static async Task ClickCopy(Pill pill) {
        Rectangle bounds=(Rectangle)typeof(Pill).GetProperty("CopyBounds",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pill,null);
        int position=((bounds.Top+bounds.Height/2)<<16)|(bounds.Left+bounds.Width/2);
        PostMessage(pill.Handle,0x201,new IntPtr(1),new IntPtr(position));PostMessage(pill.Handle,0x202,IntPtr.Zero,new IntPtr(position));await Task.Delay(80);
    }
    static void Activate(Form form,TextBox field) {
        uint pid;uint foreground=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid),current=GetCurrentThreadId();bool attached=false;
        try{if(current!=foreground)attached=AttachThreadInput(current,foreground,true);ShowWindow(form.Handle,5);form.Activate();SetForegroundWindow(form.Handle);field.Focus();}
        finally{if(attached)AttachThreadInput(current,foreground,false);}
    }
    static async Task Wait(Func<bool> ready){Stopwatch watch=Stopwatch.StartNew();while(!ready()){if(watch.Elapsed.TotalSeconds>30)throw new TimeoutException("Fixture did not finish.");await Task.Delay(30);}}
    static async Task Persistence() {
        string root=Path.Combine(fixture,"storage");Directory.CreateDirectory(root);HistoryStore store=new HistoryStore(root);
        string raw="um hello Zo\u00eb new paragraph send the report tomorrow",edited="Hello Zo\u00eb,\r\n\r\nSend the report tomorrow.";
        string id=store.Add(raw,edited,"Email");await store.FlushAsync();
        HistoryEntry entry=new HistoryStore(root).Snapshot().Single();
        Check(entry.Id==id&&entry.Transcript==raw&&entry.Result==edited&&entry.Mode=="Email"&&entry.CreatedUtc.Kind==DateTimeKind.Utc,"History survives restart with original, result, Unicode and formatting");
        HistoryEntry[] copy=store.Snapshot();copy[0].Result="modified outside store";Check(store.Snapshot()[0].Result==edited,"History snapshot cannot mutate stored text");
        store.Add("Change Saturday to Sunday.","The meeting is on Sunday.","Voice edit","The meeting is on Saturday.");await store.FlushAsync();entry=new HistoryStore(root).Snapshot()[0];
        Check(entry.VoiceEdit&&entry.SelectedText.Contains("Saturday")&&entry.Transcript.Contains("Sunday")&&entry.Result.Contains("Sunday"),"Voice edit retains selected source, instruction and result");
        Check(store.Delete(id),"Delete identifies only requested history entry");await store.FlushAsync();Check(new HistoryStore(root).Snapshot().Length==1,"Deletion persists after restart");
        store.Clear();for(int i=0;i<205;i++)store.Add("Speech "+i,"Result "+i,"Keep my wording");await store.FlushAsync();HistoryEntry[] retained=new HistoryStore(root).Snapshot();
        Check(retained.Length==200&&retained[0].Result=="Result 204"&&retained[199].Result=="Result 5","History retains latest 200 entries in order");
        store.Clear();store.Add("Kept after clear","New entry","Original transcript");await store.FlushAsync();Check(new HistoryStore(root).Snapshot().Single().Result=="New entry","Queued writes cannot resurrect cleared history");
        store.Clear();await store.FlushAsync();Check(new HistoryStore(root).Snapshot().Length==0&&!File.ReadAllText(Path.Combine(root,"history.json")).Contains("Speech"),"Clear removes saved text from history file");
        File.WriteAllText(Path.Combine(root,"history.json"),"broken history fixture");HistoryStore repaired=new HistoryStore(root);repaired.Add("One","One","Original transcript");await repaired.FlushAsync();repaired.Add("Two","Two","Original transcript");await repaired.FlushAsync();
        Check(repaired.LoadError!=null&&Directory.GetFiles(root,"history.json.unreadable-*").Length==1&&new HistoryStore(root).Snapshot().Length==2,"Unreadable history is preserved and new saving recovers");
        string failRoot=Path.Combine(fixture,"failure");Directory.CreateDirectory(failRoot);HistoryStore failing=new HistoryStore(failRoot);int errors=0;failing.SaveFailed+=message=>Interlocked.Increment(ref errors);
        using(FileStream locked=new FileStream(Path.Combine(failRoot,"history.json.tmp"),FileMode.Create,FileAccess.ReadWrite,FileShare.None)) {
            Stopwatch watch=Stopwatch.StartNew();failing.Add("Retained in memory","Copy available","Keep my wording");double elapsed=watch.Elapsed.TotalMilliseconds;await failing.FlushAsync();
            report["history_add_ms_while_disk_blocked"]=elapsed;Check(failing.Snapshot().Length==1&&errors==1&&failing.LastSaveError!=null&&elapsed<100,"Disk write failure preserves text without blocking submission");
        }
        failing.Add("Retry","Saved","Keep my wording");await failing.FlushAsync();Check(failing.LastSaveError==null&&new HistoryStore(failRoot).Snapshot().Length==2,"Saving recovers after disk access returns");
        File.WriteAllText(Path.Combine(root,"preferences.json"),"{\"Rewrite\":true,\"Style\":\"Keep my wording\"}");Check(Settings.Load(root).SaveHistory,"Existing preferences enable history by default");
        Settings preferences=Settings.Load(root);preferences.SaveHistory=false;preferences.Save(root);Check(!Settings.Load(root).SaveHistory,"History opt-out persists in preferences");
    }
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(Control item in All(child))yield return item;}
    static Button Button(Control root,string name){return All(root).OfType<Button>().Single(b=>b.Name==name);}
    static async Task Window() {
        string root=Path.Combine(fixture,"window");Directory.CreateDirectory(root);
        HistoryEntry[] seed={new HistoryEntry {Id="dictation",CreatedUtc=DateTime.Today.AddHours(10).ToUniversalTime(),Transcript="um please send the report to Priya tomorrow thanks",Result="Please send the report to Priya tomorrow. Thanks!",Mode="Keep my wording"},new HistoryEntry {Id="voice",CreatedUtc=DateTime.Today.AddDays(-1).AddHours(9).ToUniversalTime(),Transcript="Change Saturday to Sunday.",Result="The meeting is on Sunday.",Mode="Voice edit",SelectedText="The meeting is on Saturday.",VoiceEdit=true}};
        File.WriteAllText(Path.Combine(root,"history.json"),new JavaScriptSerializer().Serialize(seed));HistoryStore store=new HistoryStore(root);
        using(HistoryWindow window=new HistoryWindow(store,true,value=>{})) {
            window.TopMost=true;window.Show();await Task.Delay(150);FlowLayoutPanel list=Field<FlowLayoutPanel>(window,"list");Func<HistoryResultRow[]> rows=()=>list.Controls.OfType<HistoryResultRow>().ToArray();
            Check(rows().Length==2&&rows()[0].Entry.Id=="dictation"&&!All(window).OfType<TabControl>().Any(),"History is a date-grouped list of results without detail tabs");
            Check(All(window).OfType<TextBox>().Where(t=>t.Name=="Result").All(t=>t.ReadOnly&&t.BorderStyle==BorderStyle.None),"Results are selectable native text without input-box borders");
            TextBox search=Field<TextBox>(window,"search");search.Text="PRIYA";await Task.Delay(50);Check(rows().Length==1&&rows()[0].Entry.Id=="dictation","Search finds original and edited text case-insensitively");
            Button(rows()[0],"Copy result").PerformClick();Check(Clipboard.GetText()==seed[0].Result,"History copy uses the complete final result");Button(rows()[0],"Show original").PerformClick();Button(rows()[0],"Copy original").PerformClick();Check(Clipboard.GetText()==seed[0].Transcript,"Original transcript expands inline and can be copied");Button(rows()[0],"Show original").PerformClick();
            search.Text="";Field<Button>(window,"date").PerformClick();Check(Field<Panel>(window,"calendarPanel").Visible&&Field<MonthCalendar>(window,"calendar").BoldedDates.Length==2,"Date picker opens inline and marks recording dates");
            window.FilterDate(DateTime.Today.AddDays(-1));Check(rows().Length==1&&rows()[0].Entry.Id=="voice"&&!Field<Panel>(window,"calendarPanel").Visible,"Calendar date filters results and closes inline picker");search.Text="PRIYA";Check(rows().Length==0,"Search and date filters intersect");search.Text="Saturday";Check(rows().Length==1,"Search includes the selected source of a voice edit");Button(rows()[0],"Show original").PerformClick();Check(All(rows()[0]).OfType<TextBox>().Single(t=>t.Name=="Recorded instruction").Visible,"Voice edit keeps its recorded instruction under Original");
            search.Text="";Field<Button>(window,"resetDate").PerformClick();Check(rows().Length==2,"Clear date restores the entire result list");
            using(Bitmap image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(previewPath,System.Drawing.Imaging.ImageFormat.Png);}
            window.ClientSize=new Size(650,560);window.PerformLayout();Check(rows().All(r=>All(r).OfType<Button>().Where(c=>c.Visible).All(c=>c.Parent==r?c.Right<=r.Width:c.Right<=c.Parent.ClientSize.Width)),"Row actions fit at minimum window size");
            Button(rows().Single(r=>r.Entry.Id=="voice"),"Delete entry").PerformClick();await store.FlushAsync();Check(rows().Length==1&&new HistoryStore(root).Snapshot().Length==1,"Row delete removes only its own entry and persists");
            search.Text="unmatched fixture";Check(rows().Length==0&&All(window).OfType<Label>().Any(l=>l.Text=="No results"),"Empty search shows a clean no-results state");search.Text="";Field<Button>(window,"clear").PerformClick();Check(store.Snapshot().Length==1,"Clear history requires an inline confirmation");Field<Button>(window,"clear").PerformClick();Check(store.Snapshot().Length==0&&All(window).OfType<Label>().Any(l=>l.Text=="No history yet"),"Confirm clear empties the list");window.Close();
        }
        string longResult=String.Join("\r\n",Enumerable.Repeat("A longer result with all details preserved.",20));for(int i=0;i<110;i++)store.Add("Speech "+i,i==109?longResult:"Result "+i,"Polish");
        using(HistoryWindow window=new HistoryWindow(store,true,value=>{})){window.Show();await Task.Delay(100);FlowLayoutPanel list=Field<FlowLayoutPanel>(window,"list");Check(list.Controls.OfType<HistoryResultRow>().Count()==50,"History initially renders only 50 entries");HistoryResultRow row=list.Controls.OfType<HistoryResultRow>().First();int height=row.Height;Button(row,"Show more").PerformClick();Check(row.Height>height&&All(row).OfType<TextBox>().Single(t=>t.Name=="Result").Text==longResult,"Show more reveals long results without changing text");Button(row,"Copy result").PerformClick();Check(Clipboard.GetText()==longResult,"Copy includes every line of a long result");Field<Button>(window,"loadMore").PerformClick();Check(list.Controls.OfType<HistoryResultRow>().Count()==100,"Load more extends the list in batches");window.Close();}
        await store.FlushAsync();
    }
    static async Task Integration(Form form,TextBox first,TextBox second) {
        FlowContext context=new FlowContext(fixture);
        try {
            await Wait(()=>Field<Models>(context,"models")!=null&&Field<Models>(context,"models").Alive);HistoryStore history=Field<HistoryStore>(context,"history");Settings settings=Field<Settings>(context,"settings");settings.ShowBar=false;Field<Pill>(context,"pill").Persistent=false;Field<Pill>(context,"pill").Conceal();
            Check(Field<Models>(context,"models").Alive,"Resident models ready for paste and history integration");
            await FeatureChecks.Models(Field<Models>(context,"models"),Check);await UnifiedChecks.Run(context,form,first,second,Check,Path.GetDirectoryName(previewPath));
            Activate(form,first);await Task.Delay(80);DictationSession preview=new DictationSession {Target=FocusTarget.Capture(),Style="Keep my wording",Rewrite=true};preview.RecordingTime.Start();byte[] wav=File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(previewPath),"validation","speech-fixture.wav"));byte[] pcm=wav.Skip(44).ToArray();preview.Segments.Push(pcm);typeof(FlowContext).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(context,preview);Pill live=Field<Pill>(context,"pill");live.Prepare("Listening",true);live.Place(form.Handle);live.Reveal();await Task.Delay(900);Call(context,"Preview",preview);await Wait(()=>live.TranscriptText.Contains("Sarah"));Check(live.Recording&&live.TranscriptText.Contains("proposal")&&preview.Pending.Count==0,"Resident ASR displays a live preview without adding final transcript segments");Call(context,"Cancel");Check(preview.PreviewCancel.IsCancellationRequested&&history.Snapshot().Length==0,"Stopping preview cancels its work without saving or pasting text");
            Activate(form,first);first.Text="";await Task.Delay(100);Clipboard.SetText("History clipboard sentinel");
            DictationSession session=new DictationSession {Target=FocusTarget.Capture(),Style="Keep my wording",Rewrite=true};session.Pending.Add(Task.FromResult("um please send the report to Priya tomorrow thanks"));
            typeof(FlowContext).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(context,session);Stopwatch watch=Stopwatch.StartNew();Call(context,"Finish",session);await Wait(()=>Field<DictationSession>(context,"session")==null);
            await history.FlushAsync();HistoryEntry entry=new HistoryStore(fixture).Snapshot().Single();report["cleanup_paste_history_seconds"]=watch.Elapsed.TotalSeconds;
            report["first_paste_diagnostics"]=new {fieldText=first.Text,result=entry.Result,window=session.Target.Window.ToInt64(),expectedWindow=form.Handle.ToInt64(),control=session.Target.Control.ToInt64(),expectedControl=first.Handle.ToInt64(),currentWindow=Native.GetForegroundWindow().ToInt64(),currentControl=Native.FocusedHandle(Native.GetForegroundWindow()).ToInt64(),matches=session.Target.MatchesCurrent(),modifiers=Native.ModifiersDown()};
            Check(first.Text==entry.Result&&first.Text.Contains("Priya")&&entry.Transcript.StartsWith("um"),"Completed cleanup automatically pastes and saves both text versions");Check(Clipboard.GetText()=="History clipboard sentinel","Automatic paste preserves previous clipboard");
            Pill pill=Field<Pill>(context,"pill");Check(pill.ResultReady&&pill.ResultText==entry.Result&&IsWindowVisible(pill.Handle)&&session.Target.MatchesCurrent(),"Copy indicator appears after automatic paste without taking focus");
            Activate(form,first);await Task.Delay(100);string before=first.Text;session=new DictationSession {Target=FocusTarget.Capture(),Style="Original transcript",Rewrite=false};TaskCompletionSource<string> delayed=new TaskCompletionSource<string>();session.Pending.Add(delayed.Task);
            typeof(FlowContext).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(context,session);Call(context,"Finish",session);second.Focus();delayed.SetResult("Retained after focus changed.");await Wait(()=>Field<DictationSession>(context,"session")==null);await history.FlushAsync();
            Check(first.Text==before&&second.Text==""&&new HistoryStore(fixture).Snapshot()[0].Result=="Retained after focus changed.","Changed focus prevents paste and retains result in history");
            Check(pill.ResultReady&&pill.ResultText=="Retained after focus changed."&&!Field<System.Windows.Forms.Timer>(pill,"dismiss").Enabled,"Blocked paste leaves a persistent Copy button for the correct result");
            using(Bitmap image=new Bitmap(pill.Width,pill.Height)){pill.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(Path.GetDirectoryName(previewPath),"copy-result-preview.png"),System.Drawing.Imaging.ImageFormat.Png);}
            FocusTarget focused=FocusTarget.Capture();Check(SendMessage(pill.Handle,0x21,form.Handle,new IntPtr((0x201<<16)|1))==new IntPtr(3),"Copy indicator rejects focus activation");await ClickCopy(pill);
            bool copied=Clipboard.GetText()=="Retained after focus changed.",captionCopied=pill.Caption=="Copied",focusPreserved=focused.MatchesCurrent();report["blocked_copy_diagnostics"]=new {copied,captionCopied,focusPreserved,pill.Caption};
            Check(copied&&captionCopied&&focusPreserved,"One click copies failed paste result and preserves text field focus");
            Activate(form,first);first.Text="Before. The meeting is on Saturday. After.";first.Select(8,27);await Task.Delay(80);FocusTarget selected=FocusTarget.Capture();string selectedText=selected.CaptureSelection();Check(selectedText=="The meeting is on Saturday.","Voice edit integration captures exact selection");
            session=new DictationSession {Target=selected,SelectedText=selectedText,Style="Keep my wording",Rewrite=true};session.Pending.Add(Task.FromResult("Change Saturday to Sunday."));typeof(FlowContext).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(context,session);Call(context,"Finish",session);await Wait(()=>Field<DictationSession>(context,"session")==null);await history.FlushAsync();entry=new HistoryStore(fixture).Snapshot()[0];
            report["voice_paste_diagnostics"]=new {text=first.Text,entry.Result,entry.SelectedText,entry.Transcript,window=selected.Window.ToInt64(),expectedWindow=form.Handle.ToInt64(),control=selected.Control.ToInt64(),expectedControl=first.Handle.ToInt64(),currentWindow=Native.GetForegroundWindow().ToInt64(),currentControl=Native.FocusedHandle(Native.GetForegroundWindow()).ToInt64(),matches=selected.MatchesCurrent(),notice=Field<NotifyIcon>(context,"tray").BalloonTipText};
            Check(first.Text=="Before. The meeting is on Sunday. After."&&entry.SelectedText==selectedText&&entry.Transcript=="Change Saturday to Sunday."&&entry.Result.Contains("Sunday"),"Voice edit pastes only selection and records source, command and result");
            int count=history.Snapshot().Length;settings.SaveHistory=false;Activate(form,second);await Task.Delay(80);session=new DictationSession {Target=FocusTarget.Capture(),Style="Original transcript",Rewrite=false};session.Pending.Add(Task.FromResult("No saved history for this dictation."));typeof(FlowContext).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(context,session);Call(context,"Finish",session);await Wait(()=>Field<DictationSession>(context,"session")==null);await history.FlushAsync();
            Check(second.Text=="No saved history for this dictation."&&new HistoryStore(fixture).Snapshot().Length==count,"Disabling history preserves automatic paste and stops saving new text");
            UsageStore usage=Field<UsageStore>(context,"usage");await usage.FlushAsync();Check(usage.Data.Sessions==3&&new UsageStore(fixture).Data.Sessions==3,"Usage tracks dictation with history disabled and excludes voice edits");settings.SaveInsights=false;
            Check(pill.ResultText=="No saved history for this dictation.","Copy fallback remains available with history disabled");
            settings.SaveHistory=true;Activate(form,first);await Task.Delay(80);session=new DictationSession {Target=FocusTarget.Capture(),Style="Keep my wording",Rewrite=true};session.Pending.Add(Task.FromResult(new string('x',9100)));typeof(FlowContext).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(context,session);Call(context,"Finish",session);await Wait(()=>Field<DictationSession>(context,"session")==null);await history.FlushAsync();entry=new HistoryStore(fixture).Snapshot()[0];
            Check(entry.Transcript.Length==9100&&entry.Result=="","Rewrite failure retains recognized transcript in history");
            Check(pill.ResultReady&&pill.Caption=="Transcript ready"&&pill.ResultText.Length==9100,"Rewrite failure shows current transcript instead of an older result");await ClickCopy(pill);Check(Clipboard.GetText()==entry.Transcript,"Copy fallback recovers original text after rewrite failure");
            Check(usage.Data.Sessions==3,"Usage opt-out stops future tracking");count=history.Snapshot().Length;session=new DictationSession {Target=FocusTarget.Capture(),Style="Original transcript",Rewrite=false};session.Pending.Add(Task.FromResult("Cancelled fixture."));session.Cancel.Cancel();typeof(FlowContext).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(context,session);Call(context,"Finish",session);await Wait(()=>Field<DictationSession>(context,"session")==null);
            Check(history.Snapshot().Length==count,"Cancelled processing creates no history entry");Check(!pill.ResultReady&&!IsWindowVisible(pill.Handle),"Cancelled processing does not leave a stale Copy indicator");Call(context,"OpenHistory");await Task.Delay(100);Check(Field<HubWindow>(context,"hub")!=null&&Field<Models>(context,"models").Alive,"In-app history opens while models remain loaded");
        } finally {context.ExitThread();}
    }
    static async Task Indicator(Form form,TextBox field) {
        Activate(form,field);await Task.Delay(80);FocusTarget focus=FocusTarget.Capture();
        using(Pill pill=new Pill()) {
            pill.CopyResult=text=>false;pill.ShowResult("Synthetic result.",false,false,form.Handle);await ClickCopy(pill);
            Check(pill.ResultReady&&pill.Caption=="Text ready"&&!Field<System.Windows.Forms.Timer>(pill,"dismiss").Enabled,"Clipboard copy failure leaves text ready for retry");
            pill.ShowResult("Synthetic result.",false,false,form.Handle);System.Windows.Forms.Timer timer=Field<System.Windows.Forms.Timer>(pill,"dismiss");timer.Interval=80;await Task.Delay(180);
            Check(!pill.ResultReady&&!IsWindowVisible(pill.Handle),"Successful paste Copy indicator dismisses automatically");
            pill.ShowResult("Another result.",false,true,form.Handle);pill.Prepare("Listening",true);Check(!pill.ResultReady&&pill.ResultText==""&&!timer.Enabled&&pill.TranscriptText==""&&!pill.Processing,"New dictation resets previous Copy state and timer");
            bool cancelled=false;pill.Cancel=()=>cancelled=true;PostMessage(pill.Handle,0x201,new IntPtr(1),new IntPtr((25<<16)|(pill.Width-15)));PostMessage(pill.Handle,0x202,IntPtr.Zero,new IntPtr((25<<16)|(pill.Width-15)));await Task.Delay(60);
            Check(cancelled&&focus.MatchesCurrent(),"Recording close still cancels without taking focus");pill.Conceal();
        }
    }
    [STAThread]public static void Main(string[] args) {
        fixture=args[0];reportPath=args[1];previewPath=args[2];Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        Form form=new Form {Text="LocalFlow history verification",ClientSize=new Size(500,220),TopMost=true};TextBox first=new TextBox {Location=new Point(20,20),Size=new Size(450,75),Multiline=true},second=new TextBox {Location=new Point(20,120),Size=new Size(450,75),Multiline=true};form.Controls.Add(first);form.Controls.Add(second);
        form.Shown+=async(s,e)=>{
            DataObject clipboard=new DataObject();bool captured=false;
            try {IDataObject original=Clipboard.GetDataObject();if(original!=null)foreach(string format in original.GetFormats(false))try{clipboard.SetData(format,original.GetData(format,false));}catch{}captured=true;await Persistence();await Window();await Indicator(form,first);await FeatureChecks.Run(fixture,Path.GetDirectoryName(previewPath),form,first,Check);await Integration(form,first,second);report["complete"]=true;}
            catch(Exception error){report["error"]=error.ToString();Console.WriteLine(error);Environment.ExitCode=1;}
            finally {if(captured)try{Clipboard.SetDataObject(clipboard,true);}catch{}File.WriteAllText(reportPath,new JavaScriptSerializer().Serialize(report));form.Close();}
        };Application.Run(form);
    }
}
