using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using LocalFlow;

public static class ModelLoadingChecks {
    static readonly Dictionary<string,object> report=new Dictionary<string,object>();
    static T Field<T>(object instance,string name){return (T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);}
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(Control nested in All(child))yield return nested;}
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);report[name]=true;Console.WriteLine("PASS "+name);}
    static async Task Wait(Func<bool> condition){Stopwatch clock=Stopwatch.StartNew();while(!condition()){if(clock.Elapsed.TotalSeconds>20)throw new TimeoutException("Model UI did not finish.");await Task.Delay(20);}}
    static async Task Run(string fixture,string preview){
        Directory.CreateDirectory(fixture);Settings prefs=new Settings();ComputerSpecs specs=new ComputerSpecs {RamBytes=128L*1073741824,GpuBytes=12L*1073741824,Gpu="Fixture GPU"};
        using(ModelWindow missing=new ModelWindow(fixture,prefs,specs)){
            missing.Show();await Task.Delay(80);Button[] buttons=All(missing).OfType<Button>().Where(b=>b.Visible).ToArray();Check(buttons.Length==1&&buttons[0].Text=="Download & load models","Missing models have one download-and-load action");Check(All(missing).OfType<ComboBox>().Count()==2,"Speech and language models retain independent selectors");
            ComboBox speech=Field<ComboBox>(missing,"speechChoice"),text=Field<ComboBox>(missing,"textChoice");text.SelectedItem=ModelLibrary.TextModels[0];Check(speech.SelectedItem==ModelLibrary.SpeechModels[0]&&text.SelectedItem==ModelLibrary.TextModels[0],"Selecting a language model leaves the speech selection unchanged");speech.SelectedItem=ModelLibrary.SpeechModels[1];Check(text.SelectedItem==ModelLibrary.TextModels[0],"Selecting speech leaves the language selection unchanged");
            Check(!buttons.Any(b=>b.Text=="Recommended"||b.Text.Contains("Reload")||b.Text=="Use models"),"Legacy model action buttons are removed");missing.Close();
        }
        // Isolated tiny files exercise both SHA checks without downloading weights or touching installed files.
        ModelPair pair=ModelLibrary.Pairs[0];long voiceBytes=pair.Speech.Bytes,textBytes=pair.Text.Bytes;string voiceHash=pair.Speech.Sha256,textHash=pair.Text.Sha256;
        byte[] voice=System.Text.Encoding.UTF8.GetBytes("Synthetic speech model fixture"),language=System.Text.Encoding.UTF8.GetBytes("Synthetic language model fixture");
        Directory.CreateDirectory(Path.Combine(fixture,"models"));File.WriteAllBytes(Path.Combine(fixture,"models",pair.Speech.Name),voice);File.WriteAllBytes(Path.Combine(fixture,"models",pair.Text.Name),language);
        using(SHA256 hash=SHA256.Create()){pair.Speech.Bytes=voice.Length;pair.Text.Bytes=language.Length;pair.Speech.Sha256=BitConverter.ToString(hash.ComputeHash(voice)).Replace("-","").ToLowerInvariant();pair.Text.Sha256=BitConverter.ToString(hash.ComputeHash(language)).Replace("-","").ToLowerInvariant();}
        try{using(ModelWindow window=new ModelWindow(fixture,prefs,specs)){
            window.Show();await Task.Delay(50);Button install=Field<Button>(window,"install"),cancel=Field<Button>(window,"cancel");Label state=Field<Label>(window,"state");ComboBox speech=Field<ComboBox>(window,"speechChoice"),text=Field<ComboBox>(window,"textChoice");
            Check(install.Text=="Load models"&&install.Visible&&!cancel.Visible,"Downloaded models use the same action labelled Load models");
            TaskCompletionSource<bool> loader=new TaskCompletionSource<bool>();int calls=0;bool verified=false;
            window.ApplyModels=async chosen=>{calls++;verified=chosen.Speech==pair.Speech&&chosen.Text==pair.Text&&await ModelLibrary.Verify(Path.Combine(fixture,"models",chosen.Speech.Name),chosen.Speech.Bytes,chosen.Speech.Sha256,System.Threading.CancellationToken.None)&&await ModelLibrary.Verify(Path.Combine(fixture,"models",chosen.Text.Name),chosen.Text.Bytes,chosen.Text.Sha256,System.Threading.CancellationToken.None);return await loader.Task;};
            install.PerformClick();await Wait(()=>calls==1);Check(verified,"Both selected model files are verified before loading");Check(!install.Enabled&&!speech.Enabled&&!text.Enabled&&!cancel.Visible&&state.Text=="Loading both models…","One action stays busy until both models finish loading");await Task.Delay(100);Check(!install.Enabled&&!state.Text.StartsWith("Ready"),"Ready is not shown while the loader is pending");loader.SetResult(true);await Wait(()=>install.Enabled);Check(state.Text=="Ready · both models stay loaded"&&speech.Enabled&&text.Enabled,"Successful loading shows Ready and restores selection controls");
            using(Bitmap image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(preview);}
            window.ApplyModels=chosen=>Task.FromResult(false);install.PerformClick();await Wait(()=>install.Enabled);Check(state.Text.Contains("Could not load")&&install.Text=="Load models","A failed load keeps a clear retry action");
            window.ApplyModels=chosen=>{throw new IOException("Fixture load error");};install.PerformClick();await Wait(()=>install.Enabled);Check(state.Text.Contains("Fixture load error")&&!cancel.Visible,"Loader exceptions leave the page usable with inline feedback");
            window.CanApply=()=>false;install.PerformClick();Check(install.Enabled&&speech.Enabled&&state.Text.Contains("Finish recording"),"An active recording or warm-up blocks replacement without disabling Settings");
            window.UpdateStatus("Loading local models",prefs);Check(Field<Label>(window,"loaded").Text=="Models downloaded","Downloaded model files are not labelled ready before readiness");window.UpdateStatus("Ready · shortcut",prefs);Check(Field<Label>(window,"loaded").Text=="Models ready","Ready confirms both models are loaded");Check(!All(window).OfType<Label>().Any(l=>l.Text.Contains("Fixture GPU")||l.Text.Contains("RAM")||l.Text.Contains("GPU")),"Models page omits computer hardware details");window.Close();
        }}finally{pair.Speech.Bytes=voiceBytes;pair.Text.Bytes=textBytes;pair.Speech.Sha256=voiceHash;pair.Text.Sha256=textHash;}
        string flowRoot=Path.Combine(fixture,"reopen-context");Directory.CreateDirectory(flowRoot);
        using(System.Threading.EventWaitHandle reopen=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset)){
            FlowContext context=new FlowContext(flowRoot,false,reopen);
            try{
                await Wait(()=>!Field<bool>(context,"booting"));await Task.Delay(100);
                typeof(FlowContext).GetMethod("OpenModels",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(context,null);
                HubWindow hub=Field<HubWindow>(context,"hub");ModelWindow page=All(hub).OfType<ModelWindow>().Single();ComboBox selected=Field<ComboBox>(page,"textChoice");selected.SelectedItem=ModelLibrary.TextModels[5];hub.WindowState=FormWindowState.Minimized;reopen.Set();await Wait(()=>hub.Visible&&hub.WindowState!=FormWindowState.Minimized);
                Check(All(hub).OfType<ModelWindow>().Single()==page&&!page.IsDisposed&&selected.SelectedItem==ModelLibrary.TextModels[5],"Reopening preserves the existing Models page and its selections");
                hub.Hide();reopen.Set();await Wait(()=>hub.Visible);Check(Field<HubWindow>(context,"hub")==hub,"Open app also restores a hidden window without rebuilding its page");
                NotifyIcon tray=Field<NotifyIcon>(context,"tray");Check(tray.ContextMenuStrip.Items.OfType<ToolStripMenuItem>().Any(i=>i.Text=="Open app"),"The tray has an explicit Open app command");
                hub.Close();Check(Field<HubWindow>(context,"hub")==null,"Closing clears the disposed main window reference");reopen.Set();await Wait(()=>Field<HubWindow>(context,"hub")!=null);Check(Field<HubWindow>(context,"hub").Visible,"The reopen signal creates a visible replacement for a closed window");
            }finally{context.ExitThread();context.Dispose();}
        }
        report["complete"]=true;report["microphone_used"]=false;report["model_downloads_performed"]=false;
    }
    [STAThread]public static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(Form driver=new Form {ShowInTaskbar=false,Opacity=0}){driver.Shown+=async(s,e)=>{try{await Run(args[0],args[2]);}catch(Exception error){report["complete"]=false;report["error"]=error.ToString();Console.WriteLine(error);Environment.ExitCode=1;}finally{File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(report));driver.Close();}};Application.Run(driver);}}
}
