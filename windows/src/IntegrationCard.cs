using System;
using System.Drawing;
using System.Windows.Forms;

namespace LocalFlow {
public sealed class IntegrationCard : Panel {
    public readonly Button Install=Theme.Button("Install plugin");
    readonly Label title=new Label {Text="Stream Deck",AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold)},detail=new Label {Text="Dictate from your Stream Deck.",AutoSize=true,ForeColor=Theme.Muted};
    public IntegrationCard(){Size=new Size(700,104);Margin=new Padding(0,0,0,12);DoubleBuffered=true;Install.AutoSize=false;Install.Size=new Size(132,36);Install.AccessibleName="Install Stream Deck plugin";Controls.AddRange(new Control[]{title,detail,Install});Resize+=(s,e)=>Arrange();Arrange();}
    void Arrange(){title.Location=new Point(76,25);detail.Location=new Point(76,53);Install.Location=new Point(Math.Max(320,Width-152),34);detail.MaximumSize=new Size(Math.Max(180,Width-248),25);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var shape=Theme.Round(new Rectangle(1,1,Width-3,Height-3),12))using(Pen edge=new Pen(Theme.Line))e.Graphics.DrawPath(edge,shape);using(var background=Theme.Round(new Rectangle(19,29,40,40),9))using(Brush fill=new SolidBrush(Theme.Surface))e.Graphics.FillPath(fill,background);for(int y=0;y<3;y++)for(int x=0;x<3;x++)using(Brush fill=new SolidBrush(x==1&&y==1?Theme.Accent:Theme.Ink))e.Graphics.FillRectangle(fill,28+x*8,38+y*8,5,5);}
}
}
