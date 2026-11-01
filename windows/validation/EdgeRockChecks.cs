using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using LocalFlow;

public static class EdgeRockChecks {
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static T Field<T>(object instance,string name){return (T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);}
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(Control nested in All(child))yield return nested;}
    static void Set(object instance,string name,object value){instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(instance,value);}
    static async Task Reply(HttpListenerContext request,string text){byte[] response=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new {choices=new[]{new {finish_reason="stop",message=new {content=text}}}}));request.Response.ContentType="application/json";request.Response.ContentLength64=response.Length;await request.Response.OutputStream.WriteAsync(response,0,response.Length);request.Response.Close();}
    static async Task WaitFinished(FlowContext context){Stopwatch timer=Stopwatch.StartNew();while(Field<DictationSession>(context,"session")!=null&&timer.Elapsed.TotalSeconds<10)await Task.Delay(20);Check(Field<DictationSession>(context,"session")==null,"The shared-input session completes");}
    static async Task InputModes(Models model,HttpListener server){
        string fixture=Path.Combine(Path.GetTempPath(),"OpenMicAI-input-modes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
        new Settings {Shortcut="Ctrl + Alt + Shift + F24",ImageShortcut="Off",ShowBar=false,SaveInsights=false}.Save(fixture);
        FlowContext context=new FlowContext(fixture,false,integrateShell:false);
        try{
            Stopwatch timer=Stopwatch.StartNew();while(Field<bool>(context,"booting")&&timer.Elapsed.TotalSeconds<15)await Task.Delay(25);
            Check(!Field<bool>(context,"booting"),"Input routing fixture starts without a microphone or model download");Set(context,"models",model);
            DictationSession image=new DictationSession {Target=new FocusTarget(),Image=new byte[]{1,2,3}};image.AddPending(Task.FromResult("What does this show?"));Set(context,"session",image);
            typeof(FlowContext).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(context,new object[]{image});
            HttpListenerContext request=await server.GetContextAsync();string body;using(StreamReader reader=new StreamReader(request.Request.InputStream))body=await reader.ReadToEndAsync();
            Check(body.Contains("image_url")&&body.Contains("What does this show?"),"Shared-input image questions go directly to vision without a rewrite or format decision");await Reply(request,"An image fixture.");await WaitFinished(context);
            Pill pill=Field<Pill>(context,"pill");Check(pill.Caption=="Image answer"&&pill.ResultText=="An image fixture."&&pill.Width==440,"Image answers use an expanded copy card instead of the automatic paste path");
            Check(Field<HistoryStore>(context,"history").Snapshot().Count()==0,"Image questions and answers are not persisted as dictation history");
            DictationSession dictation=new DictationSession {Target=new FocusTarget(),Style="Original transcript",Rewrite=false};dictation.AddPending(Task.FromResult("Please send the blue folder."));Set(context,"session",dictation);
            typeof(FlowContext).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(context,new object[]{dictation});await WaitFinished(context);
            Check(pill.ResultText=="Please send the blue folder."&&Field<HistoryStore>(context,"history").Snapshot().Count()==1,"Ordinary dictation retains its text output and existing history behavior");
            DictationSession selection=new DictationSession {Target=new FocusTarget(),SelectedText="The meeting is Saturday. Bring the blue folder.",Style="Clean",Rewrite=true};selection.AddPending(Task.FromResult("Rewrite this and change Saturday to Sunday."));Set(context,"session",selection);
            typeof(FlowContext).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(context,new object[]{selection});
            request=await server.GetContextAsync();using(StreamReader reader=new StreamReader(request.Request.InputStream))body=await reader.ReadToEndAsync();Check(!body.Contains("image_url")&&body.Contains("Saturday"),"Selected text retains its text-edit request rather than entering vision");await Reply(request,"The meeting is Sunday. Bring the blue folder.");
            request=await server.GetContextAsync();await Reply(request,"{\"safe\":true}");await WaitFinished(context);
            Check(pill.ResultText=="The meeting is Sunday. Bring the blue folder."&&Field<HistoryStore>(context,"history").Snapshot().Count()==2,"Selected-text edits retain the meaning check and history behavior");
        }finally{
            Set(context,"models",null);context.ExitThread();context.Dispose();
            string full=Path.GetFullPath(fixture);if(!full.StartsWith(Path.GetFullPath(Path.GetTempPath())+"OpenMicAI-input-modes-",StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected fixture cleanup path");Directory.Delete(full,true);
        }
    }
    static async Task Run(){
        Check(ModelLibrary.TextModels.Last().Name==EdgeRockRuntime.ModelName,"EdgeRock is an independent experimental language choice");
        Check(ModelLibrary.Recommend(new ComputerSpecs()).Text.Name!=EdgeRockRuntime.ModelName,"Automatic recommendations retain a tested GGUF default");
        ModelPair shared=ModelLibrary.Find(ModelLibrary.SpeechModels[0].Name,ModelLibrary.DecisionModel.Name);Check(shared.DecisionFile==shared.Text&&ModelLibrary.RequiredFiles(shared).Length==2,"Automatic decisions share the writing model without duplicate downloads");
        ModelPair independent=ModelLibrary.Find(shared.Speech.Name,shared.Text.Name,ModelLibrary.DecisionModels[1].Name);Check(independent.DecisionFile==ModelLibrary.DecisionModels[1]&&ModelLibrary.RequiredFiles(independent).Length==3,"An explicit independent decision model is included in verified downloads");
        Check(ModelLibrary.Find(shared.Speech.Name,shared.Text.Name,"../unknown-model")==null,"Unknown decision model paths cannot bypass the pinned catalog");
        JavaScriptSerializer settingsJson=new JavaScriptSerializer();Settings saved=settingsJson.Deserialize<Settings>(settingsJson.Serialize(new Settings {Speech=ModelLibrary.SpeechModels.Last().Name,Decision=independent.DecisionChoice,EdgeRockFastWriting=false}));
        Check(saved.Decision==independent.DecisionChoice&&saved.Speech==ModelLibrary.SpeechModels.Last().Name&&!saved.EdgeRockFastWriting,"Decision and speech selections round-trip without changing the writing preference");
        Check(settingsJson.Deserialize<Settings>("{}").Decision=="auto","Existing settings without a decision choice default to Automatic");
        Check(ModelLibrary.SpeechModels.Any(file=>file.Language=="auto")&&ModelLibrary.SpeechModels.Any(file=>file.Name=="tdt_ctc-110m-f16.gguf"),"Speech choices include multilingual and compact models");
        Check(!EdgeRockRuntime.Installed(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"))),"A missing EdgeRock runtime is not reported installed");
        using(ModelWindow setup=new ModelWindow(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")),new Settings(),new ComputerSpecs())){
            Field<ComboBox>(setup,"textChoice").SelectedItem=ModelLibrary.TextModels.Last();
            Check(Field<Button>(setup,"install").Text=="Download & load models"&&Field<Label>(setup,"state").Text.Contains("Setup progress"),"EdgeRock setup is offered by the model action with progress instead of a manual shell command");
            Check(setup.FastWriting&&Field<RedCheck>(setup,"fastWriting").Text.Contains("Qwen"),"The faster writing option clearly identifies Qwen and can be disabled for EdgeRock writing");
        }
        using(ShortcutWindow shortcuts=new ShortcutWindow())Check(shortcuts.RegisterImage("Off")&&shortcuts.ImageLabel=="Off","Image hotkeys can be disabled without replacing the dictation shortcut");
        using(Bitmap source=new Bitmap(12,10)){
            DataObject data=new DataObject();data.SetData(DataFormats.Bitmap,source);byte[] copied=SelectedImageData.FromClipboard(data);
            using(MemoryStream stream=new MemoryStream(copied))using(Image decoded=Image.FromStream(stream))Check(decoded.Width==12&&decoded.Height==10,"A selected bitmap becomes a bounded in-memory PNG");
            Check(source.Width==12,"Reading a selected bitmap does not dispose the clipboard-owned image");
        }
        DataObject link=new DataObject();link.SetText("https://example.com/image.png");Check(SelectedImageData.FromClipboard(link)==null,"Text and remote image links cannot become selected images");
        DataObject html=new DataObject();html.SetData(DataFormats.Html,"<html><img src='unselected'><body><!--StartFragment--><a><img src='https://example.com/image'></a><!--EndFragment--></body></html>");Check(SelectedImageData.IsImageFragment(html),"An image-only HTML selection can request region capture without fetching its URL");
        html.SetData(DataFormats.Html,"<img src='outside'><!--StartFragment-->Selected text<!--EndFragment-->");Check(!SelectedImageData.IsImageFragment(html),"An unselected page image cannot turn selected text into an image question");
        using(Pill card=new Pill()){
            card.ShowImageActions(IntPtr.Zero);Check(card.ChoosingImage&&!card.ChoosingAction&&card.AccessibleName.Contains("Ask about image"),"Selected images offer Ask about image instead of text-edit actions");
            card.ShowTextActions("Selected text","Smart formatting","English",IntPtr.Zero);Check(!card.ChoosingImage&&card.ChoosingAction&&!All(card).OfType<Button>().Any(b=>b.Visible&&b.Text=="Ask about image"),"Text selection restores Rewrite, Translate and Summarize without an image action");
            card.ShowImageVoiceInstruction(IntPtr.Zero);Check(card.Recording&&card.ChoosingImage&&!card.InstructionRecording&&!card.ChoosingAction,"Shared-hotkey image recording exposes the image question action without text edit actions");
            bool stopped=false;card.Start=()=>stopped=true;All(card).OfType<Button>().Single(b=>b.Text=="Ask about image").PerformClick();Check(stopped,"Ask about image completes the existing recording rather than starting a second microphone");
            Check(new Settings().ImageShortcut=="Off","The shared dictation shortcut works without requiring a second hotkey");
            card.Conceal();
        }
        using(ImageQuestionWindow window=new ImageQuestionWindow(()=>null,()=>true,()=>-1)){
            window.Show();Application.DoEvents();
            Check(All(window).OfType<Button>().Any(b=>b.Text=="Attach image")&&All(window).OfType<Button>().Any(b=>b.Text=="Select screen region"),"Image questions expose both attachment and region selection");
            Check(!window.IsBusy,"Opening image questions does not capture or record automatically");
            MethodInfo set=typeof(ImageQuestionWindow).GetMethod("SetImage",BindingFlags.Instance|BindingFlags.NonPublic);
            using(Bitmap source=new Bitmap(40,30)){set.Invoke(window,new object[]{source});}
            byte[] png=Field<byte[]>(window,"png");
            using(MemoryStream stream=new MemoryStream(png))using(Image restored=Image.FromStream(stream))Check(restored.Width==40&&restored.Height==30,"The selected image stays valid after its source is disposed");
            All(window).OfType<Button>().Single(b=>b.Text=="Ask").PerformClick();Application.DoEvents();
            Check(!window.IsBusy&&All(window).OfType<Label>().Any(l=>l.Text.StartsWith("Load a language model")),"An unavailable backend gives feedback without recording or sending a question");
            window.Close();
        }
        TcpListener portPicker=new TcpListener(IPAddress.Loopback,0);portPicker.Start();int port=((IPEndPoint)portPicker.LocalEndpoint).Port;portPicker.Stop();
        using(HttpListener server=new HttpListener()){
            server.Prefixes.Add("http://127.0.0.1:"+port+"/");server.Start();Models model=new Models(Path.GetTempPath());
            List<Process> processes=Field<List<Process>>(model,"processes");processes.Add(Process.GetCurrentProcess());processes.Add(Process.GetCurrentProcess());
            typeof(Models).GetField("rewriteUrl",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(model,"http://127.0.0.1:"+port);
            typeof(Models).GetProperty("SupportsImages").GetSetMethod(true).Invoke(model,new object[]{true});model.Ready=true;
            try{
                byte[] largeImage=new byte[2100000];new Random(7).NextBytes(largeImage);
                Task<string> answer=model.AskImage(largeImage,"What does this show?",CancellationToken.None);
                HttpListenerContext request=await server.GetContextAsync();string body;using(StreamReader reader=new StreamReader(request.Request.InputStream))body=await reader.ReadToEndAsync();
                var payload=new JavaScriptSerializer {MaxJsonLength=16*1024*1024}.Deserialize<Dictionary<string,object>>(body);
                Check(body.Length>2097152&&body.Contains("data:image/png;base64,")&&body.Contains("What does this show?"),"Image payloads over the default JSON limit reach the chat API with their question");
                Check(request.Request.Headers["Authorization"]=="Bearer "+Field<string>(model,"token"),"Image requests carry the private local-process bearer token");
                byte[] response=Encoding.UTF8.GetBytes("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"A synthetic fixture.\"}}]}");request.Response.ContentType="application/json";request.Response.ContentLength64=response.Length;await request.Response.OutputStream.WriteAsync(response,0,response.Length);request.Response.Close();
                Check(await answer=="A synthetic fixture.","Image answers use the existing completion-result checks");
                using(CancellationTokenSource cancel=new CancellationTokenSource()){
                    Set(model,"visionUrl","http://127.0.0.1:"+port);
                    Task<string> cancelled=model.AskImage(new byte[]{1},"Cancel this question.",cancel.Token);HttpListenerContext waiting=await server.GetContextAsync();string identity=waiting.Request.Headers["X-OpenMic-Request"];cancel.Cancel();bool observed=false;
                    HttpListenerContext signal=await server.GetContextAsync();Check(signal.Request.Url.AbsolutePath=="/cancel/"+identity&&identity.Length==32,"Cancellation reaches the EdgeRock control endpoint with the original request identity");await Reply(signal,"Cancelled");
                    try{await cancelled;}catch(OperationCanceledException){observed=true;}waiting.Response.Abort();Check(observed,"Cancelling an image question cancels the in-flight local request");
                }
                await InputModes(model,server);
                string edgeUrl="http://127.0.0.1:"+port;Set(model,"decisionUrl","http://127.0.0.1:1");model.WritingBudgetMilliseconds=40;
                PropertyInfo writing=typeof(Models).GetProperty("WritingUrl",BindingFlags.Instance|BindingFlags.NonPublic);MethodInfo bounded=typeof(Models).GetMethod("WritingTask",BindingFlags.Instance|BindingFlags.NonPublic);int fallbacks=0;
                Func<CancellationToken,Task<string>> slow=async token=>{if((string)writing.GetValue(model,null)==edgeUrl){await Task.Delay(5000,token);return "late";}fallbacks++;return "Original facts preserved.";};
                string recovered=await (Task<string>)bounded.Invoke(model,new object[]{slow,CancellationToken.None});Check(recovered=="Original facts preserved."&&fallbacks==1,"Slow EdgeRock writing retries once through the fast model after its time budget");
                Check((string)writing.GetValue(model,null)==edgeUrl,"The fallback endpoint cannot leak into later image or writing requests");
                fallbacks=0;using(CancellationTokenSource userCancel=new CancellationTokenSource()){userCancel.CancelAfter(5);bool stopped=false;try{await (Task<string>)bounded.Invoke(model,new object[]{slow,userCancel.Token});}catch(OperationCanceledException){stopped=true;}Check(stopped&&fallbacks==0,"User cancellation does not restart generation through the fallback model");}
            }finally{processes.Clear();model.Dispose();server.Stop();}
        }
    }
    [STAThread]public static void Main(){Application.EnableVisualStyles();using(Form driver=new Form {ShowInTaskbar=false,Opacity=0}){driver.Shown+=async(s,e)=>{try{await Run();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}finally{driver.Close();}};Application.Run(driver);}}
}
