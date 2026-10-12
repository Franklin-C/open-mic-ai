using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using LocalFlow;

public static class PolishRetryChecks {
    static string Completion(string text){return new JavaScriptSerializer().Serialize(new {choices=new[]{new {finish_reason="stop",message=new {content=text}}}});}
    static async Task Respond(TcpListener listener,string[] replies,List<string> requests){
        foreach(string reply in replies)using(TcpClient client=await listener.AcceptTcpClientAsync())using(NetworkStream stream=client.GetStream()){
            byte[] buffer=new byte[8192];using(MemoryStream bytes=new MemoryStream()){
                int expected=-1,end=-1;while(expected<0||bytes.Length<expected){int read=await stream.ReadAsync(buffer,0,buffer.Length);if(read==0)throw new IOException("Fixture disconnected.");bytes.Write(buffer,0,read);string header=Encoding.ASCII.GetString(bytes.ToArray());end=header.IndexOf("\r\n\r\n",StringComparison.Ordinal);if(end>=0&&expected<0){Match length=Regex.Match(header.Substring(0,end),@"Content-Length:\s*(\d+)",RegexOptions.IgnoreCase);expected=end+4+int.Parse(length.Groups[1].Value);}}
                requests.Add(Encoding.UTF8.GetString(bytes.ToArray(),end+4,expected-end-4));
            }
            byte[] body=Encoding.UTF8.GetBytes(reply),headers=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nConnection: close\r\nContent-Length: "+body.Length+"\r\n\r\n");await stream.WriteAsync(headers,0,headers.Length);await stream.WriteAsync(body,0,body.Length);
        }
    }
    static async Task<string> Fixture(Models models,string source,string[] responses,Action<List<string>> inspect,bool selected=false,bool reworded=false){
        TcpListener listener=new TcpListener(IPAddress.Loopback,0);listener.Start();FieldInfo address=typeof(Models).GetField("rewriteUrl",BindingFlags.Instance|BindingFlags.NonPublic);string original=(string)address.GetValue(models);
        using(CancellationTokenSource cancel=new CancellationTokenSource())try{
            address.SetValue(models,"http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port);List<string> requests=new List<string>();Task server=Respond(listener,new[]{Completion("{\"layout\":\""+(source.Contains("grocery")?"list":"prose")+"\"}")}.Concat(responses).ToArray(),requests);Task<string> rewrite=selected?models.TransformSelection(source,"Rewrite","Smart formatting","","English",cancel.Token,"",reworded):models.Rewrite(source,"Smart formatting","",cancel.Token,"","Medium",reworded);Task finished=Task.WhenAll(server,rewrite);
            if(await Task.WhenAny(finished,Task.Delay(8000))!=finished)throw new TimeoutException("Retry fixture did not finish.");await finished;requests.RemoveAt(0);inspect(requests);return await rewrite;
        }finally{cancel.Cancel();address.SetValue(models,original);listener.Stop();}
    }
    public static async Task Run(Models models,Action<bool,string> check){
        string source="I want to make a grocery list with apples and two strawberries.",full="I want to make a grocery list with:\n\n- Apples\n- Two strawberries",bare="- Apples\n- Two strawberries";
        string result=await Fixture(models,source,new[]{Completion(bare)},requests=>check(requests.Count==1,"My words recovers the original introduction without an extra generation"));check(result==full,"Source-word layout recovery preserves the introduction and formatted items");
        result=await Fixture(models,source,new[]{Completion("Invented unrelated prose."),Completion("Invented unrelated prose.")},requests=>check(requests.Count==2,"An unrepaired loss cannot cause an endless retry"));check(result==source,"Failed repair retains the full original transcript");
        string conditional="Please get apples for the picnic. If they are not ripe, skip them.";
        result=await Fixture(models,conditional,new[]{Completion("Please get apples for the picnic.")},requests=>check(requests.Count==1,"My words restores omitted conditional content from the original source"));check(result==conditional,"The restored condition remains intact");
        result=await Fixture(models,source,new[]{Completion(full),Completion("{\"safe\":true,\"complete\":false}"),Completion(full),Completion("{\"safe\":true,\"complete\":true}")},requests=>check(requests.Count==4,"Reworded completeness rejection triggers bounded repair"),false,true);check(result==full,"Reworded repair must pass both factual and completeness checks");
        check(Formatting.PolishPersonalContextPreserved("I want you to make this a bullet list.","- Apples\n- Bananas"),"Explicit editing commands do not force a redundant intention sentence");
        check(Formatting.PolishPersonalContextPreserved("I'd like to make a shopping list.","I would like to make a shopping list."),"Equivalent personal contractions are accepted");
        check(Formatting.PolishContextAnchorsPreserved(conditional,"Please get ripe apples for the picnic; skip unripe ones."),"Conditions can be reworded while their key details remain");
        check(Formatting.PolishWordsPreserved("bring milk eggs and bread","Bring:\n\n- Milk\n- Eggs\n- Bread"),"Capitalization, punctuation, bullets and list connectors are allowed");
        check(Formatting.PolishWordsPreserved("first connect the cable then print a page","1. First, connect the cable.\n2. Then print a page."),"List numbers are formatting markers rather than added quantities");
        check(Formatting.PolishWordsPreserved("project status the beta is ready blockers the export fails","Project status\n\nThe beta is ready.\n\nBlockers\n\nThe export fails."),"Headings can reuse existing source words without duplication");
        check(Formatting.PolishWordsPreserved("I'd like two folders","I\u2019d like two folders."),"Typographic apostrophes preserve the same contraction");
        check(!Formatting.PolishWordsPreserved(source,full+"\nEnjoy!"),"New commentary is rejected even without an invented fact");
        check(!Formatting.PolishWordsPreserved("The screen is good.","The screen is excellent."),"Synonyms are rejected in Polish");
        check(!Formatting.PolishWordsPreserved("I don't want two folders.","I do not want two folders."),"Expanded contractions are rejected in Polish");
        check(!Formatting.PolishWordsPreserved("The printer don't work.","The printer doesn't work."),"Grammar substitutions are rejected in Polish");
        check(!Formatting.PolishWordsPreserved("Bring two folders.","Bring 2 folders."),"Changing number words into digits is rejected");
        check(!Formatting.PolishWordsPreserved("Bring apples and bananas.","Grocery list\n\n- Apples\n- Bananas"),"Invented heading labels are rejected");
        check(!Formatting.PolishWordsPreserved("Nina will call Alex.","Alex will call Nina."),"Reordering existing words to change the meaning is rejected");
        check(!Formatting.PolishWordsPreserved("Project status the beta is ready.","Project status\n\nProject status: the beta is ready."),"Duplicated heading words are rejected");
        check(!Formatting.PolishWordsPreserved("Bring apples.","- Apples\n- Apples"),"Repeating an existing item is rejected");
        check(!Formatting.PolishWordsPreserved("Use code 7319 and LF_SNIP_ABC.","Use code 7318 and LF_SNIP_XYZ."),"Codes and snippet placeholders must retain their exact words");
        check(!Formatting.PolishWordsPreserved("Bring folders.","- Two folders"),"Added quantity words are rejected");
        check(!Formatting.PolishWordsPreserved("Bring folders.","1. Bring 2 folders."),"Numbering does not permit adding quantities inside a step");
        result=await Fixture(models,source,new[]{Completion(full+"\nEnjoy!")},requests=>check(requests.Count==1,"Added model commentary is removed by source-word rendering"));check(result==full,"Useful layout survives removal of added commentary");
        result=await Fixture(models,source,new[]{Completion(source),Completion(full)},requests=>check(requests.Count==2,"A dense paragraph triggers one repair when a list was chosen"));check(result==full,"The repaired output uses the intended list layout");
        result=await Fixture(models,source,new[]{Completion(full+"\nEnjoy!")},requests=>{},true);check(result==full,"Selected-text My words retains formatting while removing new wording");
        check(Formatting.PreparePolish("Add the following bananas, pears, three apples. Actually two bananas.").Text=="Add the following two bananas, pears, three apples.","A quantity correction updates an earlier list item");
        check(Formatting.PreparePolish("Add bananas, pears. Actually two bananas. Actually make that three bananas.").Text=="Add three bananas, pears.","Repeated corrections use the final quantity");
        check(Formatting.PreparePolish("We need apples. Actually two pears.").Text.Contains("Actually two pears"),"An unmatched new item is not forced onto a different item");
        string checklist=WordLayout.Project("Travel checklist pack the passport and charge the camera before Friday.","Travel checklist\n\n- [ ] Pack the passport\n- [ ] Charge the camera before Friday.");
        check(PolishLayout.Fits("checklist",checklist)&&Formatting.PolishContentComplete("Travel checklist pack the passport and charge the camera before Friday.",checklist),"Source-word recovery retains checklist boxes and all task details");
        string outline=WordLayout.Project("Talk local privacy offline recording speed loaded models.","Talk\n\n- Local privacy\n  - Offline recording\n- Speed\n  - Loaded models");
        check(outline.Contains("\n  - ")&&Formatting.PolishContentComplete("Talk local privacy offline recording speed loaded models.",outline),"Source-word recovery preserves outline nesting");
        check(PolishLayout.Choices.Length==PolishLayout.Names.Length+1&&PolishLayout.Selected("Table")=="table"&&PolishLayout.Normalize("Old invalid format")=="Auto","All formats are selectable with a safe default for old settings");
        string markdown="Comparison\n\n| Name | Item |\n| --- | --- |\n| Zoë | <script>& café |";
        string html=TextClipboard.TableHtml(markdown),clipboard=TextClipboard.HtmlClipboard(html);
        check(html.Contains("<table")&&html.Contains("&lt;script&gt;&amp;")&&!html.Contains("<script>"),"Rich tables escape user content and retain the surrounding text");
        byte[] bytes=Encoding.UTF8.GetBytes(clipboard);int start=int.Parse(Regex.Match(clipboard,@"StartFragment:(\d+)").Groups[1].Value),end=int.Parse(Regex.Match(clipboard,@"EndFragment:(\d+)").Groups[1].Value);
        check(Encoding.UTF8.GetString(bytes,start,end-start)==html&&int.Parse(Regex.Match(clipboard,@"EndHTML:(\d+)").Groups[1].Value)==bytes.Length,"HTML clipboard offsets use UTF-8 bytes for multilingual content");
        check(TextClipboard.TableHtml("Plain sentence.")==null&&TextClipboard.TableHtml("| A | B |\n| --- | --- |\n| One | Two | Three |")==null,"Ordinary text and inconsistent tables retain plain-text copying");
        string table=TableFormatting.Render("{\"before\":\"Comparison table. Plan price storage. Basic five dollars ten gigabytes.\",\"headers\":[\"Plan\",\"price\",\"storage\"],\"rows\":[[\"Basic\",\"five dollars\",\"ten gigabytes\"]],\"after\":\"\"}");
        check(PolishLayout.Fits("table",table)&&Formatting.PolishContentComplete("Comparison table. Plan price storage. Basic five dollars ten gigabytes.",table),"Structured tables remove exact duplicated introductory content without losing source words");
        check(TableFormatting.Render("{\"before\":\"\",\"headers\":[\"A\",\"B\"],\"rows\":[[\"One\"]],\"after\":\"\"}")==null,"A mismatched cell count cannot produce a malformed table");
        check(PolishLayout.Explicit("Travel checklist. Pack the passport.")=="checklist"&&PolishLayout.Explicit("The cafe had tables and chairs.")==null,"Explicit format requests route directly without turning ordinary nouns into a layout");
    }
}
