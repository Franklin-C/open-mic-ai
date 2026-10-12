using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LocalFlow {
public sealed class WordRule {public string Id=Guid.NewGuid().ToString("N"),Heard="",Written="";}
public sealed class Snippet {public string Id=Guid.NewGuid().ToString("N"),Trigger="",Text="";}
public sealed class PersonalData {public List<WordRule> Words=new List<WordRule>();public List<Snippet> Snippets=new List<Snippet>();}
public sealed class LocalJson<T> where T:new() {
    readonly string path;readonly object gate=new object();Task writer=Task.FromResult(0);
    public string Error {get;private set;}
    public event Action<string> Failed;
    bool blocked;
    public LocalJson(string root,string name){path=Path.Combine(root,name);}
    static JavaScriptSerializer Serializer(){return new JavaScriptSerializer {MaxJsonLength=64*1024*1024};}
    public T Read(){try {if(!File.Exists(path))return new T();T value=Serializer().Deserialize<T>(File.ReadAllText(path));if(value==null)throw new InvalidDataException("Invalid saved data.");return value;}catch(Exception error){Error=error.Message;try{File.Move(path,path+".unreadable-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));}catch{blocked=true;}return new T();}}
    public void Save(T value){string json=Serializer().Serialize(value);lock(gate)writer=writer.ContinueWith(previous=>Write(json),CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.Default);}
    void Write(string json){string temp=path+".tmp";try{if(blocked&&File.Exists(path))throw new IOException("Unreadable saved data was preserved.");byte[] bytes=new UTF8Encoding(false).GetBytes(json);using(FileStream file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);Error=null;}catch(Exception error){Error=error.Message;try{File.Delete(temp);}catch{}Action<string> failed=Failed;if(failed!=null)failed(error.Message);}}
    public Task FlushAsync(){lock(gate)return writer;}
}
public sealed class PersonalizationStore {
    readonly LocalJson<PersonalData> file;public PersonalData Data;public event Action Changed;
    public string Error {get{return file.Error;}}
    public event Action<string> Failed {add{file.Failed+=value;}remove{file.Failed-=value;}}
    public PersonalizationStore(string root){file=new LocalJson<PersonalData>(root,"personalization.json");Data=file.Read();if(Data.Words==null)Data.Words=new List<WordRule>();if(Data.Snippets==null)Data.Snippets=new List<Snippet>();}
    public void Save(){file.Save(Data);Action change=Changed;if(change!=null)change();}
    public Task FlushAsync(){return file.FlushAsync();}
    public TextRules Snapshot(){return new TextRules(Data);}
}
public sealed class PreparedDictation {
    public string Text;public bool Literal;public Dictionary<string,string> Inserts=new Dictionary<string,string>();
    public string Restore(string output) {
        foreach(string marker in Inserts.Keys)if(Regex.Matches(output??"",Regex.Escape(marker),RegexOptions.IgnoreCase).Count!=1){output=Text;break;}
        foreach(var item in Inserts)output=Regex.Replace(output,Regex.Escape(item.Key),match=>item.Value,RegexOptions.IgnoreCase);
        return output;
    }
}
public sealed class TextRules {
    readonly WordRule[] words;readonly Snippet[] snippets;
    public TextRules(PersonalData data){words=data.Words.Where(w=>w!=null&&!String.IsNullOrWhiteSpace(w.Written)).Select(w=>new WordRule {Id=w.Id,Heard=w.Heard??"",Written=w.Written}).OrderByDescending(w=>w.Heard.Length).ToArray();snippets=data.Snippets.Where(s=>s!=null&&!String.IsNullOrWhiteSpace(s.Trigger)&&s.Text!=null).Select(s=>new Snippet {Id=s.Id,Trigger=s.Trigger.Trim(),Text=s.Text}).OrderByDescending(s=>s.Trigger.Length).ToArray();}
    static string Phrase(string value){return @"(?<![\p{L}\p{N}_])"+String.Join(@"\s+",Regex.Split(value.Trim(),@"\s+").Select(Regex.Escape))+@"(?![\p{L}\p{N}_])";}
    public string Guidance(string existing){return String.Join(", ",new[]{existing??""}.Concat(words.Take(100).Select(w=>w.Written)).Where(s=>s.Length>0));}
    public string Correct(string text){foreach(WordRule rule in words)if(!String.IsNullOrWhiteSpace(rule.Heard))text=Regex.Replace(text,Phrase(rule.Heard),match=>rule.Written,RegexOptions.IgnoreCase);return text;}
    public PreparedDictation Prepare(string text) {
        PreparedDictation prepared=new PreparedDictation {Text=text};
        if(Regex.IsMatch(text,@"\b(literal|literally|quotation marks|quoted words)\b",RegexOptions.IgnoreCase)){prepared.Literal=true;return prepared;}
        if(snippets.Length>0) {
            string pattern=String.Join("|",snippets.Select(s=>"(?:"+Phrase(s.Trigger)+")"));
            prepared.Text=Regex.Replace(text,pattern,match=>{
                Snippet snippet=snippets.First(s=>Regex.IsMatch(match.Value,"^"+Phrase(s.Trigger)+"$",RegexOptions.IgnoreCase));
                string marker="LF_SNIP_"+Guid.NewGuid().ToString("N").ToUpperInvariant();prepared.Inserts.Add(marker,snippet.Text);return marker;
            },RegexOptions.IgnoreCase);
        }
        prepared.Text=Correct(prepared.Text);return prepared;
    }
    public string Finish(string output,PreparedDictation prepared){return prepared.Restore(prepared.Literal?output:Correct(output));}
}

public sealed class PersonalizationWindow : Form {
    readonly PersonalizationStore store;readonly bool snippets;readonly FlowLayoutPanel list=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};readonly TextBox search=new TextBox {Width=300};readonly Panel editor=new Panel {Dock=DockStyle.Right,Width=310,Visible=false,Padding=new Padding(20,8,0,0)};readonly TextBox first=new TextBox {Dock=DockStyle.Top,MaxLength=200},second=new TextBox {Dock=DockStyle.Top,MaxLength=16000};readonly Label error=new Label {Dock=DockStyle.Top,Height=58,ForeColor=Theme.Accent};string editing;
    public PersonalizationWindow(PersonalizationStore data,bool showSnippets){store=data;snippets=showSnippets;Text=snippets?"Snippets":"Vocabulary";ClientSize=new Size(790,620);Font=new Font("Segoe UI",10);BackColor=Color.White;
        Panel layout=new Panel {Dock=DockStyle.Fill,Padding=new Padding(24,4,24,20)};Controls.Add(layout);Panel toolbar=new Panel {Dock=DockStyle.Top,Height=65};search.Location=new Point(0,18);search.AccessibleName="Search";Theme.Cue(search,snippets?"Search snippets":"Search vocabulary");Button add=Theme.Button(snippets?"Add snippet":"Add word",true);add.Anchor=AnchorStyles.Top|AnchorStyles.Right;add.Location=new Point(toolbar.Width-128,12);toolbar.Controls.AddRange(new Control[]{search,add});toolbar.Resize+=(s,e)=>{add.Left=toolbar.Width-add.Width;Theme.Frame(search).Width=Math.Max(140,toolbar.Width-add.Width-20);};
        Label intro=new Label {Text=snippets?"Say a phrase. Insert your saved text.":"Names and terms, spelled your way.",Dock=DockStyle.Top,Height=38,ForeColor=Theme.Muted};Panel body=new Panel {Dock=DockStyle.Fill};body.Controls.Add(list);body.Controls.Add(editor);layout.Controls.Add(body);layout.Controls.Add(toolbar);layout.Controls.Add(intro);
        second.Multiline=snippets;second.Height=snippets?170:30;second.ScrollBars=snippets?ScrollBars.Vertical:ScrollBars.None;Panel saveRow=new Panel {Dock=DockStyle.Top,Height=51,Padding=new Padding(0,12,0,0)};Button save=Theme.Button("Save",true),cancel=Theme.Button("Cancel");save.Location=new Point(0,10);cancel.Location=new Point(108,10);saveRow.Controls.AddRange(new Control[]{save,cancel});editor.Controls.Add(error);editor.Controls.Add(saveRow);editor.Controls.Add(second);editor.Controls.Add(new Label {Text=snippets?"Insert":"Heard as (optional)",Dock=DockStyle.Top,Height=42,Padding=new Padding(0,15,0,0)});editor.Controls.Add(first);editor.Controls.Add(new Label {Text=snippets?"Say":"Correct spelling",Dock=DockStyle.Top,Height=30});editor.Controls.Add(new Label {Text="",Name="editorHeading",Dock=DockStyle.Top,Height=40,Font=new Font("Segoe UI",13,FontStyle.Bold)});
        add.Click+=(s,e)=>Edit(null);cancel.Click+=(s,e)=>{editor.Visible=false;ResizeRows();};save.Click+=(s,e)=>Save();search.TextChanged+=(s,e)=>RefreshList();list.SizeChanged+=(s,e)=>ResizeRows();store.Changed+=RefreshList;RefreshList();Theme.Apply(this);
    }
    void Edit(string id){editing=id;Snippet snippet=snippets?store.Data.Snippets.FirstOrDefault(s=>s.Id==id):null;WordRule word=!snippets?store.Data.Words.FirstOrDefault(w=>w.Id==id):null;first.Text=snippets?(snippet==null?"":snippet.Trigger):(word==null?"":word.Written);second.Text=snippets?(snippet==null?"":snippet.Text):(word==null?"":word.Heard);editor.Controls["editorHeading"].Text=id==null?(snippets?"New snippet":"New word"):"Edit";error.Text="";editor.Visible=true;ResizeRows();first.Focus();}
    void Save(){string value=first.Text.Trim();if(value.Length==0||(snippets&&second.Text.Length==0)){error.Text="Enter both fields.";return;}if(snippets){if(store.Data.Snippets.Any(s=>s.Id!=editing&&String.Equals(s.Trigger,value,StringComparison.OrdinalIgnoreCase))){error.Text="That phrase already exists.";return;}Snippet item=store.Data.Snippets.FirstOrDefault(s=>s.Id==editing);if(item==null){item=new Snippet();store.Data.Snippets.Add(item);}item.Trigger=value;item.Text=second.Text;}else{WordRule item=store.Data.Words.FirstOrDefault(w=>w.Id==editing);if(item==null){item=new WordRule();store.Data.Words.Add(item);}item.Written=value;item.Heard=second.Text.Trim();}editor.Visible=false;store.Save();}
    void RefreshList(){if(IsDisposed)return;string query=search.Text.Trim();list.SuspendLayout();foreach(Control old in list.Controls.Cast<Control>().ToArray())old.Dispose();list.Controls.Clear();if(snippets){foreach(Snippet item in store.Data.Snippets.Where(s=>(s.Trigger+" "+s.Text).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0))AddRow(item.Id,item.Trigger,item.Text);}else foreach(WordRule item in store.Data.Words.Where(w=>(w.Written+" "+w.Heard).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0))AddRow(item.Id,item.Written,item.Heard);if(list.Controls.Count==0)list.Controls.Add(new Label {Text=query.Length>0?"No matches.":snippets?"Add a link, greeting or signature.":"Add a name or term.",AutoSize=true,ForeColor=Theme.Muted,Padding=new Padding(0,28,0,0)});list.ResumeLayout();ResizeRows();}
    void AddRow(string id,string title,string text){Panel row=new Panel {Width=Math.Max(180,list.ClientSize.Width-22),Height=126,BackColor=Theme.Surface,Margin=new Padding(0,0,0,12)};Label cue=new Label {Text=title,Location=new Point(16,13),Size=new Size(row.Width-32,25),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Theme.Ink,AutoEllipsis=true};Label preview=new Label {Text=text.Replace("\r","").Replace("\n"," · "),Location=new Point(16,43),Size=new Size(row.Width-32,26),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,ForeColor=Theme.Muted,AutoEllipsis=true};Button edit=Theme.Button("Edit"),copy=Theme.Button("Copy"),remove=Theme.Button("Delete");edit.AutoSize=copy.AutoSize=remove.AutoSize=false;edit.MinimumSize=copy.MinimumSize=remove.MinimumSize=new Size(70,30);edit.Size=copy.Size=remove.Size=new Size(70,30);edit.Location=new Point(16,85);copy.Location=new Point(96,85);remove.Location=new Point(snippets?176:96,85);copy.Visible=snippets;edit.Click+=(s,e)=>Edit(id);copy.Click+=(s,e)=>{try{TextClipboard.SetText(text);copy.Text="Copied";}catch(System.Runtime.InteropServices.ExternalException){copy.Text="Try again";}};remove.Click+=(s,e)=>{if(snippets)store.Data.Snippets.RemoveAll(v=>v.Id==id);else store.Data.Words.RemoveAll(v=>v.Id==id);if(editing==id)editor.Visible=false;store.Save();};row.Controls.AddRange(new Control[]{cue,preview,edit,copy,remove});list.Controls.Add(row);}
    void ResizeRows(){foreach(Panel row in list.Controls.OfType<Panel>())row.Width=Math.Max(180,list.ClientSize.Width-22);}
    protected override void Dispose(bool disposing){if(disposing)store.Changed-=RefreshList;base.Dispose(disposing);}
}
}
