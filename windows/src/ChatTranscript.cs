using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LocalFlow {
public sealed class ChatTranscript : FlowLayoutPanel {
    public ChatTranscript(){Dock=DockStyle.Fill;AutoScroll=true;WrapContents=false;FlowDirection=FlowDirection.TopDown;Padding=new Padding(12,12,12,18);BackColor=Color.White;AccessibleName="Chat messages";AccessibleRole=AccessibleRole.Pane;SizeChanged+=(s,e)=>Arrange();}
    public void ClearMessages(){foreach(Control item in Controls.Cast<Control>().ToArray())item.Dispose();Controls.Clear();Text="";}
    public void Render(ChatTurn[] turns,byte[] image,string pending=null,bool thinking=false,string error=null){
        SuspendLayout();ClearMessages();List<ChatTurn> visible=new List<ChatTurn>(turns);if(pending!=null)visible.Add(new ChatTurn("user",pending));
        Text=String.Join(Environment.NewLine+Environment.NewLine,visible.Select(turn=>(turn.Role=="user"?"You":"Assistant")+Environment.NewLine+turn.Text));
        if(visible.Count==0){Controls.Add(new Label {Text="What can I help you with?\n\nAsk a question, attach an image, or select a screen region.",AutoSize=false,Height=110,ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleCenter,Margin=new Padding(0,70,0,0),AccessibleName="Start a chat"});}
        for(int i=0;i<visible.Count;i++)Controls.Add(new ChatMessageBubble(visible[i],i==0?image:null));
        if(thinking)Controls.Add(new ChatMessageBubble(new ChatTurn("assistant","Thinking…"),null));
        if(error!=null)Controls.Add(new ChatMessageBubble(new ChatTurn("error",error),null));
        Arrange();ResumeLayout(true);if(Controls.Count>0&&visible.Count>0)ScrollControlIntoView(Controls[Controls.Count-1]);
    }
    void Arrange(){int width=Math.Max(180,ClientSize.Width-Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-2);foreach(Control item in Controls){ChatMessageBubble bubble=item as ChatMessageBubble;if(bubble!=null)bubble.Arrange(width);else item.Width=width;}}
}
public sealed class ChatMessageBubble : Panel {
    readonly Panel card=new BubbleCard();readonly Label body,role;readonly LinkLabel copy;readonly PictureBox picture;
    readonly bool user;
    public ChatMessageBubble(ChatTurn turn,byte[] image){
        user=turn.Role=="user";Margin=new Padding(0,0,0,16);BackColor=Color.White;AccessibleName=(user?"You: ":"Assistant: ")+turn.Text;
        card.BackColor=user||turn.Role=="error"?Color.FromArgb(255,241,243):Theme.Surface;Controls.Add(card);
        role=new Label {Text=user?"You":turn.Role=="error"?"Couldn't answer":"Open Mic AI",Font=new Font("Segoe UI",9,FontStyle.Bold),ForeColor=Theme.Muted,AutoSize=false,Height=22,BackColor=Color.Transparent};
        body=new Label {Text=turn.Text,Font=new Font("Segoe UI",10),ForeColor=Theme.Ink,AutoSize=false,BackColor=Color.Transparent,UseMnemonic=false};card.Controls.Add(role);card.Controls.Add(body);
        copy=new LinkLabel {Text="Copy",AutoSize=true,LinkColor=Theme.Muted,ActiveLinkColor=Theme.Accent,BackColor=Color.Transparent,AccessibleName="Copy message",TabStop=true};copy.LinkClicked+=(s,e)=>{try{TextClipboard.SetText(turn.Text);copy.Text="Copied";}catch{copy.Text="Try again";}};card.Controls.Add(copy);
        if(image!=null){picture=new PictureBox {SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White,AccessibleName="Attached image"};using(MemoryStream input=new MemoryStream(image))using(Image source=Image.FromStream(input)){picture.Image=new Bitmap(source,new Size(Math.Max(1,(int)(source.Width*Math.Min(280.0/source.Width,160.0/source.Height))),Math.Max(1,(int)(source.Height*Math.Min(280.0/source.Width,160.0/source.Height)))));}card.Controls.Add(picture);}
    }
    public void Arrange(int width){
        Width=width;int cardWidth=Math.Max(150,Math.Min(680,(int)(width*0.88)));int inner=cardWidth-32,y=14;role.SetBounds(16,y,inner,22);y+=26;
        if(picture!=null){picture.SetBounds(16,y,Math.Min(inner,280),160);y+=170;}
        int height=TextRenderer.MeasureText(body.Text,body.Font,new Size(inner,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix).Height+8;
        body.SetBounds(16,y,inner,Math.Max(24,height));y+=body.Height+8;copy.Location=new Point(16,y);y+=24;Height=y+14;card.SetBounds(user?width-cardWidth:0,0,cardWidth,Height);
    }
    protected override void Dispose(bool disposing){if(disposing){if(picture!=null&&picture.Image!=null)picture.Image.Dispose();role.Font.Dispose();body.Font.Dispose();}base.Dispose(disposing);}
    sealed class BubbleCard : Panel {public BubbleCard(){DoubleBuffered=true;}protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.White:Parent.BackColor);}protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(GraphicsPath shape=Theme.Round(new Rectangle(0,0,Width-1,Height-1),12))using(Brush fill=new SolidBrush(BackColor))e.Graphics.FillPath(fill,shape);}}
}
public sealed class ChatComposer : Panel {
    public ChatComposer(){DoubleBuffered=true;BackColor=Color.White;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(GraphicsPath shape=Theme.Round(new Rectangle(1,1,Width-3,Height-3),12))using(Pen outline=new Pen(Theme.Line))e.Graphics.DrawPath(outline,shape);}
}
}
