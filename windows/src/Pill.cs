using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalFlow {
public sealed class Pill : Form {
    public string Caption="Loading models";
    public bool Recording,Processing,Persistent,Available,Idle;
    public float Level;
    public bool ChoosingAction {get;private set;}
    public bool InstructionRecording {get;private set;}
    string selectedPreview;
    public Action<string> TextAction;
    public Action SpeakInstruction;public Action<string> TranslationLanguage;
    bool choosingLanguage;
    string actionLanguage,actionStyle;
    public Rectangle ActionBounds(int index){return new Rectangle(28+index*112,InstructionRecording?150:104,104,32);}
    public Rectangle VoiceBounds {get{return new Rectangle(28,144,166,30);}}
    public Rectangle LanguageToggleBounds {get{return new Rectangle(206,144,150,30);}}
    public Rectangle LanguageBounds(int index){return new Rectangle(28+(index%3)*112,193+(index/3)*31,104,26);}
    public Action Cancel,Start,History,HideBar,Release;
    public string RecordingMode=RecordingModes.Names[0];
    bool holding;
    readonly TextBox resultReader=new TextBox {Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,Font=new Font("Segoe UI",10),Visible=false,Name="Card result",AccessibleName="AI result"};
    public Action<Point> PositionChanged;
    public Func<string,bool> CopyResult;
    public string RecentText {get;private set;}
    public Rectangle DictateBounds {get{return new Rectangle(24,4,90,30);}}
    public Rectangle RecentCopyBounds {get{return new Rectangle(116,4,28,30);}}
    public Rectangle OpenAppBounds {get{return new Rectangle(146,4,28,30);}}
    Rectangle CloseBounds {get{return new Rectangle(Width-28,5,24,28);}}
    readonly ToolTip hints=new ToolTip {InitialDelay=350,ReshowDelay=100,AutoPopDelay=2500};
    Point hoverLocation=new Point(-1,-1);DateTime recentCopiedUntil;bool dictatePressed;
    public void RememberResult(string text){if(!String.IsNullOrWhiteSpace(text)){RecentText=text;recentCopiedUntil=DateTime.MinValue;Invalidate();}}
    public void CompletePaste(string text){RememberResult(text);Conceal();}
    public bool ResultReady {get;private set;}
    public string ResultText {get;private set;}
    public string TranscriptText {get;private set;}
    public bool Pinned {get;private set;}
    readonly Timer animation=new Timer {Interval=50},dismiss=new Timer();
    int frame;bool pressed,dragging,suppressClick;Point dragStart,origin;
    Rectangle CopyBounds {get{return new Rectangle(Width-108,8,72,27);}}
    public Pill(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;BackColor=Color.White;ForeColor=Theme.Ink;Size=new Size(204,38);DoubleBuffered=true;Controls.Add(resultReader);
        animation.Tick+=(s,e)=>{if(!Recording&&!Processing){animation.Stop();return;}frame++;Invalidate();};dismiss.Tick+=(s,e)=>Conceal();
        MouseDown+=(s,e)=>{if(e.Button!=MouseButtons.Left)return;dictatePressed=Idle&&DictateBounds.Contains(e.Location);if(dictatePressed)Invalidate();if(RecordingMode=="Hold to talk"&&((DictateBounds.Contains(e.Location)&&Idle)||(ChoosingAction&&VoiceBounds.Contains(e.Location)))){holding=true;suppressClick=true;Capture=true;if(ChoosingAction){if(SpeakInstruction!=null)SpeakInstruction();}else if(Start!=null)Start();return;}pressed=e.X<24;dragging=false;dragStart=PointToScreen(e.Location);origin=Location;if(pressed)Capture=true;};
        MouseLeave+=(s,e)=>{dictatePressed=false;hoverLocation=new Point(-1,-1);hints.SetToolTip(this,"");Invalidate();};MouseMove+=(s,e)=>{UpdateHover(e.Location);if(pressed){Point screen=PointToScreen(e.Location);int dx=screen.X-dragStart.X,dy=screen.Y-dragStart.Y;if(Math.Abs(dx)>5||Math.Abs(dy)>5)dragging=true;if(dragging){Location=Clamp(new Point(origin.X+dx,origin.Y+dy));Cursor=Cursors.SizeAll;}}else Cursor=e.X<24?Cursors.SizeAll:Cursors.Hand;};
        MouseUp+=(s,e)=>{if(e.Button!=MouseButtons.Left)return;dictatePressed=false;Invalidate();if(holding){holding=false;Capture=false;if(Release!=null)Release();return;}pressed=false;Capture=false;if(dragging){Pinned=true;suppressClick=true;if(PositionChanged!=null)PositionChanged(Location);}dragging=false;};
        MouseClick+=(s,e)=>{if(suppressClick){suppressClick=false;return;}if(e.Button!=MouseButtons.Left||e.X<24)return;if(CloseBounds.Contains(e.Location)){if(Idle){if(HideBar!=null)HideBar();else{Persistent=false;Conceal();}}else if(ResultReady)Conceal();else if(Cancel!=null)Cancel();return;}if(InstructionRecording){for(int i=0;i<TextActions.Names.Length;i++)if(ActionBounds(i).Contains(e.Location)){if(TextAction!=null)TextAction(TextActions.Names[i]);return;}}if(ChoosingAction){if(VoiceBounds.Contains(e.Location)){if(SpeakInstruction!=null)SpeakInstruction();return;}if(LanguageToggleBounds.Contains(e.Location)){ToggleLanguages();return;}if(choosingLanguage){for(int i=0;i<TextActions.Languages.Length;i++)if(LanguageBounds(i).Contains(e.Location)){actionLanguage=TextActions.Languages[i];if(TranslationLanguage!=null)TranslationLanguage(actionLanguage);if(TextAction!=null)TextAction("Translate");return;}}for(int i=0;i<TextActions.Names.Length;i++)if(ActionBounds(i).Contains(e.Location)){if(TextActions.Names[i]=="Translate")ToggleLanguages();else if(TextAction!=null)TextAction(TextActions.Names[i]);break;}return;}if(Idle){if(DictateBounds.Contains(e.Location)){if(Start!=null)Start();}else if(RecentCopyBounds.Contains(e.Location))CopyRecent();else if(OpenAppBounds.Contains(e.Location)&&History!=null)History();return;}if(ResultReady){if(CopyBounds.Contains(e.Location)&&CopyResult!=null){dismiss.Stop();if(CopyResult(ResultText)){Caption="Copied";Invalidate();dismiss.Interval=2500;dismiss.Start();}}return;}if(Recording&&Start!=null)Start();};
    }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(Width<2||Height<2)return;using(GraphicsPath shape=Theme.Round(ClientRectangle,12)){Region previous=Region;Region=new Region(shape);if(previous!=null)previous.Dispose();}}
    protected override CreateParams CreateParams {get{CreateParams p=base.CreateParams;p.ExStyle|=0x08000080;return p;}}
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
    public void SetPosition(Point point){Pinned=true;Location=Clamp(point);}
    Point Clamp(Point point){Rectangle area=Screen.FromPoint(point).WorkingArea;return new Point(Math.Max(area.Left,Math.Min(point.X,area.Right-Width)),Math.Max(area.Top,Math.Min(point.Y,area.Bottom-Height)));}
    void ResizeBar(Size size){Point anchored=new Point(Left+(Width-size.Width)/2,Top+Height-size.Height);Size=size;if(Pinned)Location=Clamp(anchored);}
    public void Reveal(){if(!Visible)Show();SetWindowPos(Handle,new IntPtr(-1),Left,Top,Width,Height,0x0010|0x0040);if(Recording||Processing)animation.Start();}
    public void Conceal(){dismiss.Stop();animation.Stop();ChoosingAction=false;ResultReady=false;ResultText="";TranscriptText="";resultReader.Visible=false;resultReader.Text="";Processing=Recording=false;if(Persistent){ShowIdle();return;}Idle=false;Hide();}
    public void Prepare(string caption,bool recording){dictatePressed=false;hints.SetToolTip(this,"");dismiss.Stop();animation.Stop();ChoosingAction=false;InstructionRecording=false;selectedPreview="";AccessibleName="Open Mic AI";choosingLanguage=false;Idle=false;Processing=false;ResultReady=false;ResultText="";TranscriptText="";resultReader.Visible=false;resultReader.Text="";Caption=caption;Recording=recording;ResizeBar(recording?new Size(360,90):new Size(252,40));Invalidate();}
    public void ShowIdle(){Prepare("Ready",false);Idle=true;AccessibleName="Dictate, Copy recent transcription, Open app";ResizeBar(new Size(204,38));Place(Native.GetForegroundWindow());Reveal();}
    public void ShowProcessing(bool rewrite){string text=TranscriptText;Prepare(rewrite?"Polishing":"Transcribing",false);Processing=true;TranscriptText=text;ResizeBar(new Size(360,90));Reveal();Invalidate();}
    public void ShowTextActions(string source,string style,string language,IntPtr target){Prepare("Selected text",false);ChoosingAction=true;TranscriptText=source;actionStyle=style=="Smart formatting"?"Polish":style=="Custom"?"Custom":"Clean up";actionLanguage=language;ResizeBar(new Size(388,186));Place(target);AccessibleName="Selected text: Rewrite, Translate, Summarize, Speak instruction. Escape to cancel.";Reveal();Invalidate();}
    void ToggleLanguages(){choosingLanguage=!choosingLanguage;ResizeBar(new Size(388,choosingLanguage?358:186));Location=Clamp(Location);Reveal();Invalidate();}
    public void OpenLanguages(){if(ChoosingAction&&!choosingLanguage)ToggleLanguages();}
    public void ShowVoiceInstruction(string source,IntPtr target){Prepare("Say an instruction",true);InstructionRecording=true;selectedPreview=source;ResizeBar(new Size(388,222));Place(target);AccessibleName="Selected text. Say rewrite and describe the change, translate into a language, summarize, or dictate replacement text. Rewrite, Translate and Summarize buttons are available.";Reveal();Invalidate();}
    public void ShowActionProcessing(string action,string source){Prepare(action=="Translate"?"Translating":action=="Summarize"?"Summarizing":"Rewriting",false);Processing=true;TranscriptText=source;ResizeBar(new Size(360,90));Reveal();Invalidate();}
    public void ShowNotice(string title,string detail){Prepare(title,false);TranscriptText=detail;ResizeBar(new Size(360,90));Place(Native.GetForegroundWindow());Reveal();dismiss.Interval=8000;dismiss.Start();Invalidate();}
    public void UpdateTranscript(string text){TranscriptText=text??"";Invalidate();}
    public void ShowResult(string text,bool transcriptOnly,bool persistent,IntPtr target,bool expanded=false,string title=null){Prepare(transcriptOnly?"Transcript ready":"Text ready",false);ResultReady=true;ResultText=text;RememberResult(text);TranscriptText=text;if(title!=null)Caption=title;int cardHeight=expanded?Math.Max(60,Math.Min(184,TextRenderer.MeasureText(text,resultReader.Font,new Size(384,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPadding).Height+12))+62:68;ResizeBar(expanded?new Size(440,cardHeight):new Size(360,68));resultReader.Visible=expanded;if(expanded){resultReader.SetBounds(29,46,Width-56,Height-62);resultReader.Text=text;}Place(target);Reveal();Invalidate();if(!persistent){dismiss.Interval=8000;dismiss.Start();}}
    protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
    public void Place(IntPtr target){if(Pinned){Location=Clamp(Location);return;}Rectangle area=Screen.FromHandle(target).WorkingArea;Location=new Point(area.Left+(area.Width-Width)/2,area.Bottom-Height-28);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using(GraphicsPath shape=Theme.Round(new Rectangle(1,1,Width-3,Height-3),12)){using(Brush fill=new SolidBrush(Color.White))g.FillPath(fill,shape);using(Pen line=new Pen(Theme.Line))g.DrawPath(line,shape);}using(Brush grip=new SolidBrush(Color.FromArgb(185,185,191)))for(int row=0;row<3;row++)for(int col=0;col<2;col++)g.FillEllipse(grip,9+col*4,Height/2-5+row*4,2,2);
        using(Font font=new Font("Segoe UI",9.5f)){TextRenderer.DrawText(g,"×",font,new Rectangle(Width-29,7,24,24),Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(ChoosingAction){TextRenderer.DrawText(g,"Selected text",font,new Rectangle(28,7,124,24),Theme.Ink,TextFormatFlags.VerticalCenter);using(Font small=new Font("Segoe UI",8.5f)){TextRenderer.DrawText(g,(TranscriptText??"").Length.ToString("N0")+" characters",small,new Rectangle(158,8,Width-194,22),Theme.Muted,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);TextRenderer.DrawText(g,TranscriptText??"",font,new Rectangle(28,38,Width-54,57),Theme.Muted,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);for(int i=0;i<TextActions.Names.Length;i++)PaintAction(g,font,ActionBounds(i),TextActions.Names[i],true);PaintAction(g,small,VoiceBounds,"Speak instruction",false);PaintAction(g,small,LanguageToggleBounds,actionLanguage+" ▾",false);if(choosingLanguage){TextRenderer.DrawText(g,"Translate to",small,new Rectangle(28,173,Width-54,20),Theme.Muted,TextFormatFlags.VerticalCenter);for(int i=0;i<TextActions.Languages.Length;i++)PaintAction(g,small,LanguageBounds(i),TextActions.Languages[i],TextActions.Languages[i]==actionLanguage);}}return;}if(Idle){PaintDictate(g);PaintIdleIcon(g,RecentCopyBounds,false,!String.IsNullOrWhiteSpace(RecentText));PaintIdleIcon(g,OpenAppBounds,true,true);return;}
            if(Processing){using(Pen ring=new Pen(Theme.Accent,2.2f)){g.DrawArc(ring,29,15,12,12,frame*24,245);}for(int i=0;i<3;i++){int alpha=80+(int)(175*(.5+.5*Math.Sin(frame*.3-i)));using(Brush dot=new SolidBrush(Color.FromArgb(alpha,Theme.Accent)))g.FillEllipse(dot,Width-81+i*9,18,4,4);}}
            else using(Brush dot=new SolidBrush(Theme.Accent))g.FillEllipse(dot,30,16,9,9);
            TextRenderer.DrawText(g,Caption,font,new Rectangle(49,8,Width-170,27),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            if(ResultReady){using(GraphicsPath copy=Theme.Round(CopyBounds,7))using(Brush fill=new SolidBrush(Theme.Accent))g.FillPath(fill,copy);PaintCopyIcon(g,new Point(CopyBounds.Left+8,CopyBounds.Top+5),Color.White,Caption=="Copied");TextRenderer.DrawText(g,"Copy",font,new Rectangle(CopyBounds.Left+27,CopyBounds.Top,CopyBounds.Width-30,CopyBounds.Height),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
            if(Recording)for(int i=0;i<5;i++){float height=3+Math.Min(15,Level*180)*(float)(.5+.5*Math.Sin(frame*.5+i));using(Brush wave=new SolidBrush(Theme.Accent))g.FillRectangle(wave,Width-90+i*7,21-height/2,3,height);}
        }
        if(resultReader.Visible)return;
        if(InstructionRecording){using(Font small=new Font("Segoe UI",8.5f)){TextRenderer.DrawText(g,selectedPreview??"",small,new Rectangle(29,39,Width-52,38),Theme.Muted,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);TextRenderer.DrawText(g,String.IsNullOrWhiteSpace(TranscriptText)?"Say rewrite, translate into a language, or summarize…":TranscriptText,small,new Rectangle(29,87,Width-52,52),Theme.Ink,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);for(int i=0;i<TextActions.Names.Length;i++)PaintAction(g,small,ActionBounds(i),TextActions.Names[i],true);TextRenderer.DrawText(g,"Or dictate replacement text · press again to finish",small,new Rectangle(29,192,Width-52,20),Theme.Muted,TextFormatFlags.VerticalCenter);}return;}
        string preview=TranscriptText;if(String.IsNullOrWhiteSpace(preview))preview=Recording?"Speak…":Processing?"Finishing your text…":"";if(preview.Length>170)preview="… "+preview.Substring(preview.Length-170);if(Caption=="Copied")preview="Ctrl+V to paste";using(Font font=new Font("Segoe UI",9))TextRenderer.DrawText(g,preview,font,new Rectangle(29,ResultReady?40:41,Width-52,ResultReady?21:39),Theme.Muted,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);
    }
    static void PaintAction(Graphics g,Font font,Rectangle area,string text,bool accent){using(GraphicsPath shape=Theme.Round(area,7)){using(Brush fill=new SolidBrush(accent?Color.FromArgb(255,240,242):Color.White))g.FillPath(fill,shape);using(Pen line=new Pen(Theme.Line))g.DrawPath(line,shape);}TextRenderer.DrawText(g,text,font,area,accent?Theme.Accent:Theme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
    void PaintIdleIcon(Graphics g,Rectangle area,bool app,bool enabled){
        if(enabled&&area.Contains(hoverLocation))using(GraphicsPath shape=Theme.Round(area,6)){using(Brush fill=new SolidBrush(Color.FromArgb(255,244,245)))g.FillPath(fill,shape);using(Pen edge=new Pen(Color.FromArgb(247,227,230)))g.DrawPath(edge,shape);}
        if(app){if(Brand.Logo!=null)g.DrawImage(Brand.Logo,new Rectangle(area.Left+3,area.Top+4,22,22));return;}
        PaintCopyIcon(g,new Point(area.Left+6,area.Top+7),enabled?Theme.Ink:Color.FromArgb(185,188,197),recentCopiedUntil>DateTime.UtcNow);
    }
    static void PaintCopyIcon(Graphics g,Point origin,Color color,bool copied){
        GraphicsState state=g.Save();g.TranslateTransform(origin.X,origin.Y);
        using(Pen pen=new Pen(color,1.5f)){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
            if(copied)g.DrawLines(pen,new[]{new PointF(2,8),new PointF(6,12),new PointF(14,3)});
            else{
                using(GraphicsPath back=new GraphicsPath()){back.AddLine(4,11,3,11);back.AddArc(1,8,3,3,90,90);back.AddLine(1,9.5f,1,3);back.AddArc(1,1,3,3,180,90);back.AddLine(2.5f,1,9.5f,1);back.AddArc(8,1,3,3,270,90);back.AddLine(11,2.5f,11,4);g.DrawPath(pen,back);}
                using(GraphicsPath front=Theme.Round(new Rectangle(5,5,10,10),2))g.DrawPath(pen,front);
            }
        }g.Restore(state);
    }
    void PaintDictate(Graphics g){
        Rectangle area=DictateBounds;bool hot=area.Contains(hoverLocation);
        Color fill=hot?(dictatePressed?Color.FromArgb(251,230,233):Color.FromArgb(255,244,245)):Color.White;
        Color foreground=Color.Black;
        using(GraphicsPath shape=Theme.Round(area,8)){using(Brush background=new SolidBrush(fill))g.FillPath(background,shape);if(hot)using(Pen edge=new Pen(Color.FromArgb(247,227,230)))g.DrawPath(edge,shape);}
        int x=area.Left+12,y=area.Top+7;
        using(Pen mic=new Pen(foreground,1.5f)){mic.StartCap=mic.EndCap=LineCap.Round;using(GraphicsPath capsule=Theme.Round(new Rectangle(x+2,y,5,10),2))g.DrawPath(mic,capsule);g.DrawArc(mic,x-1,y+3,11,11,0,180);g.DrawLine(mic,x+4.5f,y+14,x+4.5f,y+17);g.DrawLine(mic,x+1,y+17,x+8,y+17);}
        using(Font label=new Font("Segoe UI Semibold",9.25f,FontStyle.Regular))TextRenderer.DrawText(g,"Dictate",label,new Rectangle(area.Left+29,area.Top,area.Width-33,area.Height),foreground,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
    }
    void CopyRecent(){if(String.IsNullOrWhiteSpace(RecentText)||CopyResult==null)return;if(CopyResult(RecentText)){recentCopiedUntil=DateTime.UtcNow.AddSeconds(1.5);dismiss.Interval=1600;dismiss.Start();Invalidate();}}
    void UpdateHover(Point point){hoverLocation=point;if(!Idle)return;string text=RecentCopyBounds.Contains(point)?String.IsNullOrWhiteSpace(RecentText)?"No recent transcription":"Copy recent transcription":OpenAppBounds.Contains(point)?"Open app":DictateBounds.Contains(point)?"Dictate":CloseBounds.Contains(point)?"Hide bar":"Drag to move";if(hints.GetToolTip(this)!=text)hints.SetToolTip(this,text);Invalidate();}
    protected override void Dispose(bool disposing){if(disposing){animation.Dispose();dismiss.Dispose();hints.Dispose();}base.Dispose(disposing);}
}
}
