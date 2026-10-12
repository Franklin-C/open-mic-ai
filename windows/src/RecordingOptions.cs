using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace LocalFlow {
public static class Hotkey {
    public static bool Mouse(string value){return value=="Mouse 4"||value=="Mouse 5";}
    public static bool Parse(string value,out uint modifiers,out uint key){
        modifiers=0x4000;key=0;if(String.IsNullOrWhiteSpace(value))return false;
        foreach(string part in value.Split('+').Select(p=>p.Trim())){
            if(part=="Ctrl")modifiers|=2;else if(part=="Alt")modifiers|=1;else if(part=="Shift")modifiers|=4;else if(part=="Win")modifiers|=8;
            else {Keys parsed;if(key!=0||!Enum.TryParse<Keys>(part,true,out parsed))return false;key=(uint)(parsed&Keys.KeyCode);}
        }
        return key!=0&&key!=27&&key!=0x10&&key!=0x11&&key!=0x12&&(key<0x5B||key>0x5C)&&((modifiers&15)!=0||(key>=0x70&&key<=0x87));
    }
    public static string Format(Keys data){Keys key=data&Keys.KeyCode;if(key==Keys.ControlKey||key==Keys.ShiftKey||key==Keys.Menu||key==Keys.LWin||key==Keys.RWin)return null;string label=((data&Keys.Control)!=0?"Ctrl + ":"")+((data&Keys.Alt)!=0?"Alt + ":"")+((data&Keys.Shift)!=0?"Shift + ":"")+key;uint mods,code;return Parse(label,out mods,out code)?label:null;}
}
public sealed class ShortcutPicker : ModernButton {
    public string Value {get;private set;}public bool Capturing {get;private set;}
    public ShortcutPicker(string value){Value=value;Text=value;Name="Record shortcut";AccessibleName="Record shortcut";AutoSize=false;Size=new Size(570,44);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderColor=Theme.Line;BackColor=Theme.Surface;Cursor=Cursors.Hand;Margin=new Padding(0,0,0,12);}
    protected override void OnClick(EventArgs e){base.OnClick(e);if(!Capturing){Capturing=true;ShortcutWindow.Capturing=true;Text="Press your shortcut… (Esc cancels)";Focus();}}
    void Finish(string value){if(value!=null)Value=value;Capturing=false;ShortcutWindow.Capturing=false;Text=Value;}
    protected override bool ProcessCmdKey(ref Message msg,Keys data){if(Capturing){if((data&Keys.KeyCode)==Keys.Escape){Finish(null);return true;}string value=Hotkey.Format(data);if(value!=null)Finish(value);else Text="Use Ctrl, Alt or Shift + a key, or F1–F24";return true;}return base.ProcessCmdKey(ref msg,data);}
    protected override void OnMouseDown(MouseEventArgs e){if(Capturing&&(e.Button==MouseButtons.XButton1||e.Button==MouseButtons.XButton2)){Finish(e.Button==MouseButtons.XButton1?"Mouse 4":"Mouse 5");return;}base.OnMouseDown(e);}
    protected override void OnLostFocus(EventArgs e){if(Capturing)Finish(null);base.OnLostFocus(e);}
    protected override void Dispose(bool disposing){if(Capturing)Finish(null);base.Dispose(disposing);}
}
public static class RecordingModes {
    public static readonly string[] Names={"Click to start / stop","Hold to talk","Automatic"};
    public static string Normalize(string value){return Names.Contains(value)?value:Names[0];}
    // Measured audio time makes silence detection independent of UI polling and inference speed.
    public static bool ShouldStop(string mode,double duration,bool speech,double silence){return mode=="Automatic"&&(speech?silence>=1.4:duration>=10);}
}
public sealed class SpokenAction {
    public string Action="Dictate",Language,Instruction;
    public bool ShowsCard {get{return Action=="Translate"||Action=="Summarize";}}
    public static SpokenAction Parse(string command,string fallbackLanguage){
        string text=(command??"").Trim();if(text.Length==0)throw new InvalidOperationException("Say what you want to change.");if(text.Length>2000)throw new InvalidOperationException("Use a shorter voice instruction.");
        string start=Regex.Replace(text,@"^(?:(?:please|can you|could you|would you|I want you to)\s+)+","",RegexOptions.IgnoreCase);
        SpokenAction result=new SpokenAction {Language=TextActions.Language(fallbackLanguage),Instruction=text};
        if(Regex.IsMatch(start,@"^(?:translate|translation|give me (?:a |the )?translation)\b",RegexOptions.IgnoreCase)){
            result.Action="Translate";Match language=Regex.Match(start,@"\b(?:into|to|in)\s+([\p{L}][\p{L}\p{M} ()-]*?)(?:[,.!?;]|\s+(?:and|using|with|as|but|please)\b|$)",RegexOptions.IgnoreCase);
            if(language.Success)result.Language=TextActions.Language(language.Groups[1].Value);
        }else if(Regex.IsMatch(start,@"^(?:summari[sz]e|summary|give me (?:a |the )?(?:short |brief |quick )?summary|make (?:this|it) (?:a |into a )?summary)\b",RegexOptions.IgnoreCase))result.Action="Summarize";
        else if(Regex.IsMatch(start,@"^(?:rewrite|reword|rephrase|edit|polish|format|improve|shorten|lengthen|simplify|fix|correct|change|turn|make|add|append|remove|replace|convert)\b",RegexOptions.IgnoreCase))result.Action="Rewrite";
        return result;
    }
}
}
