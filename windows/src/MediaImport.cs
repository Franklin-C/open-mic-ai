using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LocalFlow {
public static class MediaText {
    public static string Join(string previous,string next,bool overlap) {
        next=(next??"").Trim();if(String.IsNullOrWhiteSpace(previous))return next;if(next.Length==0)return previous;
        if(overlap) {
            MatchCollection a=Regex.Matches(previous,@"[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*"),b=Regex.Matches(next,@"[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*");
            // A one-second overlap cannot normally contain more than 12 words.
            for(int n=Math.Min(12,Math.Min(a.Count,b.Count));n>=2;n--) {
                bool same=true;for(int i=0;i<n;i++)if(!String.Equals(a[a.Count-n+i].Value.Replace('’','\''),b[i].Value.Replace('’','\''),StringComparison.OrdinalIgnoreCase)){same=false;break;}
                if(same){next=next.Substring(b[n-1].Index+b[n-1].Length).TrimStart(' ','.',',','!','?',':',';','\r','\n');break;}
            }
        }
        return next.Length==0?previous:previous.TrimEnd()+" "+next;
    }
    public static IEnumerable<string> Parts(string text) {
        while(text.Length>4500) {
            int split=text.LastIndexOfAny(new[]{'\n','.','!','?'},4499,2200);if(split<2300)split=text.LastIndexOf(' ',4499);if(split<1)split=4500;else split++;
            yield return text.Substring(0,split).Trim();text=text.Substring(split).TrimStart();
        }
        if(text.Trim().Length>0)yield return text.Trim();
    }
}
public sealed class MediaImportController {
    public Func<bool> CanStart=()=>false;
    public Func<Settings> Preferences;
    public Func<TextRules> Rules;
    public Func<byte[],string,CancellationToken,Task<string>> Transcribe;
    public Func<string,Settings,string,CancellationToken,Task<string>> Rewrite;
    public Action<string,string,string> Save;
    public event Action Changed;
    public bool Busy {get;private set;}
    public string FileName="",Source="",Result="",Status="";
    CancellationTokenSource cancel;
    public Task Running {get;private set;}
    void Signal(){Action changed=Changed;if(changed!=null)changed();}
    public void Cancel(){if(cancel!=null)cancel.Cancel();}
    public void Start(string path,bool edit) {
        if(Busy)return;
        if(!CanStart()){Status="Finish the current action and load models in Settings first.";Signal();return;}
        try {path=MediaFile.ValidatePath(path);}catch(Exception error){Status=error.Message;Signal();return;}
        Settings prefs=Preferences();TextRules rules=Rules();string vocabulary=rules.Guidance(prefs.Vocabulary);
        FileName=Path.GetFileName(path);Source="";Result="";Status="Reading audio…";Busy=true;cancel=new CancellationTokenSource();Signal();
        Running=Run(path,edit,prefs,rules,vocabulary,cancel);
    }
    async Task Run(string path,bool edit,Settings prefs,TextRules rules,string vocabulary,CancellationTokenSource cancellation) {
        CancellationToken token=cancellation.Token;bool complete=false;string finalStatus="";
        string partial="";bool reading=true;
        Progress<Tuple<string,double>> display=new Progress<Tuple<string,double>>(value=>{if(cancel!=cancellation||!reading)return;Source=value.Item1;Result=Source;Status="Transcribing · "+TimeSpan.FromSeconds(value.Item2).ToString(@"hh\:mm\:ss");Signal();});
        ActionProgress<Tuple<string,double>> progress=new ActionProgress<Tuple<string,double>>(value=>{partial=value.Item1;((IProgress<Tuple<string,double>>)display).Report(value);});
        try {
            string source=await ReadTranscript(path,vocabulary,Transcribe,progress,token);reading=false;Source=source;Result=source;
            if(String.IsNullOrWhiteSpace(source)){finalStatus="No speech found.";return;}
            if(edit&&prefs.Rewrite&&WritingStyles.Normalize(prefs.Style)!="Original transcript") {
                Status="Applying "+StyleOptions.Selected(prefs)+"…";Signal();List<string> edited=new List<string>();
                foreach(string part in MediaText.Parts(rules.Correct(source))){token.ThrowIfCancellationRequested();edited.Add(await Rewrite(part,prefs,vocabulary,token));}
                Result=String.Join("\n\n",edited);
            }
            token.ThrowIfCancellationRequested();complete=true;finalStatus="Ready to copy";
        } catch(OperationCanceledException){finalStatus="Cancelled";Source=partial;Result=Source;}
        catch(Exception error){finalStatus="Could not finish: "+error.Message;Source=partial;Result=Source;}
        finally {
            reading=false;
            if(!String.IsNullOrWhiteSpace(Source)&&Preferences().SaveHistory)Save(Source,Result,"Imported file"+(complete?"":" (partial)")+" · "+FileName);
            Status=finalStatus;Busy=false;cancel=null;cancellation.Dispose();Signal();
        }
    }
    public static async Task<string> ReadTranscript(string path,string vocabulary,Func<byte[],string,CancellationToken,Task<string>> transcribe,IProgress<Tuple<string,double>> progress,CancellationToken token) {
        using(CancellationTokenSource lifetime=CancellationTokenSource.CreateLinkedTokenSource(token))
        using(BlockingCollection<MediaChunk> queue=new BlockingCollection<MediaChunk>(2)) {
            Task producer=Task.Run(()=>{try{foreach(MediaChunk chunk in MediaFile.Read(path,lifetime.Token))queue.Add(chunk,lifetime.Token);}finally{queue.CompleteAdding();}},lifetime.Token);
            string result=null;Exception failure=null;
            try {
                result=await Task.Run(async()=> {
                    string text="";
                    foreach(MediaChunk chunk in queue.GetConsumingEnumerable(lifetime.Token)) {
                        lifetime.Token.ThrowIfCancellationRequested();if(MediaFile.HasSound(chunk.Pcm))text=MediaText.Join(text,await transcribe(chunk.Pcm,vocabulary,lifetime.Token).ConfigureAwait(false),chunk.Overlap);
                        if(text.Length>1000000)throw new InvalidOperationException("Transcript is too long. Import a shorter file.");
                        if(progress!=null)progress.Report(Tuple.Create(text,chunk.Seconds));
                    }
                    await producer.ConfigureAwait(false);return text;
                },lifetime.Token);
            } catch(Exception error){failure=error;}
            lifetime.Cancel();try{await producer;}catch(Exception error){if(failure==null)failure=error;}
            if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();return result;
        }
    }
}
public sealed class ActionProgress<T> : IProgress<T> {
    readonly Action<T> report;public ActionProgress(Action<T> action){report=action;}public void Report(T value){report(value);}
}
public sealed class MediaImportPanel : Panel {
    readonly MediaImportController importer;
    readonly Button choose=Theme.Button("Import file",true),stop=Theme.Button("Cancel"),copy=Theme.Button("Copy"),original=Theme.Button("Original");
    readonly RedCheck edit=new RedCheck {Text="Use writing style",AutoSize=true,Checked=true};
    readonly Label status=new Label {AutoEllipsis=true,ForeColor=Theme.Muted};
    readonly TextBox result=new TextBox {ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Tag="history-reader",BorderStyle=BorderStyle.None,BackColor=Color.White};
    bool raw;
    public Action<int> HeightNeeded;
    public int DesiredHeight {get{return importer.Source.Length>0?180:94;}}
    public MediaImportPanel(MediaImportController controller) {
        importer=controller;Dock=DockStyle.Fill;Margin=Padding.Empty;BackColor=Color.White;AllowDrop=true;
        Controls.AddRange(new Control[]{choose,stop,copy,original,edit,status,result});
        choose.Click+=(s,e)=>{using(OpenFileDialog dialog=new OpenFileDialog {Title="Import audio or video",Filter="Audio and video|*.wav;*.mp3;*.m4a;*.aac;*.mp4;*.mov;*.wma;*.wmv;*.flac;*.aif;*.aiff|All files|*.*",CheckFileExists=true})if(dialog.ShowDialog(FindForm())==DialogResult.OK){raw=false;importer.Start(dialog.FileName,edit.Checked);}};
        stop.Click+=(s,e)=>importer.Cancel();original.Click+=(s,e)=>{raw=!raw;RefreshState();};
        copy.Click+=(s,e)=>{try{TextClipboard.SetText(result.Text);copy.Text="Copied";}catch(System.Runtime.InteropServices.ExternalException){status.Text="Clipboard busy. Try again.";}};
        DragEnter+=(s,e)=>{if(!importer.Busy&&e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};
        DragDrop+=(s,e)=>{string[] files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length==1){raw=false;importer.Start(files[0],edit.Checked);}else status.Text="Import one file at a time.";};
        importer.Changed+=RefreshState;SizeChanged+=(s,e)=>Arrange();RefreshState();
    }
    void RefreshState(){if(IsDisposed)return;choose.Enabled=edit.Enabled=!importer.Busy;stop.Visible=importer.Busy;copy.Visible=original.Visible=importer.Source.Length>0;copy.Text="Copy";original.Text=raw?"Result":"Original";result.Text=raw?importer.Source:importer.Result;status.Text=(importer.FileName.Length>0?importer.FileName+" · ":"Drop audio or video here · ")+importer.Status;if(HeightNeeded!=null)HeightNeeded(DesiredHeight);Arrange();}
    void Arrange(){choose.SetBounds(0,0,116,36);edit.SetBounds(128,7,156,24);stop.SetBounds(290,0,88,36);copy.SetBounds(Math.Max(390,Width-176),0,78,36);original.SetBounds(Math.Max(478,Width-90),0,90,36);status.SetBounds(0,43,Width,24);result.Visible=result.Text.Length>0;result.SetBounds(0,76,Width,88);}
    protected override void Dispose(bool disposing){if(disposing)importer.Changed-=RefreshState;base.Dispose(disposing);}
}
}
