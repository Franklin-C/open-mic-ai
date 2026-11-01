using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LocalFlow {
public static class SelectedImageData {
    public static bool IsImageFragment(IDataObject data){
        if(data==null||!data.GetDataPresent(DataFormats.Html))return false;
        string html=data.GetData(DataFormats.Html) as string;if(html==null)return false;
        Match selection=Regex.Match(html,@"<!--StartFragment-->([\s\S]*?)<!--EndFragment-->",RegexOptions.IgnoreCase);if(!selection.Success)return false;
        string fragment=selection.Groups[1].Value;
        return Regex.IsMatch(fragment,@"<img\b",RegexOptions.IgnoreCase)&&String.IsNullOrWhiteSpace(Regex.Replace(fragment,@"<[^>]*>|&(?:nbsp|#160);","",RegexOptions.IgnoreCase));
    }
    public static async Task<byte[]> CaptureRegion(CancellationToken cancel,Form owner=null){
        bool restore=owner!=null&&owner.Visible;Rectangle desktop=SystemInformation.VirtualScreen;
        try{
            if(restore)owner.Hide();await Task.Delay(200,cancel);
            using(Bitmap screen=new Bitmap(desktop.Width,desktop.Height)){
                using(Graphics draw=Graphics.FromImage(screen))draw.CopyFromScreen(desktop.Location,Point.Empty,desktop.Size);
                using(RegionPicker picker=new RegionPicker(screen,desktop)){
                    using(cancel.Register(()=>{if(picker.IsHandleCreated&&!picker.IsDisposed)try{picker.BeginInvoke((Action)(()=>picker.Close()));}catch(InvalidOperationException){}})){
                        cancel.ThrowIfCancellationRequested();if(picker.ShowDialog()!=DialogResult.OK)return null;cancel.ThrowIfCancellationRequested();
                        using(Bitmap crop=screen.Clone(picker.Selection,PixelFormat.Format32bppArgb))using(MemoryStream output=new MemoryStream()){
                            if(crop.Width*(long)crop.Height>16000000)throw new InvalidOperationException("Select a smaller region (at most 16 million pixels).");
                            crop.Save(output,ImageFormat.Png);if(output.Length>8*1024*1024)throw new InvalidOperationException("Select a smaller image region.");return output.ToArray();
                        }
                    }
                }
            }
        }finally{if(restore&&!owner.IsDisposed){owner.Show();owner.Activate();}}
    }
    public static byte[] FromClipboard(IDataObject data){
        if(data==null)return null;
        Image image=null;bool owned=false;
        try{
            if(data.GetDataPresent(DataFormats.Bitmap))image=data.GetData(DataFormats.Bitmap) as Image;
            if(image==null&&data.GetDataPresent("PNG")){
                MemoryStream stream=data.GetData("PNG") as MemoryStream;
                if(stream!=null&&stream.Length<=8*1024*1024){long position=stream.Position;try{stream.Position=0;using(Image decoded=Image.FromStream(stream)){image=new Bitmap(decoded);owned=true;}}finally{stream.Position=position;}}
            }
            if(image==null||image.Width*(long)image.Height>16000000)return null;
            using(MemoryStream output=new MemoryStream()){image.Save(output,ImageFormat.Png);return output.Length<=8*1024*1024?output.ToArray():null;}
        }catch(ArgumentException){return null;}catch(System.Runtime.InteropServices.ExternalException){return null;}
        finally{if(owned&&image!=null)image.Dispose();}
        // Clipboard-owned bitmaps are not disposed here; the caller restores the clipboard.
    }
}
public sealed class ImageQuestionWindow : Form {
    readonly Func<Models> models;readonly Func<bool> canStart;readonly Func<int> microphone;
    readonly PictureBox preview=new PictureBox {Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Theme.Surface,AccessibleName="Selected image"};
    readonly TextBox question=new TextBox {Multiline=true,Dock=DockStyle.Fill,MaxLength=4000,ScrollBars=ScrollBars.Vertical,AccessibleName="Chat message"};
    readonly TextBox answer=new TextBox {Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,AccessibleName="Chat conversation"};
    readonly Label status=new Label {Dock=DockStyle.Fill,ForeColor=Theme.Muted};
    readonly Button attach=Theme.Button("Attach image"),capture=Theme.Button("Select screen region"),record=Theme.Button("Speak question"),ask=Theme.Button("Ask",true),cancel=Theme.Button("Cancel"),copy=Theme.Button("Copy answer");
    readonly Button newChat=Theme.Button("New chat"),removeImage=Theme.Button("Remove image");
    readonly ChatSession conversation=new ChatSession();readonly RowStyle imageRow=new RowStyle(SizeType.Absolute,0);string lastAnswer="";
    readonly System.Windows.Forms.Timer recordingLimit=new System.Windows.Forms.Timer {Interval=60000};
    CancellationTokenSource pending;Microphone mic;Utterances segments;readonly List<Task<string>> transcriptions=new List<Task<string>>();
    byte[] png;bool disposed;int recordingGeneration;
    public bool IsBusy {get{return pending!=null;}}
    public bool Recording {get{return mic!=null;}}
    public Action<string> AnswerReady;
    public void UseImage(byte[] image){using(MemoryStream stream=new MemoryStream(image))using(Image decoded=Image.FromStream(stream))SetImage(decoded);}
    public void UseExchange(byte[] image,string message,string result){if(IsBusy)return;UseImage(image);conversation.Complete(message,result);lastAnswer=result;question.Clear();RenderConversation();ControlsBusy(false);status.Text="Image answer ready. Ask a follow-up about the same image.";}
    public void SpeakQuestion(){StartRecording();}
    public Task StopQuestion(){return FinishRecording();}
    public Task SelectRegion(){return CaptureRegion();}
    public ImageQuestionWindow(Func<Models> getModels,Func<bool> available,Func<int> getMicrophone){
        models=getModels;canStart=available;microphone=getMicrophone;Text="Chat";Font=new Font("Segoe UI",10);BackColor=Color.White;ClientSize=new Size(800,730);
        TableLayoutPanel layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(24)};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));layout.RowStyles.Add(imageRow);layout.RowStyles.Add(new RowStyle(SizeType.Absolute,28));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));foreach(int height in new[]{28,80,46,48})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        FlowLayoutPanel sources=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};sources.Controls.AddRange(new Control[]{attach,capture,removeImage,newChat});layout.Controls.Add(sources,0,0);layout.Controls.Add(preview,0,1);
        layout.Controls.Add(new Label {Text="Conversation",AutoSize=true,Margin=new Padding(0,8,0,0)},0,2);layout.Controls.Add(answer,0,3);
        layout.Controls.Add(new Label {Text="Message · Ctrl + Enter to send",AutoSize=true,Margin=new Padding(0,8,0,0)},0,4);layout.Controls.Add(question,0,5);
        FlowLayoutPanel actions=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(0,6,0,0)};actions.Controls.AddRange(new Control[]{record,ask,cancel,copy});layout.Controls.Add(actions,0,6);layout.Controls.Add(status,0,7);Controls.Add(layout);
        status.Text="Chat with your local language model. Attach an image or screen grab with EdgeRock. Recent turns stay in memory until you close the window.";cancel.Enabled=copy.Enabled=removeImage.Enabled=false;
        attach.Click+=(s,e)=>Attach();capture.Click+=async(s,e)=>await CaptureRegion();record.Click+=async(s,e)=>{if(mic!=null)await FinishRecording();else StartRecording();};ask.Click+=async(s,e)=>await Ask();cancel.Click+=(s,e)=>CancelWork();copy.Click+=(s,e)=>{try{TextClipboard.SetText(lastAnswer);}catch{status.Text="Clipboard busy. Try Copy answer again.";}};
        newChat.Click+=(s,e)=>ClearChat();removeImage.Click+=(s,e)=>ClearChat();question.KeyDown+=async(s,e)=>{if(e.Control&&e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Ask();}};
        recordingLimit.Tick+=async(s,e)=>await FinishRecording();Theme.Apply(this);
        Shown+=(s,e)=>question.Focus();
    }
    bool Available(){Models current=models();if(!canStart()){status.Text="Finish recording, importing or loading models first.";return false;}if(current==null||!current.Alive){status.Text="Load a language model in Settings → System → Models first.";return false;}if(png!=null&&!current.SupportsImages){status.Text="Load EdgeRock with vision for image chat, or remove the image to chat with your language model.";return false;}return true;}
    void ControlsBusy(bool busy){attach.Enabled=capture.Enabled=question.Enabled=ask.Enabled=newChat.Enabled=!busy;removeImage.Enabled=!busy&&png!=null;record.Enabled=!busy||mic!=null;record.Text=mic!=null?"Stop and ask":"Speak question";cancel.Enabled=busy;copy.Enabled=!busy&&!String.IsNullOrEmpty(lastAnswer);}
    void ClearChat(){if(IsBusy)return;conversation.Clear();answer.Clear();question.Clear();lastAnswer="";png=null;Image previous=preview.Image;preview.Image=null;if(previous!=null)previous.Dispose();imageRow.Height=0;ControlsBusy(false);status.Text="New local chat. Type or speak a message, or attach an image.";}
    void RenderConversation(){answer.Text=String.Join(Environment.NewLine+Environment.NewLine,Array.ConvertAll(conversation.Turns,turn=>(turn.Role=="user"?"You":"Assistant")+Environment.NewLine+turn.Text));answer.SelectionStart=answer.TextLength;answer.ScrollToCaret();}
    void SetImage(Image image){
        if(image.Width*(long)image.Height>16000000)throw new InvalidOperationException("Choose an image with at most 16 million pixels.");
        using(Bitmap normalized=new Bitmap(image.Width,image.Height)){using(Graphics draw=Graphics.FromImage(normalized))draw.DrawImage(image,0,0,image.Width,image.Height);using(MemoryStream stream=new MemoryStream()){normalized.Save(stream,ImageFormat.Png);if(stream.Length>8*1024*1024)throw new InvalidOperationException("Choose a smaller image (up to 8 MB as PNG).");png=stream.ToArray();}Image previous=preview.Image;preview.Image=new Bitmap(normalized);if(previous!=null)previous.Dispose();}
        conversation.Clear();answer.Clear();lastAnswer="";copy.Enabled=false;removeImage.Enabled=true;imageRow.Height=140;status.Text="New image selected. Started a new chat; follow-up questions will use this image.";
    }
    void Attach(){if(IsBusy)return;using(OpenFileDialog picker=new OpenFileDialog {Title="Choose an image",Filter="Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"})if(picker.ShowDialog(this)==DialogResult.OK)try{if(new FileInfo(picker.FileName).Length>16*1024*1024)throw new InvalidOperationException("Choose an image file under 16 MB.");using(FileStream file=File.OpenRead(picker.FileName))using(Image image=Image.FromStream(file))SetImage(image);}catch(Exception error){status.Text="Could not open image: "+error.Message;}}
    async Task CaptureRegion(){
        if(IsBusy)return;Form host=TopLevelControl as Form;Form owner=host??this;Rectangle desktop=SystemInformation.VirtualScreen;
        using(CancellationTokenSource work=new CancellationTokenSource()){
            pending=work;ControlsBusy(true);
            try{byte[] image=await SelectedImageData.CaptureRegion(work.Token,owner);if(image!=null&&!disposed)UseImage(image);}
            catch(OperationCanceledException){}catch(Exception error){if(!disposed)status.Text="Could not capture image: "+error.Message;}
            finally{if(!disposed){owner.Show();owner.Activate();pending=null;ControlsBusy(false);}}
        }
    }
    void StartRecording(){
        if(IsBusy)return;if(!Available())return;
        pending=new CancellationTokenSource();transcriptions.Clear();segments=new Utterances();Models current=models();CancellationToken token=pending.Token;int generation=++recordingGeneration;
        segments.Submit=pcm=>{if(token.IsCancellationRequested||generation!=recordingGeneration)return;lock(transcriptions){Task<string> task=current.Transcribe(pcm,"",token);transcriptions.Add(task);task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}};
        try{mic=new Microphone(microphone());mic.Audio=segments.Push;mic.Start();recordingLimit.Start();status.Text="Listening. Click Stop and ask when finished (60-second limit).";ControlsBusy(true);}
        catch(Exception error){CancelWork();status.Text=error.Message;}
    }
    async Task FinishRecording(){
        if(mic==null)return;recordingLimit.Stop();Microphone active=mic;mic=null;active.Dispose();CancellationTokenSource work=pending;ControlsBusy(true);
        try{segments.Finish();Task<string>[] tasks;lock(transcriptions)tasks=transcriptions.ToArray();string spoken=String.Join(" ",await Task.WhenAll(tasks));work.Token.ThrowIfCancellationRequested();if(disposed)return;if(String.IsNullOrWhiteSpace(spoken)){status.Text="No speech detected. Try again.";return;}question.Text=spoken.Length<=4000?spoken:spoken.Substring(0,4000);}
        catch(OperationCanceledException){return;}catch(Exception error){if(!disposed)status.Text="Could not transcribe question: "+error.Message;return;}
        finally{if(pending==work)pending=null;work.Dispose();if(!disposed)ControlsBusy(false);}
        if(!disposed)await Ask();
    }
    async Task Ask(){
        if(IsBusy||!Available())return;if(String.IsNullOrWhiteSpace(question.Text)){status.Text="Enter a message first.";return;}string message=question.Text.Trim();
        CancellationTokenSource work=new CancellationTokenSource();pending=work;ControlsBusy(true);status.Text=png!=null?"Reading image and conversation locally…":"Thinking locally…";
        try{string result=await models().Chat(conversation.Request(message),png,work.Token);work.Token.ThrowIfCancellationRequested();if(!disposed){conversation.Complete(message,result);lastAnswer=result;RenderConversation();question.Clear();status.Text="Answer ready. Ask a follow-up, or start a new chat. Recent conversation stays in memory.";if(AnswerReady!=null)AnswerReady(result);}}
        catch(OperationCanceledException){if(!disposed)status.Text="Cancelled.";}catch(Exception error){if(!disposed)status.Text="Could not answer: "+error.Message;}
        finally{if(pending==work)pending=null;work.Dispose();if(!disposed)ControlsBusy(false);}
    }
    public void CancelWork(){recordingLimit.Stop();recordingGeneration++;bool recording=mic!=null;if(mic!=null){mic.Dispose();mic=null;}if(pending!=null){pending.Cancel();if(recording){pending.Dispose();pending=null;}}if(!disposed){status.Text="Cancelled.";ControlsBusy(IsBusy);}}
    protected override void Dispose(bool disposing){if(disposing){disposed=true;CancelWork();recordingLimit.Dispose();if(preview.Image!=null)preview.Image.Dispose();png=null;conversation.Clear();lastAnswer="";}base.Dispose(disposing);}
}

public sealed class RegionPicker : Form {
    readonly Bitmap screen;Point start,end;bool dragging;public Rectangle Selection {get;private set;}
    public RegionPicker(Bitmap image,Rectangle bounds){screen=image;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=bounds;TopMost=true;ShowInTaskbar=false;DoubleBuffered=true;Cursor=Cursors.Cross;KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}};}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.DrawImageUnscaled(screen,0,0);using(Brush shade=new SolidBrush(Color.FromArgb(120,0,0,0)))e.Graphics.FillRectangle(shade,ClientRectangle);Rectangle region=Rectangle.FromLTRB(Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),Math.Max(start.X,end.X),Math.Max(start.Y,end.Y));if(region.Width>0&&region.Height>0){e.Graphics.DrawImage(screen,region,region,GraphicsUnit.Pixel);using(Pen pen=new Pen(Theme.Accent,2))e.Graphics.DrawRectangle(pen,region);}e.Graphics.DrawString("Drag to select · Escape to cancel",Font,Brushes.White,20,20);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;start=end=e.Location;dragging=true;Capture=true;Invalidate();}
    protected override void OnMouseMove(MouseEventArgs e){if(dragging){end=e.Location;Invalidate();}}
    protected override void OnMouseUp(MouseEventArgs e){if(!dragging||e.Button!=MouseButtons.Left)return;dragging=false;Capture=false;end=e.Location;Selection=Rectangle.Intersect(ClientRectangle,Rectangle.FromLTRB(Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),Math.Max(start.X,end.X),Math.Max(start.Y,end.Y)));if(Selection.Width<4||Selection.Height<4)return;DialogResult=DialogResult.OK;Close();}
}
}
