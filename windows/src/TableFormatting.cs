using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LocalFlow {
public static class TableFormatting {
    public sealed class Plan {public string before,after;public string[] headers;public string[][] rows;}
    public static string Render(string json,bool nameEmptyHeaders=false){
        Plan plan;try{plan=new JavaScriptSerializer().Deserialize<Plan>(json);}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
        if(plan==null||plan.headers==null||plan.headers.Length<2||plan.headers.Length>12||plan.rows==null||plan.rows.Length==0||plan.rows.Any(r=>r==null||r.Length!=plan.headers.Length))return null;
        if(plan.headers.Concat(plan.rows.SelectMany(r=>r)).Any(c=>c==null||c.Contains("|")||c.Contains("\n")||c.Contains("\r")))return null;
        // Small models sometimes copy the complete source into 'before' while
        // also placing the same words in cells. Remove only an exact duplicate
        // suffix; the caller still verifies the whole rendered result.
        string[] body=Regex.Matches(String.Join(" ",plan.headers.Concat(plan.rows.SelectMany(r=>r))),@"[\p{L}\p{M}\p{N}_]+(?:['’][\p{L}\p{M}\p{N}_]+)*").Cast<Match>().Select(m=>m.Value).ToArray();
        Match[] prefix=Regex.Matches(plan.before??"",@"[\p{L}\p{M}\p{N}_]+(?:['’][\p{L}\p{M}\p{N}_]+)*").Cast<Match>().ToArray();
        if(body.Length>0)for(int start=0;start<=prefix.Length-body.Length;start++)if(body.Select((word,i)=>String.Equals(word,prefix[start+i].Value,StringComparison.OrdinalIgnoreCase)).All(s=>s)){
            Match last=prefix[start+body.Length-1];string closing=plan.before.Substring(last.Index+last.Length).TrimStart(' ','.',',',';',':','\r','\n');
            plan.before=plan.before.Substring(0,prefix[start].Index).TrimEnd();
            if(closing.Length>0&&!String.Equals(closing.Trim(),(plan.after??"").Trim(),StringComparison.OrdinalIgnoreCase))plan.after=String.Join("\n\n",new[]{closing,plan.after}.Where(t=>!String.IsNullOrWhiteSpace(t)));
            break;
        }
        if(nameEmptyHeaders)for(int i=0;i<plan.headers.Length;i++)if(String.IsNullOrWhiteSpace(plan.headers[i]))plan.headers[i]=i==0?"Name":"Value "+(i+1);
        StringBuilder output=new StringBuilder();if(!String.IsNullOrWhiteSpace(plan.before))output.Append(plan.before.Trim()).Append("\n\n");
        output.Append("| ").Append(String.Join(" | ",plan.headers.Select(c=>c.Trim()))).Append(" |\n| ").Append(String.Join(" | ",plan.headers.Select(c=>"---"))).Append(" |\n");
        foreach(string[] row in plan.rows)output.Append("| ").Append(String.Join(" | ",row.Select(c=>c.Trim()))).Append(" |\n");
        if(!String.IsNullOrWhiteSpace(plan.after))output.Append("\n").Append(plan.after.Trim());return output.ToString().TrimEnd();
    }
}
}
