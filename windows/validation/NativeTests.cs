ï»¿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using LocalFlow;

public class Tests {
    static string root, reportFile, textModel;
    static Dictionary<string,object> result=new Dictionary<string,object>();
    static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);result[label]=true;Console.WriteLine("PASS "+label);}
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr handle,int command);
    [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]static extern bool AttachThreadInput(uint from,uint to,bool attach);
    static void ActivateFixture(Form form) {
        uint pid;uint foreground=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid),current=GetCurrentThreadId();bool attached=false;
        try{if(current!=foreground)attached=AttachThreadInput(current,foreground,true);ShowWindow(form.Handle,5);form.Activate();SetForegroundWindow(form.Handle);}
        finally{if(attached)AttachThreadInput(current,foreground,false);}
    }
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr handle,uint message,IntPtr w,IntPtr l);
    [STAThread]public static void Main(string[] args) {
        root=args[0];reportFile=args[1];textModel=args.Length>2?args[2]:"Qwen3.5-2B-Q4_K_M.gguf";
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        Form form=new Form {Text="LocalFlow verification",ClientSize=new Size(500,270),StartPosition=FormStartPosition.CenterScreen};
        TextBox first=new TextBox {Location=new Point(20,20),Width=450,Height=70,Multiline=true};
        TextBox second=new TextBox {Location=new Point(20,110),Width=450,Height=70,Multiline=true};form.Controls.Add(first);form.Controls.Add(second);
        TextBox password=new TextBox {Location=new Point(20,210),Width=450,UseSystemPasswordChar=true};form.Controls.Add(password);
        form.Shown+=async(s,e)=>{
            try {
                result["fixture_path"]=reportFile;await LibraryChecks.Run(root,result,Check);
                byte[] wav=Wav.Encode(new byte[32000]);Check(wav.Length==32044&&BitConverter.ToInt32(wav,24)==16000,"PCM WAV encoding");
                Utterances segments=new Utterances();List<byte[]> clips=new List<byte[]>();segments.Submit=clips.Add;
                byte[] sound=new byte[1280];for(int i=0;i<sound.Length;i+=2){sound[i]=0x00;sound[i+1]=0x10;}
                for(int i=0;i<8;i++)segments.Push(sound);for(int i=0;i<12;i++)segments.Push(new byte[1280]);segments.Finish();
                Check(clips.Count==1&&clips[0].Length>8*1280,"Silence segmentation preserves speech");
                PreparedText prepared=Formatting.Prepare("for internal use only close parentheses question mark");Check(prepared.Text.EndsWith(")?"),"Spoken punctuation");
                Check(Formatting.Prepare("write the literal words new paragraph inside quotation marks").Text.Contains("new paragraph"),"Literal command words preserved");
                Check(Microphone.Names().Count>1,"Microphone devices available");
                // Open and prepare the microphone without starting capture.
                Stopwatch micWatch=Stopwatch.StartNew();using(Microphone mic=new Microphone(-1)){result["microphone_prepare_ms"]=micWatch.Elapsed.TotalMilliseconds;double firstFrame=-1;mic.Audio=pcm=>{if(firstFrame<0)firstFrame=micWatch.Elapsed.TotalMilliseconds;};mic.Start();await Task.Delay(1200);mic.Stop();result["microphone_test_bytes"]=mic.TotalBytes;result["first_microphone_callback_ms"]=firstFrame;Check(mic.TotalBytes>0,"Microphone callback receives PCM");}
                using(ShortcutWindow shortcut=new ShortcutWindow()) {
                    Check(shortcut.Register("Ctrl + Shift + Space"),"Global shortcut registered");int hits=0;shortcut.Toggle=()=>hits++;
                    PostMessage(shortcut.Handle,0x0312,new IntPtr(19526),IntPtr.Zero);await Task.Delay(100);Check(hits==1,"Shortcut dispatch");result["shortcut"]=shortcut.Label;
                    shortcut.RegisterEdit();Check(shortcut.EditLabel!=null,"Voice edit shortcut registered");int edits=0;shortcut.Edit=()=>edits++;PostMessage(shortcut.Handle,0x0312,new IntPtr(19528),IntPtr.Zero);await Task.Delay(100);Check(edits==1,"Voice edit shortcut dispatch");
                }
                form.TopMost=true;ActivateFixture(form);bool firstFocus=first.Focus();await Task.Delay(250);
                result["fixture_window"]=new {handle=form.Handle.ToInt64(),visible=form.Visible,enabled=form.Enabled};
                FocusTarget target=FocusTarget.Capture();result["initial_focus"]=new {focus_return=firstFocus,window=target.Window.ToInt64(),control=target.Control.ToInt64(),first=first.Handle.ToInt64(),visible=first.Visible,enabled=first.Enabled};Check(target.Control==first.Handle&&target.MatchesCurrent(),"Focused field captured");
                using(Pill pill=new Pill()){pill.Prepare("Listening",true);pill.Place(form.Handle);pill.Reveal();await Task.Delay(100);FocusTarget now=FocusTarget.Capture();result["focus_before_indicator"]=new {window=target.Window.ToInt64(),control=target.Control.ToInt64(),id=target.RuntimeId};result["focus_after_indicator"]=new {window=now.Window.ToInt64(),control=now.Control.ToInt64(),id=now.RuntimeId};Check(target.MatchesCurrent(),"Recording indicator does not steal focus");int clicks=0;pill.Cancel=()=>clicks++;PostMessage(pill.Handle,0x201,new IntPtr(1),new IntPtr((25<<16)|(pill.Width-15)));PostMessage(pill.Handle,0x202,IntPtr.Zero,new IntPtr((25<<16)|(pill.Width-15)));await Task.Delay(100);Check(clicks==1,"Indicator cancel click");pill.Conceal();}
                form.Activate();bool switched=second.Focus();await Task.Delay(250);FocusTarget afterSecond=FocusTarget.Capture();result["field_switch"]=new {focus_return=switched,window=afterSecond.Window.ToInt64(),control=afterSecond.Control.ToInt64(),first=first.Handle.ToInt64(),second=second.Handle.ToInt64(),id=afterSecond.RuntimeId};Check(!target.MatchesCurrent(),"Different field in same window blocks paste");
                ActivateFixture(form);password.Focus();await Task.Delay(150);FocusTarget passwordTarget=FocusTarget.Capture();result["password_focus"]=new {window=passwordTarget.Window.ToInt64(),control=passwordTarget.Control.ToInt64(),expected=password.Handle.ToInt64(),password=passwordTarget.IsPassword};Check(passwordTarget.Control==password.Handle&&passwordTarget.IsPassword,"Password field detected");
                first.Focus();await Task.Delay(100);target=FocusTarget.Capture();IDataObject original=Clipboard.GetDataObject();DataObject saved=new DataObject();if(original!=null)foreach(string format in original.GetFormats(false))try{saved.SetData(format,original.GetData(format,false));}catch{}Clipboard.SetText("LocalFlow clipboard sentinel");
                await FlowContext.Paste("Verified local dictation.",target,CancellationToken.None);Check(first.Text=="Verified local dictation.","Native paste into text field");Check(Clipboard.GetText()=="LocalFlow clipboard sentinel","Clipboard restored after paste");
                Clipboard.SetDataObject(saved,true);
                first.Text="Before. The meeting is on Saturday. After.";first.Select(8,"The meeting is on Saturday.".Length);target=FocusTarget.Capture();
                string selection=target.CaptureSelection();result["captured_selection"]=selection;Check(selection=="The meeting is on Saturday."&&target.SelectionMatches(),"Native selected text captured without clipboard");
                first.Select(0,7);Check(!target.SelectionMatches(),"Changed selection blocks replacement");
                first.Select(8,"The meeting is on Saturday.".Length);target=FocusTarget.Capture();target.CaptureSelection();
                await FlowContext.Paste("The meeting is on Sunday.",target,CancellationToken.None,true);
                Check(first.Text=="Before. The meeting is on Sunday. After.","Voice edit paste replaces only selected text");
                int[] childIds=null;
                using(Models models=new Models(root)) {
                    models.Progress=t=>Console.WriteLine(t);Stopwatch boot=Stopwatch.StartNew();await models.Boot("tdt-0.6b-v2-q8_0.gguf",textModel);result["cold_start_seconds"]=boot.Elapsed.TotalSeconds;Check(models.Ready,"Both resident models ready");
                    byte[] publicWav=File.ReadAllBytes(Path.Combine(root,"validation","speech-fixture.wav"));byte[] pcm=new byte[publicWav.Length-44];Array.Copy(publicWav,44,pcm,0,pcm.Length);
                    List<object> latency=new List<object>();
                    for(int round=0;round<5;round++) {
                        Stopwatch watch=Stopwatch.StartNew();string raw=await models.Transcribe(pcm,"",CancellationToken.None);double speech=watch.Elapsed.TotalSeconds;
                        string polished=await models.Rewrite(raw,"Clean","",CancellationToken.None);double total=watch.Elapsed.TotalSeconds;
                        latency.Add(new {speech=speech,total=total,raw=raw,output=polished});
                        Check(raw.ToLowerInvariant().Contains("budget")&&raw.ToLowerInvariant().Contains("sarah"),"Speech and rewrite round "+round);
                    }
                    result["warm_pipeline"]=latency;
                    string[] cases={
                        "um hey Sarah so please send the proposal tomorrow morning and include the budget thanks",
                        "our meeting is on Friday sorry I mean Saturday at two pm make it a short friendly message",
                        "the budget is five thousand dollars no sorry six thousand dollars and the deadline is June fifteenth",
                        "send this to Alex no actually send it to Priya tomorrow morning",
                        "we need to get milk eggs and bread make it a bullet list",
                        "first check the logs then restart the server then verify the website make it a numbered list",
                        "hello Maya new paragraph thanks for sending the contract new paragraph I will review it tomorrow best Daniel",
                        "can you please tell me when the shipment will arrive",
                        "the access code is 7319 and the order number is AB 2048",
                        "the appointment is at three thirty pm on Tuesday October twentieth",
                        "I do not want to cancel the subscription please keep it active",
                        "hey Tom I cannot make the meeting today because I am feeling sick make this professional",
                        "take out the trash walk the dog and call the dentist put each task on a separate line",
                        "we agreed on twelve units at nineteen dollars and ninety five cents each",
                        "write the literal words new paragraph inside quotation marks",
                        "this is urgent do not deploy version two point four until the bug is fixed",
                        "I loved the design but the navigation was confusing keep this casual",
                        "open parentheses for internal use only close parentheses the launch date is November first",
                        "my name is Siobhan and the company is Anthropic",
                        "I will attend the workshop no I will not attend the workshop",
                        "please create a title called weekly update then a paragraph saying the launch is on track then a bullet list with design complete testing ongoing and release on Monday",
                        "let me know if this works question mark",
                        "please send the invoice to Jordan by Wednesday the total is 248 dollars",
                        "we need three red folders and two blue folders no make that four blue folders",
                        "I never said she approved the change please do not tell the team that she did",
                        "meet me at six pm sorry I meant eight pm on Wednesday",
                        "reserve nine seats no make that seven seats for the team",
                        "dear Olivia new paragraph I have attached the report new paragraph please review it by Thursday make this professional",
                        "the build number is 90317 and the confirmation code is XK 5186",
                        "please do not buy the tickets until I confirm the date"
                    };
                    List<object> outputs=new List<object>();List<string> rewrites=new List<string>();
                    foreach(string input in cases) {Stopwatch watch=Stopwatch.StartNew();string output=await models.Rewrite(input,"Clean","",CancellationToken.None);rewrites.Add(output);outputs.Add(new {input=input,output=output,seconds=watch.Elapsed.TotalSeconds});Console.WriteLine(input+" => "+output.Replace("\n"," | "));}
                    result["rewrite_checks"]=outputs;
                    List<string> failures=new List<string>();
                    Action<bool,string> verify=(passed,name)=>{if(!passed)failures.Add(name);};
                    verify(rewrites[1].Contains("Saturday")&&!rewrites[1].Contains("Friday"),"Corrected day");
                    verify(!rewrites[2].Contains("five thousand")&&!rewrites[2].Contains("5,000"),"Corrected budget");
                    verify(rewrites[3].Contains("Priya")&&!rewrites[3].Contains("Alex"),"Corrected recipient");
                    verify(rewrites[4].Split('\n').Length==3,"Bullet list");
                    verify(rewrites[5].Contains("1.")&&rewrites[5].Contains("3."),"Numbered list");
                    verify(rewrites[6].Contains("\n\n")&&rewrites[6].Contains("Maya")&&rewrites[6].Contains("Daniel"),"Paragraphs and names");
                    string[] email=rewrites[6].Split(new string[]{"\n\n"},StringSplitOptions.None);verify(email.Length>=3&&email[0].Contains("Maya")&&!email[0].Contains("contract")&&email[1].Contains("contract")&&!email[1].Contains("review")&&email[2].Contains("review"),"Requested paragraph placement");
                    verify(rewrites[7].EndsWith("?")&&!rewrites[7].Contains("Friday"),"Question not answered");
                    verify(rewrites[8].Contains("7319")&&rewrites[8].Contains("2048"),"Codes preserved");
                    verify(rewrites[9].Contains("Tuesday")&&(rewrites[9].Contains("20")||rewrites[9].Contains("twentieth")),"Date preserved");
                    verify(rewrites[10].Contains("not want to cancel"),"Negation preserved");
                    verify(rewrites[11].Contains("Tom"),"Professional greeting preserved");
                    verify(rewrites[12].Split('\n').Length==3,"Separate tasks");
                    verify(rewrites[14]=="\"new paragraph\"","Literal quote");
                    verify(!rewrites[16].ToLowerInvariant().Contains("keep this casual"),"Tone instruction removed");
                    verify(rewrites[17].Contains("(")&&rewrites[17].Contains(")")&&rewrites[17].ToLowerInvariant().Contains("internal use only"),"Parentheses preserved");
                    verify(rewrites[18].Contains("Siobhan")&&rewrites[18].Contains("Anthropic"),"Names preserved");
                    verify(!rewrites[19].ToLowerInvariant().Contains("will attend"),"Corrected attendance");
                    verify(rewrites[20].Contains("Weekly Update")&&rewrites[20].Contains("Monday"),"Heading and list");
                    verify(rewrites[21].EndsWith("?"),"Spoken question mark");
                    verify(!rewrites[23].Contains("two blue")&&!rewrites[23].Contains("2 blue")&&(rewrites[23].Contains("four blue")||rewrites[23].Contains("4 blue")),"Corrected item count");
                    verify(rewrites[25].Contains("Wednesday")&&!rewrites[25].Contains("6 PM")&&!rewrites[25].Contains("six"),"Unseen time correction");
                    verify(!rewrites[26].Contains("nine")&&!rewrites[26].Contains("9")&&(rewrites[26].Contains("seven")||rewrites[26].Contains("7")),"Unseen quantity correction");
                    verify(rewrites[27].Contains("Olivia")&&rewrites[27].Contains("Thursday")&&rewrites[27].Contains("\n\n"),"Unseen email formatting");
                    string[] letter=rewrites[27].Split(new string[]{"\n\n"},StringSplitOptions.None);verify(letter.Length==3&&!letter[0].Contains("attached")&&letter[1].Contains("attached")&&letter[2].Contains("Thursday"),"Unseen paragraph placement");
                    verify(rewrites[28].Contains("90317")&&rewrites[28].Contains("5186"),"Unseen identifiers");
                    verify(rewrites[29].ToLowerInvariant().Contains("not buy"),"Unseen negation");
                    result["rewrite_failures"]=failures;
                    Check(failures.Count==0,"All semantic and formatting regressions");
                    List<object> styles=new List<object>();
                    foreach(string style in new[]{"Professional","Concise","Bullet points","Custom"}) {
                        string output=await models.Rewrite("hey Morgan please check the budget and approve the timeline by Monday","Custom"==style?"Custom":style,"Use a friendly tone with two short sentences.",CancellationToken.None);
                        styles.Add(new {style=style,output=output});Check(output.Contains("Morgan")&&output.Contains("Monday"),"Facts retained in "+style+" style");
                        if(style=="Bullet points")Check(output.TrimStart().StartsWith("- ")||output.TrimStart().StartsWith("* "),"Bullet style produces list");
                    }
                    result["styles"]=styles;
                    List<object> expanded=new List<object>();
                    foreach(string style in new[]{"Keep my wording","Smart formatting","Friendly","Casual","Email","Numbered list","Meeting notes","Original transcript"}) {
                        string input="hey Morgan please check the budget and approve the timeline by Monday";
                        Stopwatch time=Stopwatch.StartNew();string output=await models.Rewrite(input,style,"",CancellationToken.None);expanded.Add(new {style=style,input=input,output=output,seconds=time.Elapsed.TotalSeconds});
                        Check(output.Contains("Morgan")&&output.Contains("Monday"),"Names and deadline retained in "+style);
                        Check(!output.Contains("Organize this")&&!output.Contains("I dictated")&&!output.Contains("without adding any facts"),"Writing instructions not echoed in "+style);
                        if(style=="Keep my wording")Check(output.ToLowerInvariant().Contains("please check the budget")&&output.ToLowerInvariant().Contains("approve the timeline"),"Keep my wording preserves phrasing");
                        if(style=="Numbered list")Check(output.Contains("1.")&&output.Contains("2."),"Numbered preset produces steps");
                        if(style=="Original transcript")Check(output==input,"Original transcript bypasses AI");
                    }
                    result["expanded_styles"]=expanded;
                    string infer=await models.Rewrite("tasks for today buy milk pick up the prescription and call Nina about the invoice","Smart formatting","",CancellationToken.None);result["inferred_format"]=infer;Check(infer.Contains("Nina")&&infer.Contains("invoice")&&infer.Contains("\n"),"Formatting inferred from spoken task intent");
                    List<object> voiceEdits=new List<object>();result["voice_edits"]=voiceEdits;
                    foreach(string command in new[]{"Change Saturday to Sunday and blue to green.","Make this shorter and more direct.","Make this a bullet list.","Make this sound more professional."}) {
                        string source="Hey Priya, our meeting is on Saturday at 2 PM. Please bring the blue folder. The access code is 7319.";
                        Stopwatch time=Stopwatch.StartNew();string output=await models.EditSelection(source,command,CancellationToken.None);voiceEdits.Add(new {source=source,command=command,output=output,seconds=time.Elapsed.TotalSeconds});
                        Console.WriteLine(command+" => "+output.Replace("\n"," | "));
                        Check(output.Contains("Priya")&&output.Contains("7319")&&System.Text.RegularExpressions.Regex.IsMatch(output,@"2(?::00)?\s*PM",System.Text.RegularExpressions.RegexOptions.IgnoreCase)&&!output.Contains("[Your Name]"),"Voice edit retains facts: "+command);
                        if(command.StartsWith("Change"))Check(output.Contains("Sunday")&&output.Contains("green")&&!output.Contains("Saturday")&&!output.Contains("blue"),"Natural spoken word replacements");
                        if(command.Contains("bullet"))Check(output.Contains("\n- "),"Voice edit formats selected text as list");
                    }
                    result["voice_edits"]=voiceEdits;
                    Check(!await models.MeaningPreserved("I cannot attend because I am sick.","I cannot attend because I am sick. I will send notes tomorrow.",CancellationToken.None),"Invented promise rejected");
                    Check(await models.MeaningPreserved("Our meeting is Friday, sorry, Saturday.","Our meeting is Saturday.",CancellationToken.None),"Legitimate correction accepted by fact check");
                    List<Process> children=(List<Process>)typeof(Models).GetField("processes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(models);childIds=children.Select(p=>p.Id).ToArray();
                }
                bool exited=true;foreach(int id in childIds)try{using(Process p=Process.GetProcessById(id)){if(!p.HasExited)exited=false;}}catch(ArgumentException){}Check(exited,"Model helpers exit with app");
                LibraryChecks.Render(root);result["complete"]=true;
            }catch(Exception error){result["error"]=error.ToString();Console.WriteLine(error);Environment.ExitCode=1;}
            finally{File.WriteAllText(reportFile,new JavaScriptSerializer().Serialize(result));form.Close();}
        };
        Application.Run(form);
    }
}
