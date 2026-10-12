using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalFlow;

public static class MediaChecks {
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Wave(string path,int seconds,bool sound) {
        int frames=44100*seconds;
        using(BinaryWriter writer=new BinaryWriter(File.Create(path))) {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+frames*4);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(44100);writer.Write(176400);writer.Write((short)4);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(frames*4);
            for(int i=0;i<frames;i++){short sample=sound?(short)(Math.Sin(i*440*2*Math.PI/44100)*6000):(short)0;writer.Write(sample);writer.Write(sample);}
        }
    }
    public static async Task Run(string root) {
        string path=Path.Combine(root,"media-test.wav");Wave(path,61,true);
        MediaChunk[] chunks=await Task.Run(()=>MediaFile.Read(path,CancellationToken.None).ToArray());
        Check(chunks.Length==3,"Long stereo file decodes in three bounded chunks");
        Check(Math.Abs(chunks.Last().Seconds-61)<.05,"Resampling preserves duration");
        Check(chunks.All(c=>c.Pcm.Length<=960000&&MediaFile.HasSound(c.Pcm)),"Chunks are 16 kHz mono PCM with sound");
        string aac=Path.Combine(root,"tone.m4a");using(Stream resource=typeof(MediaChecks).Assembly.GetManifestResourceStream("OpenMicAI.MediaTone"))using(StreamReader reader=new StreamReader(resource))File.WriteAllBytes(aac,Convert.FromBase64String(reader.ReadToEnd()));
        MediaChunk[] compressed=await Task.Run(()=>MediaFile.Read(aac,CancellationToken.None).ToArray());
        Check(compressed.Length==1&&compressed[0].Pcm.Length>0&&MediaFile.HasSound(compressed[0].Pcm),"AAC native-format announcements preserve negotiated PCM output");
        Check(!chunks[0].Overlap&&chunks[1].Overlap&&chunks[0].Pcm.Skip(chunks[0].Pcm.Length-32000).SequenceEqual(chunks[1].Pcm.Take(32000)),"Boundary overlap is preserved");
        Check(MediaText.Join("Please send the report tomorrow.","report tomorrow and call Sarah.",true)=="Please send the report tomorrow. and call Sarah.","Boundary duplicate words are removed");
        Check(MediaText.Join("Call Sarah","Sarah will reply",true)=="Call Sarah Sarah will reply","A single repeated word is not discarded");
        Check(MediaText.Join("Say hello","Say hello",false)=="Say hello Say hello","Repetitions outside an overlap are preserved");
        string source=String.Concat(Enumerable.Repeat("Please keep every sentence. ",1000));string[] parts=MediaText.Parts(source).ToArray();
        Check(parts.All(p=>p.Length<=4500)&&String.Join(" ",parts)==source.Trim(),"Long AI input is split without dropping words");
        int calls=0;string transcript=await MediaImportController.ReadTranscript(path,"Sarah",(pcm,vocabulary,token)=>{Check(vocabulary=="Sarah","Vocabulary reaches speech model");return Task.FromResult(++calls==1?"Please send the report tomorrow.":calls==2?"report tomorrow and call Sarah.":"call Sarah before noon.");},null,CancellationToken.None);
        Check(calls==3&&transcript=="Please send the report tomorrow. and call Sarah. before noon.","Streaming transcription joins all chunks");
        using(CancellationTokenSource cancel=new CancellationTokenSource()) {
            int seen=0;try{await MediaImportController.ReadTranscript(path,"",(pcm,vocabulary,token)=>{seen++;cancel.Cancel();token.ThrowIfCancellationRequested();return Task.FromResult("");},null,cancel.Token);throw new Exception("Cancellation was ignored");}catch(OperationCanceledException){}
            Check(seen==1,"Cancellation stops transcription and releases decoder");
        }
        string silence=Path.Combine(root,"silence.wav");Wave(silence,1,false);calls=0;
        string empty=await MediaImportController.ReadTranscript(silence,"",(pcm,vocabulary,token)=>{calls++;return Task.FromResult("hallucination");},null,CancellationToken.None);
        Check(empty.Length==0&&calls==0,"Silent media skips speech inference");
        try{MediaFile.ValidatePath("https://example.com/file.mp4");throw new Exception("Remote URL accepted");}catch(ArgumentException){}
        Check(true,"Remote media URLs are rejected");
        string invalid=Path.Combine(root,"invalid.wav");File.WriteAllText(invalid,"not media");bool rejected=false;
        try{await Task.Run(()=>MediaFile.Read(invalid,CancellationToken.None).ToArray());}catch(System.Runtime.InteropServices.COMException){rejected=true;}
        Check(rejected,"Invalid media produces an error");
        // No model, user audio, microphone, or desktop clipboard is used here.
        Settings prefs=new Settings {Style="Original transcript",Rewrite=false};int saved=0;MediaImportController controller=new MediaImportController {CanStart=()=>true,Preferences=()=>prefs,Rules=()=>new TextRules(new PersonalData()),Transcribe=(pcm,vocabulary,token)=>Task.FromResult("Local transcript."),Rewrite=(text,p,vocabulary,token)=>Task.FromResult(text),Save=(raw,result,mode)=>{saved++;Check(mode.StartsWith("Imported file · "),"File result is labelled in history");}};
        controller.Start(silence,false);await controller.Running;Check(!controller.Busy&&controller.Source==""&&saved==0,"Empty imports are not saved");
        controller.Start(path,false);await controller.Running;Check(!controller.Busy&&controller.Source==controller.Result&&saved==1,"Completed import remains available to copy");
        prefs.SaveHistory=false;controller.Start(path,false);await controller.Running;Check(saved==1,"History privacy preference is respected");
        int failCalls=0;controller.Transcribe=(pcm,vocabulary,token)=>{if(++failCalls==2)throw new InvalidOperationException("fixture failure");return Task.FromResult("Keep this partial result.");};controller.Start(path,false);await controller.Running;
        Check(!controller.Busy&&controller.Source=="Keep this partial result."&&controller.Result==controller.Source&&controller.Status.Contains("fixture failure"),"Failed imports retain the partial transcript");
        failCalls=0;controller.Transcribe=async(pcm,vocabulary,token)=>{if(++failCalls==2){controller.Cancel();await Task.Delay(1,token);}return "Keep the first chunk.";};controller.Start(path,false);await controller.Running;
        Check(!controller.Busy&&controller.Source=="Keep the first chunk."&&controller.Status=="Cancelled","Cancelled imports retain completed chunks");
        Wave(path,1,true);controller.Transcribe=(pcm,vocabulary,token)=>Task.FromResult("Please send the report tomorrow.");prefs.Rewrite=true;prefs.Style="Keep my wording";int rewrites=0;
        controller.Rewrite=(text,p,vocabulary,token)=>{rewrites++;return Task.FromResult("Please send the report tomorrow!");};controller.Start(path,true);await controller.Running;
        Check(rewrites==1&&controller.Result.EndsWith("!"),"Optional writing style reaches the selected AI pipeline");
        controller.Start(path,false);await controller.Running;Check(rewrites==1&&controller.Result==controller.Source,"Original-only import skips AI editing");
        controller.Rewrite=(text,p,vocabulary,token)=>{throw new InvalidOperationException("AI fixture failure");};controller.Start(path,true);await controller.Running;
        Check(controller.Result==controller.Source&&controller.Status.Contains("AI fixture failure"),"Failed AI editing keeps the original transcript");
        Console.WriteLine("PASS "+checks+" media import checks");
    }
}
