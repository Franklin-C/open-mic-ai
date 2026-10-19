using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace LocalFlow {
public sealed class RedCheck : CheckBox {
    public RedCheck(){Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);CheckedChanged+=(s,e)=>Invalidate();}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);int y=(Height-14)/2;using(Brush fill=new SolidBrush(Checked?Theme.Accent:Color.White))e.Graphics.FillRectangle(fill,0,y,14,14);using(Pen border=new Pen(Checked?Theme.Accent:Theme.Line))e.Graphics.DrawRectangle(border,0,y,14,14);if(Checked)using(Pen mark=new Pen(Color.White,2))e.Graphics.DrawLines(mark,new[]{new Point(3,y+7),new Point(6,y+10),new Point(11,y+4)});TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(22,0,Math.Max(0,Width-22),Height),Enabled?ForeColor:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);}
}
public static class Theme {
    public static GraphicsPath Round(Rectangle area,int radius){GraphicsPath path=new GraphicsPath();int d=Math.Min(radius*2,Math.Min(area.Width,area.Height));if(d<2){path.AddRectangle(area);return path;}path.AddArc(area.Left,area.Top,d,d,180,90);path.AddArc(area.Right-d,area.Top,d,d,270,90);path.AddArc(area.Right-d,area.Bottom-d,d,d,0,90);path.AddArc(area.Left,area.Bottom-d,d,d,90,90);path.CloseFigure();return path;}
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr w,string text);
    public static void Cue(TextBox box,string text){box.HandleCreated+=(s,e)=>SendMessage(box.Handle,0x1501,new IntPtr(1),text);}
    public static readonly Color Ink=Color.FromArgb(34,34,38),Muted=Color.FromArgb(112,112,120),Surface=Color.FromArgb(249,249,250),Accent=Color.FromArgb(224,48,62),Line=Color.FromArgb(232,232,235);
    public static Button Button(string text,bool primary=false){Button button=new ModernButton {Text=text,AutoSize=true,MinimumSize=new Size(98,36),Height=36,FlatStyle=FlatStyle.Flat,BackColor=primary?Accent:Color.White,ForeColor=primary?Color.White:Ink,Cursor=Cursors.Hand,Margin=new Padding(0,0,10,0),Tag=primary?"primary":null};button.FlatAppearance.BorderColor=primary?Accent:Line;return button;}
    public static Control Frame(Control input){return input.Parent is ModernField?input.Parent:input;}
    static void Wrap(Control input){if(input.Parent==null||input.Parent is ModernField||Convert.ToString(input.Tag)=="history-reader")return;Control parent=input.Parent;int index=parent.Controls.GetChildIndex(input);TableLayoutPanel table=parent as TableLayoutPanel;TableLayoutPanelCellPosition cell=table==null?new TableLayoutPanelCellPosition():table.GetCellPosition(input);ModernField field=new ModernField(input) {Location=input.Location,Size=new Size(input.Width,input is TextBox&&((TextBox)input).Multiline?input.Height:36),Dock=input.Dock,Anchor=input.Anchor,Margin=input.Margin};if(field.Dock==DockStyle.None&&!(parent is FlowLayoutPanel))field.Top-=Math.Max(0,(field.Height-input.Height)/2);parent.Controls.Remove(input);input.Dock=DockStyle.Fill;input.Margin=Padding.Empty;field.Controls.Add(input);if(table!=null)table.Controls.Add(field,cell.Column,cell.Row);else parent.Controls.Add(field);parent.Controls.SetChildIndex(field,index);}
    public static void Apply(Control control) {
        if(control is Form){control.BackColor=Color.White;control.ForeColor=Ink;((Form)control).Icon=Brand.CreateIcon();}
        Button button=control as Button;if(button!=null){button.FlatStyle=FlatStyle.Flat;bool primary=Convert.ToString(button.Tag)=="primary";button.BackColor=primary?Accent:Color.White;button.ForeColor=primary?Color.White:Ink;button.FlatAppearance.BorderColor=primary?Accent:Line;button.Cursor=Cursors.Hand;}
        ComboBox combo=control as ComboBox;if(combo!=null){combo.FlatStyle=FlatStyle.Flat;Wrap(combo);}
        TextBox box=control as TextBox;if(box!=null){box.ForeColor=Ink;box.BackColor=Color.White;box.BorderStyle=BorderStyle.None;Wrap(box);}
        foreach(Control child in control.Controls.Cast<Control>().ToArray())Apply(child);
    }
}
public sealed class HubWindow : Form {
    readonly HistoryStore history;readonly PersonalizationStore personalization;readonly UsageStore usage;readonly Func<Settings> getSettings;readonly Action<Settings> saveSettings;readonly Func<ModelWindow> modelFactory;
    readonly string[] shortcuts;readonly string editShortcut;readonly Panel content=new Panel {Dock=DockStyle.Fill,BackColor=Color.White};
    readonly Label heading=new Label {Dock=DockStyle.Top,Height=72,Font=new Font("Segoe UI",23,FontStyle.Bold),Padding=new Padding(28,21,0,0)};
    readonly Label status=new Label {Dock=DockStyle.Bottom,Height=92,Padding=new Padding(20,12,12,0),ForeColor=Theme.Muted,Font=new Font("Segoe UI",9)};
    readonly FlowLayoutPanel navigation=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(14,18,14,0)};
    Form page;readonly MediaImportController importer;
    public HubWindow(HistoryStore historyStore,PersonalizationStore personalStore,UsageStore statistics,Func<Settings> settings,Action<Settings> applySettings,Func<ModelWindow> models,string[] choices,string editLabel,MediaImportController fileImport=null) {
        importer=fileImport;
        history=historyStore;personalization=personalStore;usage=statistics;getSettings=settings;saveSettings=applySettings;modelFactory=models;shortcuts=choices;editShortcut=editLabel;
        Text=Brand.Name;Icon=Brand.CreateIcon();ClientSize=new Size(1100,800);MinimumSize=new Size(950,740);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);BackColor=Color.White;ForeColor=Theme.Ink;
        Panel sidebar=new Panel {Dock=DockStyle.Fill,BackColor=Color.White,Margin=Padding.Empty};sidebar.Paint+=(s,e)=>{using(Pen line=new Pen(Theme.Line))e.Graphics.DrawLine(line,sidebar.Width-1,0,sidebar.Width-1,sidebar.Height);};
        Panel brand=new Panel {Dock=DockStyle.Top,Height=102};brand.Controls.Add(new PictureBox {Image=Brand.Logo,SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(18,24),Size=new Size(52,52)});brand.Controls.Add(new Label {Text="Open Mic\nAI",Font=new Font("Segoe UI",14,FontStyle.Bold),Location=new Point(82,25),Size=new Size(110,58)});sidebar.Controls.Add(navigation);sidebar.Controls.Add(status);sidebar.Controls.Add(brand);
        foreach(string name in new[]{"Home","History","Stats","Settings"}) {string item=name;Button button=new ModernButton {Text=item,Tag="nav",Size=new Size(175,46),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(7,0,0,0),Margin=new Padding(0,0,0,7),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Theme.Ink,Cursor=Cursors.Hand};button.FlatAppearance.BorderSize=0;button.Click+=(s,e)=>Navigate(item);navigation.Controls.Add(button);}
        Panel main=new Panel {Dock=DockStyle.Fill,Padding=new Padding(8,0,16,14),Margin=Padding.Empty};main.Controls.Add(content);main.Controls.Add(heading);
        TableLayoutPanel shell=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,Padding=Padding.Empty};shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.Controls.Add(sidebar,0,0);shell.Controls.Add(main,1,0);Controls.Add(shell);Navigate("Home");
    }
    public void SetStatus(string value){status.Text=value+"\nEnglish · local";if(page!=null)foreach(ModelWindow model in Descendants(page).OfType<ModelWindow>())model.UpdateStatus(value,getSettings());}
    static System.Collections.Generic.IEnumerable<Control> Descendants(Control root){yield return root;foreach(Control child in root.Controls)foreach(Control item in Descendants(child))yield return item;}
    public void Navigate(string name) {
        string section=new[]{"Style","Snippets","Vocabulary","Shortcuts","Dictation bar","Taskbar","Models","General","Integrations"}.Contains(name)?name:null;if(section!=null)name="Settings";
        if(page!=null){content.Controls.Remove(page);page.Dispose();page=null;}foreach(Control old in content.Controls.Cast<Control>().ToArray())old.Dispose();content.Controls.Clear();heading.Text=name;
        foreach(Button button in navigation.Controls.OfType<Button>()){bool active=button.Text==name;button.BackColor=active?Color.FromArgb(255,240,242):Color.White;button.ForeColor=active?Theme.Accent:Theme.Ink;}
        switch(name) {
            case "Home":page=new DashboardWindow(history,usage,()=>Navigate("History"));break;
            case "History":page=new HistoryWindow(history,getSettings().SaveHistory,enabled=>{Settings next=getSettings();next.SaveHistory=enabled;saveSettings(next);},importer);break;
            case "Stats":page=new StatsWindow(usage,()=>getSettings().SaveInsights);break;
            case "Settings":SettingsWindow settings=new SettingsWindow(getSettings(),shortcuts,null,editShortcut,modelFactory,getSettings,usage.Data.Apps.Select(a=>a.Name).ToArray(),()=>usage.Clear(),personalization);settings.Saved=saveSettings;if(section!=null)settings.SelectSection(section);page=settings;break;
            default:Navigate("Home");return;
        }
        Embed(page);
    }
    void Embed(Form window){window.TopLevel=false;window.FormBorderStyle=FormBorderStyle.None;window.MinimumSize=Size.Empty;window.Dock=DockStyle.Fill;content.Controls.Add(window);window.BringToFront();Theme.Apply(window);window.Show();}
    protected override void Dispose(bool disposing){if(disposing&&page!=null){page.Dispose();page=null;}base.Dispose(disposing);}
}
}
