using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LocalFlow {
public static class TextClipboard {
    static string[] Cells(string line){line=line.Trim();return line.StartsWith("|")&&line.EndsWith("|")?line.Substring(1,line.Length-2).Split('|').Select(c=>c.Trim()).ToArray():null;}
    static string Row(string[] cells,bool header){string tag=header?"th":"td";return "<tr>"+String.Join("",cells.Select(c=>"<"+tag+" style=\"border:1px solid #cccccc;padding:6px;text-align:left\">"+WebUtility.HtmlEncode(c)+"</"+tag+">"))+"</tr>";}
    public static string TableHtml(string text){
        string[] lines=(text??"").Replace("\r","").Split('\n');StringBuilder html=new StringBuilder();bool found=false;
        for(int i=0;i<lines.Length;i++){
            string[] header=Cells(lines[i]),separator=i+1<lines.Length?Cells(lines[i+1]):null;
            if(header!=null&&header.Length>=2&&separator!=null&&separator.Length==header.Length&&separator.All(c=>Regex.IsMatch(c,@"^:?-{3,}:?$"))){
                List<string[]> rows=new List<string[]>();int next=i+2;
                while(next<lines.Length){string[] row=Cells(lines[next]);if(row==null)break;if(row.Length!=header.Length)return null;rows.Add(row);next++;}
                if(rows.Count==0)return null;
                html.Append("<table style=\"border-collapse:collapse\"><thead>").Append(Row(header,true)).Append("</thead><tbody>");foreach(string[] row in rows)html.Append(Row(row,false));html.Append("</tbody></table>");i=next-1;found=true;
            }else if(!String.IsNullOrWhiteSpace(lines[i]))html.Append("<p>").Append(WebUtility.HtmlEncode(lines[i])).Append("</p>");
        }
        return found?html.ToString():null;
    }
    public static string HtmlClipboard(string fragment){
        const string template="Version:0.9\r\nStartHTML:{0:0000000000}\r\nEndHTML:{1:0000000000}\r\nStartFragment:{2:0000000000}\r\nEndFragment:{3:0000000000}\r\n";
        string prefix="<html><body><!--StartFragment-->",suffix="<!--EndFragment--></body></html>";
        int start=Encoding.UTF8.GetByteCount(String.Format(template,0,0,0,0)),fragmentStart=start+Encoding.UTF8.GetByteCount(prefix),fragmentEnd=fragmentStart+Encoding.UTF8.GetByteCount(fragment),end=fragmentEnd+Encoding.UTF8.GetByteCount(suffix);
        return String.Format(template,start,end,fragmentStart,fragmentEnd)+prefix+fragment+suffix;
    }
    public static void SetText(string text){
        string html=TableHtml(text);if(html==null){Clipboard.SetText(text);return;}
        DataObject data=new DataObject();data.SetText(text,TextDataFormat.UnicodeText);data.SetData(DataFormats.Html,HtmlClipboard(html));Clipboard.SetDataObject(data,true);
    }
}
}
