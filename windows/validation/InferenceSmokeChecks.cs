using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using LocalFlow;

public static class InferenceSmokeChecks {
    static readonly List<object> results=new List<object>();
    static async Task Check(string name,Func<Task<string>> action,Func<string,bool> validate){Stopwatch time=Stopwatch.StartNew();try{string output=await action();bool pass=validate(output);results.Add(new {name,seconds=time.Elapsed.TotalSeconds,output,pass});Console.WriteLine((pass?"PASS ":"FAIL ")+name+" "+time.Elapsed.TotalSeconds.ToString("0.00")+"s: "+output.Replace("\n"," | "));if(!pass)throw new Exception(name);}catch(Exception error){results.Add(new {name,seconds=time.Elapsed.TotalSeconds,error=error.Message,pass=false});throw;}}
    static async Task Run(string root,string language,bool fastWriting,string decision){
        using(Models models=new Models(root)){
            models.Progress=message=>Console.WriteLine(message);Stopwatch startup=Stopwatch.StartNew();await models.Boot("tdt-0.6b-v2-q8_0.gguf",language,fastWriting,decision);Console.WriteLine("Model load/warm-up "+startup.Elapsed.TotalSeconds.ToString("0.00")+"s");
            if(models.SupportsImages&&!fastWriting){
                int budget=models.WritingBudgetMilliseconds;models.WritingBudgetMilliseconds=50;
                try{await Check("slow-writing-recovery",()=>models.Rewrite("Please send Maya the blue folder before Friday. Do not include the old invoice.","Clean","",CancellationToken.None,"","Medium"),text=>text.Contains("Maya")&&text.Contains("Friday")&&text.Contains("blue folder")&&(text.IndexOf("not",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("don't",StringComparison.OrdinalIgnoreCase)>=0));}
                finally{models.WritingBudgetMilliseconds=budget;}
            }
            await Check("cleanup",()=>models.Rewrite("Please send Maya the blue folder before Friday. Do not include the old invoice.","Clean","",CancellationToken.None,"","Medium"),text=>text.Contains("Maya")&&text.Contains("Friday")&&text.Contains("blue folder")&&(text.IndexOf("not",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("don't",StringComparison.OrdinalIgnoreCase)>=0));
            await Check("selected-text-edit",()=>models.EditSelection("The meeting is Saturday. Bring the blue folder.","Change Saturday to Sunday.",CancellationToken.None),text=>text.Contains("Sunday")&&!text.Contains("Saturday")&&text.Contains("blue folder"));
            MethodInfo choose=typeof(Models).GetMethod("ChoosePolishLayout",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(string[] item in PolishChecks.AutomaticCases)await Check("layout-"+item[0],()=> (Task<string>)choose.Invoke(models,new object[]{item[1],CancellationToken.None}),text=>text==item[0]);
            if(models.SupportsImages){
                byte[] png;using(Bitmap image=new Bitmap(640,240)){using(Graphics draw=Graphics.FromImage(image)){draw.Clear(Color.White);using(Font font=new Font("Arial",52))draw.DrawString("OPEN MIC 42",font,Brushes.Black,30,70);}using(MemoryStream stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);png=stream.ToArray();}}
                for(int repeat=0;repeat<2;repeat++)await Check("image-reading-"+repeat,()=>models.AskImage(png,"Read the exact text and number in the picture. Answer briefly.",CancellationToken.None),text=>text.Contains("42")&&text.ToUpperInvariant().Contains("MIC"));
                ChatSession chat=new ChatSession();string initial="Read the text and number in this image. Keep it brief.";
                await Check("image-chat",async()=>{string reply=await models.Chat(chat.Request(initial),png,CancellationToken.None);chat.Complete(initial,reply);return reply;},text=>text.Contains("42"));
                await Check("image-chat-follow-up",()=>models.Chat(chat.Request("What is that number plus one? Answer with the number only."),png,CancellationToken.None),text=>text.Contains("43"));
                string speech=Path.Combine(root,"work","speech-fixture.pcm");
                if(File.Exists(speech))await Check("spoken-image-question",async()=>{string question=await models.Transcribe(File.ReadAllBytes(speech),"",CancellationToken.None);Console.WriteLine("Synthetic speech transcript: "+question);return await models.AskImage(png,question,CancellationToken.None);},text=>text.Contains("42"));
                using(CancellationTokenSource cancel=new CancellationTokenSource()){
                    Task<string> abandoned=models.AskImage(png,"Describe every visible detail at length.",cancel.Token);await Task.Delay(100);cancel.Cancel();bool cancelled=false;try{await abandoned;}catch(OperationCanceledException){cancelled=true;}if(!cancelled)throw new Exception("Image request did not cancel");Console.WriteLine("PASS image generation cancellation");
                }
                Stopwatch resumed=Stopwatch.StartNew();await Check("image-after-cancel",()=>models.AskImage(png,"What is the number?",CancellationToken.None),text=>text.Contains("42")&&resumed.Elapsed.TotalSeconds<8);
            }
        }
    }
    public static void Main(string[] args){bool pass=false;bool fast=args.Length>3&&args[3]=="fast";string decision=args.Length>4?args[4]:"auto";try{Run(args[0],args[1],fast,decision).GetAwaiter().GetResult();pass=true;}catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}finally{File.WriteAllText(args[2],new JavaScriptSerializer().Serialize(new {complete=pass,model=args[1],decision,fastWriting=fast,results}));}}
}
