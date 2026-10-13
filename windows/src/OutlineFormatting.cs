using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LocalFlow {
public static class OutlineFormatting {
    public sealed class Group {public string topic;public string[] items;}
    public sealed class Document {public string before,after;public Group[] groups;}
    static bool Line(string text){return !String.IsNullOrWhiteSpace(text)&&text.IndexOfAny(new[]{'\r','\n'})<0;}
    public static string Render(string json){
        Document document;try{document=new JavaScriptSerializer().Deserialize<Document>(json);}catch{return null;}
        if(document==null||document.groups==null||document.groups.Length==0||document.groups.Length>40||document.groups.Any(g=>g==null||!Line(g.topic)||g.items==null||g.items.Length==0||g.items.Any(i=>!Line(i))))return null;
        // Remove only source topic clauses repeated by their matching group.
        foreach(Group group in document.groups){
            string topic=Regex.Replace(group.topic.Trim(),@"^(?:under\s+)|\s+(?:include|cover|put)$","",RegexOptions.IgnoreCase);
            Func<string,string> phrase=value=>String.Join(@"[\s,.:;]+(?:(?:the|a|an)\s+)?",Regex.Matches(Regex.Replace(value,@"^and\s+","",RegexOptions.IgnoreCase),@"[\p{L}\p{M}\p{N}_]+(?:['’][\p{L}\p{M}\p{N}_]+)*").Cast<Match>().Select(m=>Regex.Escape(m.Value)));
            string pattern=@"\b(?:under|first topic|second topic|final topic)\s+"+phrase(topic)+@"\s+(?:include|cover|put|with subpoints?)\s+(?:(?:the|a|an)\s+)?"+String.Join(@"[\s,.;]*(?:and\s+)?(?:(?:the|a|an)\s+)?",group.items.Select(phrase))+@"[.]?";
            document.before=Regex.Replace(document.before??"",pattern,"",RegexOptions.IgnoreCase).Trim();
        }
        document.before=Regex.Replace(document.before??"",@"[ \t]{2,}"," ").Trim();
        string body=String.Join("\n",document.groups.Select(g=>"- "+g.topic.Trim()+"\n"+String.Join("\n",g.items.Select(i=>"  - "+i.Trim()))));
        return String.Join("\n\n",new[]{document.before,body,document.after}.Where(t=>!String.IsNullOrWhiteSpace(t)).Select(t=>t.Trim()));
    }
}
}
