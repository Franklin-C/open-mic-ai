using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalFlow {
public class ModernButton : Button {
    bool hover;
    public ModernButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);MouseEnter+=(s,e)=>{hover=true;Invalidate();};MouseLeave+=(s,e)=>{hover=false;Invalidate();};}
    public override Size GetPreferredSize(Size proposedSize){Size text=TextRenderer.MeasureText(Text,Font,new Size(Int32.MaxValue,Int32.MaxValue),TextFormatFlags.SingleLine);return new Size(Math.Max(MinimumSize.Width,text.Width+28+Padding.Horizontal),Math.Max(MinimumSize.Height,Math.Max(36,text.Height+14)));}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.White:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color fill=BackColor;if(hover&&Enabled)fill=BackColor==Theme.Accent?Color.FromArgb(203,36,51):Color.FromArgb(255,242,244);using(GraphicsPath shape=Theme.Round(new Rectangle(1,1,Width-3,Height-3),8)){using(Brush brush=new SolidBrush(fill))e.Graphics.FillPath(brush,shape);if(BackColor!=Theme.Accent&&Convert.ToString(Tag)!="nav")using(Pen border=new Pen(Theme.Line))e.Graphics.DrawPath(border,shape);}TextFormatFlags flags=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter);TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(Padding.Left+8,0,Width-Padding.Left-Padding.Right-16,Height),Enabled?ForeColor:Theme.Muted,flags);if(Focused)using(GraphicsPath focus=Theme.Round(new Rectangle(4,4,Width-9,Height-9),5))using(Pen line=new Pen(Theme.Accent))e.Graphics.DrawPath(line,focus);}
}
public sealed class ModernField : Panel {
    readonly Control input;bool focused;
    public ModernField(Control value){input=value;DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(10,value is ComboBox?5:7,10,7);input.Enter+=(s,e)=>{focused=true;Invalidate();};input.Leave+=(s,e)=>{focused=false;Invalidate();};Click+=(s,e)=>input.Focus();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(GraphicsPath shape=Theme.Round(new Rectangle(1,1,Width-3,Height-3),8))using(Pen border=new Pen(focused?Theme.Accent:Theme.Line,focused?1.6f:1))e.Graphics.DrawPath(border,shape);}
}
public sealed class ModernTabs : TabControl {
    readonly bool secondary;int hovered=-1;
    public ModernTabs(bool nested=false){secondary=nested;DrawMode=TabDrawMode.OwnerDrawFixed;SizeMode=TabSizeMode.Fixed;ItemSize=nested?new Size(104,32):new Size(118,38);Padding=nested?new Point(10,5):new Point(12,9);Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);SelectedIndexChanged+=(s,e)=>Invalidate();MouseMove+=(s,e)=>{int next=-1;for(int i=0;i<TabCount;i++)if(GetTabRect(i).Contains(e.Location)){next=i;break;}if(next!=hovered){hovered=next;Invalidate();}};MouseLeave+=(s,e)=>{hovered=-1;Invalidate();};}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.White);if(secondary&&TabCount>0)using(Pen divider=new Pen(Theme.Line))e.Graphics.DrawLine(divider,12,GetTabRect(0).Bottom-1,Width-12,GetTabRect(0).Bottom-1);for(int i=0;i<TabCount;i++)DrawTab(e.Graphics,i,GetTabRect(i));}
    protected override void OnDrawItem(DrawItemEventArgs e){DrawTab(e.Graphics,e.Index,e.Bounds);}
    void DrawTab(Graphics graphics,int index,Rectangle bounds){bool active=index==SelectedIndex;graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.FillRectangle(Brushes.White,bounds);Rectangle area=Rectangle.Inflate(bounds,-3,-3);if(secondary){using(Font label=new Font(Font,active?FontStyle.Bold:FontStyle.Regular)){TextRenderer.DrawText(graphics,TabPages[index].Text,label,area,active||hovered==index?Theme.Ink:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);if(active){int width=Math.Min(area.Width-18,TextRenderer.MeasureText(TabPages[index].Text,label).Width);int left=area.Left+(area.Width-width)/2;using(Pen line=new Pen(Theme.Accent,2))graphics.DrawLine(line,left,bounds.Bottom-2,left+width,bounds.Bottom-2);}if(Focused&&active)ControlPaint.DrawFocusRectangle(graphics,Rectangle.Inflate(area,-6,-3));}return;}using(GraphicsPath shape=Theme.Round(area,7))using(Brush fill=new SolidBrush(active?Color.FromArgb(255,241,243):Color.White))graphics.FillPath(fill,shape);TextRenderer.DrawText(graphics,TabPages[index].Text,Font,area,active?Theme.Accent:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);if(active)using(Pen line=new Pen(Theme.Accent,2))graphics.DrawLine(line,area.Left+13,area.Bottom-1,area.Right-13,area.Bottom-1);}
}
}
