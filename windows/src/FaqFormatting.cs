using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LocalFlow {
public static class FaqFormatting {
    public sealed class Pair {public string question,answer;}
    public sealed class Document {public string before,after;public Pair[] pairs;}
    public static string Render(string json){
        Document document;try{document=new JavaScriptSerializer().Deserialize<Document>(json);}catch{return null;}
        if(document==null||document.pairs==null||document.pairs.Length==0||document.pairs.Length>40||document.pairs.Any(p=>p==null||String.IsNullOrWhiteSpace(p.question)||p.question.IndexOfAny(new[]{'\r','\n'})>=0||p.answer==null))return null;
        string body=String.Join("\n\n",document.pairs.Select(p=>p.question.Trim().TrimEnd('.','?',':')+"?"+(String.IsNullOrWhiteSpace(p.answer)?"":"\n"+p.answer.Trim())));
        string[] words=Regex.Matches(body,@"[\p{L}\p{M}\p{N}_]+(?:['’][\p{L}\p{M}\p{N}_]+)*").Cast<Match>().Select(m=>m.Value).ToArray();
        Match[] prefix=Regex.Matches(document.before??"",@"[\p{L}\p{M}\p{N}_]+(?:['’][\p{L}\p{M}\p{N}_]+)*").Cast<Match>().ToArray();
        // Remove only an exact copy of the pairs from the introduction. The
        // caller still checks the complete rendered wording or meaning.
        if(words.Length>0&&prefix.Length>=words.Length&&words.Select((word,i)=>String.Equals(word,prefix[prefix.Length-words.Length+i].Value,StringComparison.OrdinalIgnoreCase)).All(s=>s))document.before=document.before.Substring(0,prefix[prefix.Length-words.Length].Index).TrimEnd();
        return String.Join("\n\n",new[]{document.before,body,document.after}.Where(t=>!String.IsNullOrWhiteSpace(t)).Select(t=>t.Trim()));
    }
}
}
