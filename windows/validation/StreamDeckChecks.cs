using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using LocalFlow;

public static class StreamDeckChecks {
    static int checks,commands;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static int Send(int port,string token,string route,string body=null,string origin=null){HttpWebRequest request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port+route);request.Proxy=null;request.Timeout=3000;request.Headers["Authorization"]="Bearer "+token;if(origin!=null)request.Headers["Origin"]=origin;if(body!=null){request.Method="POST";byte[] data=Encoding.UTF8.GetBytes(body);request.ContentLength=data.Length;request.ContentType="application/json";using(Stream stream=request.GetRequestStream())stream.Write(data,0,data.Length);}try{using(HttpWebResponse response=(HttpWebResponse)request.GetResponse())return (int)response.StatusCode;}catch(WebException error){using(HttpWebResponse response=(HttpWebResponse)error.Response)return (int)response.StatusCode;}}
    public static void Main(string[] args){string folder=Path.Combine(Path.GetFullPath(args[0]),"streamdeck-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{
        using(StreamDeckBridge bridge=new StreamDeckBridge(new SynchronizationContext(),action=>{if(action!="status")commands++;return new {ok=true,ready=true,phase="idle",hasRecent=true};},folder)){
            Dictionary<string,object> config=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(folder,"streamdeck.json")));int port=Convert.ToInt32(config["port"]);string token=(string)config["token"];
            Check(token.Length==64&&port>0,"Local connection published");
            Check(Send(port,token,"/status")==200,"Authenticated status");
            Check(Send(port,"wrong","/status")==403,"Reject wrong token");
            Check(Send(port,token,"/status",null,"https://example.test")==403,"Reject browser origins");
            Check(Send(port,token,"/command","{\"command\":\"shell\"}")==400,"Reject unknown commands");
            Check(Send(port,token,"/command",new String('x',300))==400,"Bound command size");
            foreach(string action in new[]{"toggle","hold.down","hold.up","copy","open","cancel"})Check(Send(port,token,"/command","{\"command\":\""+action+"\"}")==200,"Dispatch "+action);
            Check(commands==6,"Unauthorized requests cannot trigger actions");
        }
        Dictionary<string,object> closed=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(folder,"streamdeck.json")));Check(Convert.ToInt32(closed["port"])==0,"Shutdown marks connection offline");
        Console.WriteLine(checks+" Stream Deck checks passed");
    }finally{if(folder.StartsWith(Path.GetFullPath(args[0])+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))Directory.Delete(folder,true);}}
}
