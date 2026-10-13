using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LocalFlow {
public sealed class HistoryResultRow : Panel {
    public readonly HistoryEntry Entry;
    readonly TextBox result;
    readonly string original,command;
    readonly Label metadata;
    readonly Button more,copyOriginal,copyCommand;
    readonly FlowLayoutPanel actions;
    bool expanded,layingOut;
    public HistoryResultRow(HistoryEntry entry,Action<string> remove,Action<string> feedback) {
        Entry=entry;Tag=entry;BackColor=Color.White;Margin=new Padding(0,0,0,12);Width=650;
        metadata=new Label {Text=entry.CreatedUtc.ToLocalTime().ToString("h:mm tt")+" · "+Mode(entry.Mode),ForeColor=Theme.Muted,AutoEllipsis=true};
        result=Reader("Result",String.IsNullOrEmpty(entry.Result)?entry.Transcript:entry.Result);
        original=(entry.VoiceEdit&&!String.IsNullOrEmpty(entry.SelectedText)?entry.SelectedText:entry.Transcript)??"";
        bool voiceCommand=entry.VoiceEdit&&!String.IsNullOrEmpty(entry.SelectedText)&&entry.Transcript!=entry.SelectedText;
        command=voiceCommand?entry.Transcript:"";
        actions=new FlowLayoutPanel {WrapContents=true,Margin=Padding.Empty};
        Button copy=Theme.Button("Copy modified"),delete=Theme.Button("Delete");copy.Name="Copy modified";delete.Name="Delete entry";
        more=Theme.Button("Show more");more.Name="Show more";
        copyOriginal=Theme.Button("Copy original");copyOriginal.Name="Copy original";copyCommand=Theme.Button("Copy instruction");
        copyCommand.Name="Copy instruction";copy.Click+=(s,e)=>Copy(result.Text,copy,feedback);copyOriginal.Click+=(s,e)=>Copy(original,copyOriginal,feedback);copyCommand.Click+=(s,e)=>Copy(command,copyCommand,feedback);
        delete.Click+=(s,e)=>remove(entry.Id);more.Click+=(s,e)=>{expanded=!expanded;more.Text=expanded?"Show less":"Show more";Arrange();};
        actions.Controls.AddRange(new Control[]{copy,copyOriginal,copyCommand,more,delete});Controls.AddRange(new Control[]{metadata,result,actions});
        SizeChanged+=(s,e)=>Arrange();Arrange();
        Paint+=(s,e)=>{using(System.Drawing.Drawing2D.GraphicsPath shape=Theme.Round(new Rectangle(1,1,Width-3,Height-3),9))using(Pen line=new Pen(Theme.Line))e.Graphics.DrawPath(line,shape);};
    }
    static string Mode(string mode){return mode=="Keep my wording"||mode=="Clean"?"Clean up":mode=="Smart formatting"?"Polish":mode=="Original transcript"?"Raw transcript":mode??"";}
    static TextBox Reader(string name,string text){return new TextBox {Name=name,Tag="history-reader",Text=text??"",ReadOnly=true,Multiline=true,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,Font=new Font("Segoe UI",10)};}
    int TextHeight(TextBox box){return Math.Max(23,TextRenderer.MeasureText(box.Text,box.Font,new Size(Math.Max(160,Width-34),Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPadding).Height+5);}
    void Arrange(){
        if(layingOut)return;layingOut=true;
        try {
            int width=Math.Max(160,Width-32),full=TextHeight(result),height=expanded?Math.Min(400,full):Math.Min(62,full);
            metadata.SetBounds(16,12,width,22);result.SetBounds(16,39,width,height);result.ScrollBars=expanded&&full>height?ScrollBars.Vertical:ScrollBars.None;
            more.Visible=full>62;copyOriginal.Visible=original.Length>0;copyCommand.Visible=command.Length>0;
            int y=39+height+10,actionHeight=Math.Max(38,actions.GetPreferredSize(new Size(width,0)).Height);actions.SetBounds(14,y,width,actionHeight);y+=actionHeight+10;
            Height=y+3;
        } finally {layingOut=false;}
    }
    static void Copy(string text,Button button,Action<string> feedback){try{if(!String.IsNullOrEmpty(text))TextClipboard.SetText(text);button.Text="Copied";feedback("");}catch(System.Runtime.InteropServices.ExternalException){feedback("Clipboard busy. Try again.");}}
}

public sealed class HistoryWindow : Form {
    readonly HistoryStore store;
    readonly TextBox search=new TextBox {Dock=DockStyle.Fill,AccessibleName="Search history"};
    readonly FlowLayoutPanel list=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};
    readonly Label count=new Label {Dock=DockStyle.Fill,ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleLeft};
    readonly Label feedback=new Label {Dock=DockStyle.Fill,ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleLeft};
    readonly Button date=Theme.Button("All dates"),resetDate=Theme.Button("Clear date"),clear=Theme.Button("Clear history"),loadMore=Theme.Button("Load more");
    readonly MonthCalendar calendar=new MonthCalendar {MaxSelectionCount=1,ShowTodayCircle=true,FirstDayOfWeek=Day.Monday,Font=new Font("Segoe UI",9)};
    readonly Panel calendarPanel=new Panel {Dock=DockStyle.Fill,Visible=false,Margin=Padding.Empty};
    readonly RowStyle calendarRow=new RowStyle(SizeType.Absolute,0);
    DateTime? selectedDate;
    int shown=50;bool confirmingClear;
    public HistoryWindow(HistoryStore history,bool enabled,Action<bool> savePreference,MediaImportController importer=null) {
        store=history;Text="Open Mic AI history";ClientSize=new Size(760,650);MinimumSize=new Size(650,560);Font=new Font("Segoe UI",10);BackColor=Color.White;
        TableLayoutPanel shell=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};shell.RowStyles.Add(new RowStyle(SizeType.Absolute,importer==null?0:180));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(shell);
        if(importer!=null){Panel importArea=new Panel {Dock=DockStyle.Fill,Padding=new Padding(24,12,24,0),Margin=Padding.Empty};MediaImportPanel importCard=new MediaImportPanel(importer);importCard.HeightNeeded=height=>shell.RowStyles[0].Height=height;shell.RowStyles[0].Height=importCard.DesiredHeight;importArea.Controls.Add(importCard);shell.Controls.Add(importArea,0,0);}
        TableLayoutPanel layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24,12,24,12),ColumnCount=1,RowCount=6,Margin=Padding.Empty};shell.Controls.Add(layout,0,1);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));layout.RowStyles.Add(calendarRow);layout.RowStyles.Add(new RowStyle(SizeType.Absolute,31));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,26));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        TableLayoutPanel toolbar=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,112));
        search.Margin=new Padding(0,0,12,12);Theme.Cue(search,"Search history");date.Dock=DockStyle.Fill;date.Margin=new Padding(0,0,10,12);resetDate.Dock=DockStyle.Fill;resetDate.Margin=new Padding(0,0,0,12);resetDate.Visible=false;toolbar.Controls.Add(search,0,0);toolbar.Controls.Add(date,1,0);toolbar.Controls.Add(resetDate,2,0);layout.Controls.Add(toolbar,0,0);
        calendarPanel.Controls.Add(calendar);layout.Controls.Add(calendarPanel,0,1);layout.Controls.Add(count,0,2);layout.Controls.Add(list,0,3);layout.Controls.Add(feedback,0,4);
        FlowLayoutPanel footer=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,5,0,0)};footer.Controls.Add(clear);layout.Controls.Add(footer,0,5);
        date.Click+=(s,e)=>{calendarPanel.Visible=!calendarPanel.Visible;calendarRow.Height=calendarPanel.Visible?calendar.Height+12:0;if(calendarPanel.Visible)calendar.SetDate(selectedDate??DateTime.Today);};
        calendar.DateSelected+=(s,e)=>FilterDate(e.Start.Date);resetDate.Click+=(s,e)=>FilterDate(null);
        clear.Click+=(s,e)=>{if(!confirmingClear){confirmingClear=true;clear.Text="Confirm clear";feedback.Text="Clear all history?";return;}store.Clear();confirmingClear=false;clear.Text="Clear history";feedback.Text="";};
        search.TextChanged+=(s,e)=>{shown=50;RefreshEntries();};loadMore.Click+=(s,e)=>{shown+=50;RefreshEntries();};list.SizeChanged+=(s,e)=>ResizeRows();store.Changed+=HistoryChanged;Theme.Apply(this);RefreshEntries();
    }
    public void FilterDate(DateTime? value){selectedDate=value.HasValue?value.Value.Date:(DateTime?)null;shown=50;date.Text=selectedDate.HasValue?selectedDate.Value.ToString("MMM d, yyyy"):"All dates";resetDate.Visible=selectedDate.HasValue;calendarPanel.Visible=false;calendarRow.Height=0;RefreshEntries();}
    void HistoryChanged(){if(IsDisposed)return;if(InvokeRequired){try{BeginInvoke(new Action(RefreshEntries));}catch(InvalidOperationException){}return;}RefreshEntries();}
    void RefreshEntries(){
        if(IsDisposed)return;string query=search.Text.Trim();HistoryEntry[] all=store.Snapshot();HistoryEntry[] matches=all.Where(entry=>(!selectedDate.HasValue||entry.CreatedUtc.ToLocalTime().Date==selectedDate.Value)&&
            (query.Length==0||String.Join(" ",new[]{entry.Mode,entry.Result,entry.Transcript,entry.SelectedText}).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).OrderByDescending(e=>e.CreatedUtc).ToArray();
        list.SuspendLayout();foreach(Control old in list.Controls.Cast<Control>().Where(c=>c!=loadMore).ToArray())old.Dispose();list.Controls.Clear();DateTime? group=null;
        foreach(HistoryEntry entry in matches.Take(shown)){DateTime day=entry.CreatedUtc.ToLocalTime().Date;if(group!=day){group=day;list.Controls.Add(new Label {Text=day==DateTime.Today?"Today":day==DateTime.Today.AddDays(-1)?"Yesterday":day.ToString("dddd, MMMM d"),Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=Theme.Ink,Height=32,Margin=new Padding(0,6,0,0)});}list.Controls.Add(new HistoryResultRow(entry,id=>store.Delete(id),message=>feedback.Text=message));}
        if(matches.Length>shown)list.Controls.Add(loadMore);if(matches.Length==0)list.Controls.Add(new Label {Text=all.Length==0?"No history yet":"No results",ForeColor=Theme.Muted,AutoSize=true,Margin=new Padding(0,18,0,0)});
        count.Text=matches.Length==all.Length?all.Length+" "+(all.Length==1?"result":"results"):matches.Length+" of "+all.Length+" results";clear.Enabled=all.Length>0;calendar.BoldedDates=all.Select(e=>e.CreatedUtc.ToLocalTime().Date).Distinct().ToArray();calendar.UpdateBoldedDates();ResizeRows();list.ResumeLayout();
    }
    void ResizeRows(){foreach(Control row in list.Controls)if(row is HistoryResultRow||row is Label&&!((Label)row).AutoSize)row.Width=Math.Max(300,list.ClientSize.Width-23);list.AutoScrollMinSize=Size.Empty;list.PerformLayout();}
    protected override void Dispose(bool disposing){if(disposing){store.Changed-=HistoryChanged;loadMore.Dispose();}base.Dispose(disposing);}
}
}
