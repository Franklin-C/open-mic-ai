using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using LocalFlow;

public static class BarChecks {
    static int passed;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
    static Control Find(Control root,string text){if(root.Text==text)return root;foreach(Control child in root.Controls){Control found=Find(child,text);if(found!=null)return found;}return null;}
    static TabPage Page(Control control){while(control!=null&&!(control is TabPage))control=control.Parent;return control as TabPage;}
    static void Click(Pill pill,Rectangle bounds){Point p=new Point(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2);foreach(string name in new[]{"OnMouseDown","OnMouseUp","OnMouseClick"})typeof(Control).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pill,new object[]{new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0)});}
    static void Save(Control control,string path){using(Bitmap bitmap=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path);}}
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr window,int index);
    [STAThread]public static void Main(string[] args){
        Application.EnableVisualStyles();string folder=args[0];Directory.CreateDirectory(folder);bool native=args.Contains("--native-shell");
        File.WriteAllText(Path.Combine(folder,"preferences.json"),"{\"Shortcut\":\"Ctrl + D0\",\"ShowBar\":false,\"Speech\":\"my-speech.gguf\",\"Text\":\"my-llm.gguf\",\"Vocabulary\":\"Acme\"}");
        Settings settings=Settings.Load(folder);Check(!settings.ShowBar&&settings.BarLayout=="Horizontal"&&!settings.TaskbarControl,"Old preferences keep visibility and default to horizontal with taskbar off");Check(settings.Shortcut=="Ctrl + D0"&&settings.Speech=="my-speech.gguf"&&settings.Text=="my-llm.gguf"&&settings.Vocabulary=="Acme","Settings migration preserves shortcut, models and vocabulary");
        using(SettingsWindow window=new SettingsWindow(settings,new[]{settings.Shortcut},current:()=>settings,personalStore:new PersonalizationStore(folder))){
            window.Saved=value=>settings=value;window.Show();Application.DoEvents();
            TabControl root=window.Controls.OfType<TabControl>().Single();Check(root.TabPages.Cast<TabPage>().Select(page=>page.Text).SequenceEqual(new[]{"Writing","Access","System","Integrations"}),"Settings has Writing, Access, System and Integrations groups");
            window.SelectSection("Snippets");Check(root.SelectedTab.Text=="Writing"&&Find(window,"Add snippet").Visible,"Snippets opens inside Writing");window.SelectSection("Vocabulary");Check(root.SelectedTab.Text=="Writing"&&Find(window,"Add word").Visible,"Vocabulary opens inside Writing");window.SelectSection("Models");Check(root.SelectedTab.Text=="System","Models opens inside System");window.SelectSection("General");Check(root.SelectedTab.Text=="System"&&Find(window,"Start at sign-in").Visible,"General keeps existing system preferences");
            Check(Page(Find(window,"Install plugin")).Text=="Integrations","Stream Deck installer belongs to Integrations");
            Check(Page(Find(window,"Show dictation bar")).Text=="Dictation bar"&&Page(Find(window,"Show taskbar control")).Text=="Taskbar","Dictation bar and Taskbar have separate pages");
            window.SelectSection("Dictation bar");Application.DoEvents();((CheckBox)Find(window,"Show dictation bar")).Checked=true;((RadioButton)Find(window,"Vertical")).Checked=true;((CheckBox)Find(window,"Show taskbar control")).Checked=true;
            ((CheckBox)Find(window,"Live words")).Checked=false;((CheckBox)Find(window,"Recording indicator")).Checked=false;((CheckBox)Find(window,"Quick buttons")).Checked=false;Check(!settings.TaskbarLiveWords&&!settings.TaskbarIndicator&&!settings.TaskbarButtons,"Taskbar display options save independently");((CheckBox)Find(window,"Quit")).Checked=true;Check(settings.TaskbarMenuQuit&&TaskbarShortcuts.Selected(settings).Contains("quit"),"Right-click shortcut switches choose app-owned tasks");
            Check(settings.ShowBar&&settings.BarLayout=="Vertical"&&settings.TaskbarControl,"Visibility, layout and taskbar controls save immediately");settings.Save(folder);Settings restored=Settings.Load(folder);Check(restored.BarLayout=="Vertical"&&restored.TaskbarControl,"New bar preferences round-trip");
            Save(window,Path.Combine(folder,"dictation-bar-settings.png"));window.SelectSection("Taskbar");Application.DoEvents();Save(window,Path.Combine(folder,"taskbar-settings.png"));window.SelectSection("Integrations");Application.DoEvents();Save(window,Path.Combine(folder,"streamdeck-settings.png"));
            ((RadioButton)Find(window,"Horizontal")).Checked=true;Check(settings.BarLayout=="Horizontal"&&settings.Speech=="my-speech.gguf","Layout changes preserve other preferences");
            window.SelectSection("Shortcuts");ShortcutPicker picker=(ShortcutPicker)Find(window,settings.Shortcut);picker.PerformClick();typeof(ShortcutPicker).GetMethod("OnMouseDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(picker,new object[]{new MouseEventArgs(MouseButtons.XButton1,1,10,10,0)});Check(((RadioButton)Find(window,"Hold to talk")).Checked,"Choosing a mouse shortcut selects hold to talk");((Button)Find(window,"Save shortcut")).PerformClick();Check(settings.Shortcut=="Mouse 4"&&settings.RecordingMode=="Hold to talk","Mouse shortcut and hold mode save together");Save(window,Path.Combine(folder,"shortcut-settings.png"));picker.PerformClick();window.SelectSection("Dictation bar");Check(!picker.Capturing&&!ShortcutWindow.Capturing,"Changing settings subpage ends input capture");window.Close();
        }
        using(Pill pill=new Pill())using(TaskbarControl taskbar=new TaskbarControl()){
            int start=0,copy=0,open=0,release=0,hide=0;pill.Start=()=>start++;pill.History=()=>open++;pill.CopyResult=text=>{copy++;return true;};pill.Release=()=>release++;pill.HideBar=()=>hide++;pill.RememberResult("Recent transcription.");pill.Available=true;pill.Persistent=true;pill.ShowIdle();
            Check(pill.Size==new Size(204,38),"Default horizontal size stays compact");pill.Vertical=true;Check(pill.Size==new Size(44,166),"Vertical toolbar is 44 by 166");
            Rectangle[] hits={pill.DragBounds,pill.DictateBounds,pill.RecentCopyBounds,pill.OpenAppBounds};Check(hits.All(r=>pill.ClientRectangle.Contains(r))&&!hits.Where((r,i)=>hits.Skip(i+1).Any(r.IntersectsWith)).Any(),"Vertical grip and controls have separate hit areas");
            Click(pill,pill.DictateBounds);Click(pill,pill.RecentCopyBounds);Click(pill,pill.OpenAppBounds);Check(start==1&&copy==1&&open==1,"Vertical Dictate, Copy and Open retain independent actions");
            pill.RecordingMode="Hold to talk";Click(pill,pill.DictateBounds);Check(start==2&&release==1,"Vertical hold to talk releases correctly");
            pill.RememberResult("Recent transcription.");Save(pill,Path.Combine(folder,"vertical-bar.png"));SendMessage(pill.Handle,0x21,IntPtr.Zero,IntPtr.Zero);Check((GetWindowLong(pill.Handle,-20)&0x08000000)!=0,"Floating bar remains nonactivating");
            pill.Prepare("Listening",true);pill.UpdateTranscript("Please send the report to Sarah tomorrow.");pill.Reveal();Check(pill.Width==360&&pill.Recording,"Vertical recording expands to a readable transcript card");
            taskbar.Toggle=()=>start++;taskbar.CopyRecent=()=>copy++;taskbar.OpenApp=()=>open++;taskbar.UpdateState(pill,true,"Ready");
            Check(taskbar.IsRecording&&taskbar.CanToggle&&taskbar.PreviewText==pill.TranscriptText,"Taskbar receives live recognized words and recording state");
            using(Bitmap bitmap=taskbar.RenderPreview(320,176))bitmap.Save(Path.Combine(folder,"taskbar-listening.png"));using(Bitmap bitmap=taskbar.RenderPreview(160,80))Check(bitmap.Width<=160&&bitmap.Height<=80,"Taskbar thumbnail honors Explorer size limits");
            SendMessage(taskbar.Handle,0x111,new IntPtr((0x1800<<16)|1),IntPtr.Zero);Check(start==3,"Native thumbnail Dictate/Stop command dispatches");taskbar.InvokeCommand(2);taskbar.InvokeCommand(3);Check(copy==2&&open==2,"Taskbar Copy and Open commands dispatch independently");
            taskbar.Configure(new Settings {TaskbarLiveWords=false,TaskbarButtons=false,TaskbarIndicator=false});taskbar.UpdateState(pill,true,"Ready");Check(!taskbar.PreviewText.Contains("Sarah"),"Live words can be hidden from taskbar preview");taskbar.InvokeCommand(1);taskbar.InvokeCommand(2);Check(start==3&&copy==2,"Hidden quick buttons cannot dispatch");taskbar.Configure(new Settings());
            pill.ShowProcessing(true);taskbar.UpdateState(pill,true,"Processing");Check(taskbar.IsProcessing&&!taskbar.CanToggle&&taskbar.PreviewTitle=="Polishing","Taskbar disables recording during AI editing");taskbar.InvokeCommand(1);Check(start==3,"Disabled taskbar Dictate cannot start during AI editing");
            using(Bitmap bitmap=taskbar.RenderPreview(320,176))bitmap.Save(Path.Combine(folder,"taskbar-polishing.png"));pill.CompletePaste("Finished report.");Check(pill.Idle&&pill.Size==new Size(44,166),"Successful paste restores the selected vertical layout");taskbar.UpdateState(pill,false,"Ready");Check(!taskbar.IsRecording&&!taskbar.IsProcessing&&taskbar.CanToggle&&taskbar.PreviewText=="Finished report.","Taskbar returns to ready and preserves latest result");
            pill.Persistent=false;pill.Conceal();taskbar.UpdateState(pill,false,"Ready");Check(!pill.Visible&&taskbar.CanToggle,"Taskbar works when the idle floating bar is hidden");
            pill.ShowTextActions("Selected source","Smart formatting","French",IntPtr.Zero);taskbar.UpdateState(pill,true,"Ready");Check(taskbar.CanToggle&&taskbar.PreviewText=="Selected source","Selected-text instruction remains accessible from taskbar");pill.Conceal();
            if(native){using(Form editor=new Form {Text="Open Mic AI isolated focus fixture"}){editor.Show();editor.Activate();Application.DoEvents();IntPtr before=GetForegroundWindow();taskbar.Show();for(int i=0;i<40;i++){Application.DoEvents();taskbar.UpdateState(pill,false,"Ready");Thread.Sleep(100);if(taskbar.ButtonsReady)break;}Check(taskbar.ButtonsReady,"Explorer accepted all three native thumbnail buttons (HRESULT "+taskbar.ShellResult.ToString("X8")+")");Check(GetForegroundWindow()==before,"Taskbar registration does not steal editor focus");SendMessage(taskbar.Handle,0x111,new IntPtr((0x1800<<16)|1),IntPtr.Zero);Check(GetForegroundWindow()==before&&start==4,"Thumbnail recording command preserves editor focus");// With no flyout open, DWM limits its cached thumbnail to one pixel; rendering is checked above.
            SendMessage(taskbar.Handle,0x323,IntPtr.Zero,new IntPtr((1<<16)|1));SendMessage(taskbar.Handle,0x326,IntPtr.Zero,IntPtr.Zero);Check(taskbar.IconicResult>=0&&taskbar.ThumbnailResult>=0&&taskbar.LivePreviewResult>=0,"DWM accepts iconic-preview configuration and bounded bitmap API calls");SendMessage(taskbar.Handle,0x112,new IntPtr(0xf120),IntPtr.Zero);Application.DoEvents();Check(open==3,"Left-click restore opens the main app callback");Check(taskbar.WindowState==FormWindowState.Minimized,"Taskbar control stays minimized");Check((GetWindowLong(taskbar.Handle,-20)&0x08000000)!=0,"Taskbar window uses no-activate style");taskbar.HideControl=()=>hide++;taskbar.Close();Application.DoEvents();Check(hide==1,"Closing taskbar entry requests optional control off");editor.Close();}}
        }
        int previews=Array.IndexOf(args,"--previews");if(previews>=0&&previews+1<args.Length){Directory.CreateDirectory(args[previews+1]);foreach(string image in Directory.GetFiles(folder,"*.png"))File.Copy(image,Path.Combine(args[previews+1],Path.GetFileName(image)),true);}
        Console.WriteLine("PASS "+passed+" dictation bar and taskbar checks");
    }
}
