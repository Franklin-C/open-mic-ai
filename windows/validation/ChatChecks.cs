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

public static class ChatChecks {
    static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
    static void Set(object target,string name,object value){target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
    static Task Ask(ImageQuestionWindow window){return (Task)window.GetType().GetMethod("Ask",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);}
    static async Task<string> Read(HttpListenerContext request){using(StreamReader input=new StreamReader(request.Request.InputStream))return await input.ReadToEndAsync();}
    static async Task Reply(HttpListenerContext request,string answer){byte[] data=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new {choices=new[]{new {finish_reason="stop",message=new {content=answer}}}}));request.Response.ContentLength64=data.Length;await request.Response.OutputStream.WriteAsync(data,0,data.Length);request.Response.Close();}
    static async Task Run(){
        Check(ModelLibrary.DecisionModels.Any(file=>file.Name==DecisionRuntime.D1)&&ModelLibrary.DecisionModels.Any(file=>file.Name==DecisionRuntime.Simple),"Both dedicated decision backends are selectable");
        ModelPair edge=ModelLibrary.Find(ModelLibrary.SpeechModels[0].Name,EdgeRockRuntime.ModelName,DecisionRuntime.D1);
        Check(ModelLibrary.RequiredFiles(edge).Contains(ModelLibrary.DecisionModel),"EdgeRock keeps a writing model when decisions use a non-generative backend");
        ChatSession bounded=new ChatSession();for(int i=0;i<30;i++)bounded.Complete("Question "+i,new string('a',600));ChatTurn[] snapshot=bounded.Request("Latest question");
        Check(snapshot.Length<=17&&snapshot.Sum(turn=>turn.Text.Length)<=12000&&snapshot.Last().Text=="Latest question"&&snapshot[0].Role=="user","Chat bounds context by removing complete old exchanges");
        int before=bounded.Turns.Length;bounded.Request("A cancelled question");Check(bounded.Turns.Length==before,"Preparing or cancelling a request does not commit a partial turn");
        var picker=new TcpListener(IPAddress.Loopback,0);picker.Start();int port=((IPEndPoint)picker.LocalEndpoint).Port;picker.Stop();string url="http://127.0.0.1:"+port;
        using(HttpListener server=new HttpListener())using(Models model=new Models(Path.GetTempPath())){
            server.Prefixes.Add(url+"/");server.Start();var processes=Field<List<Process>>(model,"processes");processes.Add(Process.GetCurrentProcess());processes.Add(Process.GetCurrentProcess());Set(model,"rewriteUrl",url);Set(model,"visionUrl",url);model.Ready=true;typeof(Models).GetProperty("SupportsImages").GetSetMethod(true).Invoke(model,new object[]{true});
            try{using(ImageQuestionWindow window=new ImageQuestionWindow(()=>model,()=>true,()=>-1)){
                window.Show();Application.DoEvents();TextBox input=Field<TextBox>(window,"question");ChatTranscript transcript=Field<ChatTranscript>(window,"answer");
                using(Bitmap image=new Bitmap(80,40)){using(MemoryStream buffer=new MemoryStream()){image.Save(buffer,System.Drawing.Imaging.ImageFormat.Png);window.UseImage(buffer.ToArray());}}
                input.Text="Read the number.";Task first=Ask(window);HttpListenerContext request=await server.GetContextAsync();string body=await Read(request);
                Check(transcript.Controls.OfType<ChatMessageBubble>().Count()==2&&!transcript.Controls.OfType<TextBox>().Any(),"A sent message and thinking state appear as chat bubbles, not an editable conversation box");
                Check(body.Contains("data:image/png;base64,")&&request.Request.Headers["Authorization"]=="Bearer "+Field<string>(model,"token"),"Image chat sends the selected image to the authenticated local model");await Reply(request,"The number is 42.");await first;
                Check(transcript.Text.Contains("Read the number.")&&transcript.Text.Contains("The number is 42.")&&input.Text=="","Successful chat displays both turns and clears the composer");
                input.Text="What is that number plus one?";Task followup=Ask(window);request=await server.GetContextAsync();body=await Read(request);
                Check(body.Contains("The number is 42.")&&body.Contains("What is that number plus one?")&&body.Split(new[]{"data:image/png;base64,"},StringSplitOptions.None).Length==2,"Follow-up requests include the original image and prior answer exactly once");await Reply(request,"43.");await followup;
                Check(Field<ChatSession>(window,"conversation").Turns.Length==4,"Follow-ups append to the same conversation");
                string preview=Environment.GetEnvironmentVariable("OPENMIC_CHAT_PREVIEW");if(!String.IsNullOrEmpty(preview))using(Bitmap shot=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(preview);}
                Field<Button>(window,"newChat").PerformClick();Check(Field<byte[]>(window,"png")==null&&transcript.Text==""&&Field<ChatSession>(window,"conversation").Turns.Length==0,"New chat clears image and conversation together");
                Set(model,"rewriteUrl","http://127.0.0.1:1");input.Text="Remember the word cobalt.";Task plain=Ask(window);request=await server.GetContextAsync();body=await Read(request);Check(!body.Contains("image_url")&&body.Contains("Remember the word cobalt."),"Text-only EdgeRock chat uses EdgeRock even with delegated writing");await Reply(request,"Cobalt.");await plain;
                // Cancellation keeps the last answer and the unsent question available.
                input.Text="Cancel this follow-up.";Task cancelled=Ask(window);request=await server.GetContextAsync();await Read(request);window.CancelWork();HttpListenerContext control=await server.GetContextAsync();Check(control.Request.Url.AbsolutePath.StartsWith("/cancel/"),"Cancelling chat reaches the EdgeRock control endpoint");await Reply(control,"Cancelled");request.Response.Abort();await cancelled;
                Check(input.Text=="Cancel this follow-up."&&Field<ChatSession>(window,"conversation").Turns.Length==2&&transcript.Text.Contains("Cobalt."),"Cancelled chat preserves completed turns and the composer");
                input.Text="Read this chart.";Task failed=Ask(window);request=await server.GetContextAsync();await Read(request);byte[] failure=Encoding.UTF8.GetBytes("{\"error\":\"Not enough graphics memory. Select a smaller region.\"}");request.Response.StatusCode=503;request.Response.ContentLength64=failure.Length;await request.Response.OutputStream.WriteAsync(failure,0,failure.Length);request.Response.Close();await failed;
                Check(transcript.Controls.OfType<ChatMessageBubble>().Any(b=>b.AccessibleName.Contains("Not enough graphics memory"))&&input.Text=="Read this chart."&&Field<ChatSession>(window,"conversation").Turns.Length==2,"Model errors appear in the thread with their useful explanation and preserve the retry message");
                using(Bitmap image=new Bitmap(20,20))window.GetType().GetMethod("SetImage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{image});Check(Field<ChatSession>(window,"conversation").Turns.Length==0,"A replacement screen grab cannot inherit an unrelated image conversation");
                window.Close();
            }
            string fixture=Path.Combine(Path.GetTempPath(),"OpenMicAI-chat-navigation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
            try{Settings settings=new Settings();int creations=0;ImageQuestionWindow chat=null;using(HubWindow hub=new HubWindow(new HistoryStore(fixture),new PersonalizationStore(fixture),new UsageStore(fixture),()=>settings,next=>{},()=>new ModelWindow(fixture,settings,new ComputerSpecs()),new string[0],"")){
                hub.ImageQuestions=()=>{creations++;chat=new ImageQuestionWindow(()=>model,()=>true,()=>-1);return chat;};hub.Show();hub.Navigate("Chat");Field<ChatSession>(chat,"conversation").Complete("A test question","A test answer");hub.Navigate("Home");hub.Navigate("Chat");Check(creations==1&&!chat.IsDisposed&&Field<ChatSession>(chat,"conversation").Turns.Length==2,"Navigating away and back preserves the same in-memory chat");hub.Close();Check(chat.IsDisposed,"Closing the main window clears and disposes the chat");
            }}finally{Directory.Delete(fixture,true);}
            Set(model,"dedicatedDecision",true);Set(model,"decisionUrl",url);Set(model,"fastWritingUrl",url+"/fallback");
            MethodInfo choose=typeof(Models).GetMethod("ChoosePolishLayout",BindingFlags.Instance|BindingFlags.NonPublic);
            Task<string> routed=(Task<string>)choose.Invoke(model,new object[]{"Please buy apples and pears.",CancellationToken.None});HttpListenerContext decision=await server.GetContextAsync();string decisionBody=await Read(decision);
            Check(decisionBody.Contains("decision_criteria"),"Dedicated layout decisions receive the allowed choices and descriptions");
            byte[] uncertain=Encoding.UTF8.GetBytes("{\"decision_fallback\":true}");decision.Response.ContentLength64=uncertain.Length;await decision.Response.OutputStream.WriteAsync(uncertain,0,uncertain.Length);decision.Response.Close();
            Task<HttpListenerContext> receive=server.GetContextAsync();if(await Task.WhenAny(receive,Task.Delay(8000))!=receive){if(routed.IsFaulted)await routed;throw new Exception("The uncertain-decision fallback did not arrive.");}HttpListenerContext fallback=await receive;Check(fallback.Request.Url.AbsolutePath=="/fallback/v1/chat/completions","Uncertain dedicated decisions fall back to the language model rather than accepting an uncertain edit");await Reply(fallback,"{\"layout\":\"list\"}");Check(await routed=="list","The fallback result uses the same validated layout path");
            model.DecisionBudgetMilliseconds=30;routed=(Task<string>)choose.Invoke(model,new object[]{"Please buy apples and pears.",CancellationToken.None});decision=await server.GetContextAsync();await Read(decision);HttpListenerContext signal=await server.GetContextAsync();Check(signal.Request.Url.AbsolutePath.StartsWith("/cancel/"),"Slow dedicated decisions receive a cancellation signal");await Reply(signal,"Cancelled");fallback=await server.GetContextAsync();await Reply(fallback,"{\"layout\":\"list\"}");decision.Response.Abort();Check(await routed=="list","Slow dedicated decisions cannot hold the writing operation indefinitely");
            }finally{processes.Clear();server.Stop();}
        }
    }
    [STAThread]public static void Main(){Application.EnableVisualStyles();using(Form driver=new Form {ShowInTaskbar=false,Opacity=0}){driver.Shown+=async(s,e)=>{try{await Run();}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}finally{driver.Close();}};Application.Run(driver);}}
}
