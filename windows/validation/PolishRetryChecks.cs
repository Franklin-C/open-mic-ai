using System;
using System.Collections.Generic;
using System.IO;
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
    static async Task<string> Fixture(Models models,string source,string[] responses,Action<List<string>> inspect,bool selected=false){
        TcpListener listener=new TcpListener(IPAddress.Loopback,0);listener.Start();FieldInfo address=typeof(Models).GetField("rewriteUrl",BindingFlags.Instance|BindingFlags.NonPublic);string original=(string)address.GetValue(models);
        using(CancellationTokenSource cancel=new CancellationTokenSource())try{
            address.SetValue(models,"http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port);List<string> requests=new List<string>();Task server=Respond(listener,responses,requests);Task<string> rewrite=selected?models.TransformSelection(source,"Rewrite","Smart formatting","","English",cancel.Token):models.Rewrite(source,"Smart formatting","",cancel.Token,"","Medium");Task finished=Task.WhenAll(server,rewrite);
            if(await Task.WhenAny(finished,Task.Delay(8000))!=finished)throw new TimeoutException("Retry fixture did not finish.");await finished;inspect(requests);return await rewrite;
        }finally{cancel.Cancel();address.SetValue(models,original);listener.Stop();}
    }
    public static async Task Run(Models models,Action<bool,string> check){
        string source="I want to make a grocery list with apples and two strawberries.",full="I want to make a grocery list with:\n\n- Apples\n- Two strawberries",bare="- Apples\n- Two strawberries";
        string result=await Fixture(models,source,new[]{Completion(bare),Completion(full),Completion("{\"safe\":true,\"complete\":true}")},requests=>check(requests.Count==3&&requests[1].Contains(source),"A dropped introduction triggers one retry using the complete original source"));check(result==full,"A verified repair preserves the introduction and formatted items");
        result=await Fixture(models,source,new[]{Completion(bare),Completion(bare)},requests=>check(requests.Count==2,"An unrepaired loss cannot cause an endless retry"));check(result==source,"Failed repair retains the full original transcript");
        string conditional="Please get apples for the picnic. If they are not ripe, skip them.";
        result=await Fixture(models,conditional,new[]{Completion("Please get apples for the picnic."),Completion(conditional),Completion("{\"safe\":true,\"complete\":true}")},requests=>check(requests.Count==3,"A lost conditional detail also triggers the bounded repair"));check(result==conditional,"The repaired condition remains intact");
        result=await Fixture(models,source,new[]{Completion(full),Completion("{\"safe\":true,\"complete\":false}"),Completion(full),Completion("{\"safe\":true,\"complete\":true}")},requests=>check(requests.Count==4,"Semantic completeness rejection triggers repair despite supported facts"));check(result==full,"Repair must pass both factual and completeness checks");
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
        result=await Fixture(models,source,new[]{Completion(full+"\nEnjoy!"),Completion(full),Completion("{\"safe\":true,\"complete\":true}")},requests=>check(requests.Count==3,"Added words trigger repair before consulting the semantic checker"));check(result==full,"Only the wording-preserving repaired output is accepted");
        result=await Fixture(models,source,new[]{Completion(full+"\nEnjoy!"),Completion(full+"\nEnjoy!")},requests=>check(requests.Count==2,"Repeated added wording receives no semantic approval or extra retry"));check(result==source,"Added wording on both attempts falls back to the original transcript");
        bool blocked=false;try{await Fixture(models,source,new[]{Completion(full+"\nEnjoy!")},requests=>{},true);}catch(InvalidOperationException){blocked=true;}check(blocked,"Selected-text Polish rejects added words and keeps the source unchanged");
    }
}
