using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LocalFlow {
public sealed class StreamDeckBridge : IDisposable {
    readonly TcpListener listener;
    readonly SynchronizationContext ui;
    readonly Func<string,object> command;
    readonly string directory,connectionPath;
    readonly string token=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");
    readonly SemaphoreSlim clients=new SemaphoreSlim(4);
    volatile bool stopped;
    public static string DirectoryPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenMicAI");}}
    public static string ConnectionPath {get{return Path.Combine(DirectoryPath,"streamdeck.json");}}
    public StreamDeckBridge(SynchronizationContext context,Func<string,object> handler,string connectionDirectory=null){
        directory=connectionDirectory??DirectoryPath;connectionPath=Path.Combine(directory,"streamdeck.json");
        ui=context;command=handler;listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try {Publish(((IPEndPoint)listener.LocalEndpoint).Port);Task.Run((Action)Accept);}catch{listener.Stop();throw;}
    }
    void Publish(int port){
        Directory.CreateDirectory(directory);
        string temporary=connectionPath+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            FileSecurity security=new FileSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,AccessControlType.Allow));
            using(FileStream file=new FileStream(temporary,FileMode.CreateNew,FileSystemRights.Write,FileShare.None,4096,FileOptions.None,security))
            using(StreamWriter writer=new StreamWriter(file,new UTF8Encoding(false)))writer.Write(new JavaScriptSerializer().Serialize(new {port=port,token=token,application=System.Windows.Forms.Application.ExecutablePath,version=1}));
            if(File.Exists(connectionPath))File.Replace(temporary,connectionPath,null);else File.Move(temporary,connectionPath);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    void Accept(){while(!stopped)try{TcpClient client=listener.AcceptTcpClient();if(!clients.Wait(0)){client.Close();continue;}Task.Run(()=>Serve(client));}catch(SocketException){if(stopped)return;}catch(ObjectDisposedException){return;}}
    void Serve(TcpClient client){try{using(client){client.ReceiveTimeout=1800;client.SendTimeout=1800;using(NetworkStream stream=client.GetStream()){
        MemoryStream bytes=new MemoryStream();int current;string header="";
        while(bytes.Length<4096&&(current=stream.ReadByte())>=0){bytes.WriteByte((byte)current);if(bytes.Length>=4){byte[] buffer=bytes.GetBuffer();int end=(int)bytes.Length;if(buffer[end-4]==13&&buffer[end-3]==10&&buffer[end-2]==13&&buffer[end-1]==10){header=Encoding.UTF8.GetString(buffer,0,end);break;}}}
        string[] lines=header.Split(new[]{"\r\n"},StringSplitOptions.None);string route=lines.Length>0?lines[0]:"";string authorization="";int length=0;bool origin=false;
        foreach(string line in lines){int colon=line.IndexOf(':');if(colon<0)continue;string name=line.Substring(0,colon).Trim(),value=line.Substring(colon+1).Trim();if(name.Equals("Authorization",StringComparison.OrdinalIgnoreCase))authorization=value;if(name.Equals("Content-Length",StringComparison.OrdinalIgnoreCase))if(!Int32.TryParse(value,out length))length=-1;if(name.Equals("Origin",StringComparison.OrdinalIgnoreCase))origin=true;}
        if(origin||authorization!="Bearer "+token){Reply(stream,403,"{\"ok\":false}");return;}
        string action;
        if(route=="GET /status HTTP/1.1")action="status";
        else if(route=="POST /command HTTP/1.1"&&length>0&&length<=256){byte[] body=new byte[length];int offset=0,read;while(offset<length&&(read=stream.Read(body,offset,length-offset))>0)offset+=read;if(offset!=length)return;System.Collections.Generic.Dictionary<string,object> value=new JavaScriptSerializer().DeserializeObject(Encoding.UTF8.GetString(body)) as System.Collections.Generic.Dictionary<string,object>;action=value!=null&&value.ContainsKey("command")?Convert.ToString(value["command"]):"";}
        else{Reply(stream,400,"{\"ok\":false}");return;}
        if(action!="status"&&action!="toggle"&&action!="hold.down"&&action!="hold.up"&&action!="copy"&&action!="open"&&action!="cancel"){Reply(stream,400,"{\"ok\":false}");return;}
        object response=null;ui.Send(_=>{if(!stopped)response=command(action);},null);Reply(stream,200,new JavaScriptSerializer().Serialize(response??new {ok=false}));
    }}}catch(Exception error){if(!(error is IOException)&&!(error is SocketException)&&!(error is ObjectDisposedException))Debug.WriteLine("Stream Deck command rejected: "+error.GetType().Name);}finally{clients.Release();}}
    static void Reply(Stream stream,int status,string text){byte[] body=Encoding.UTF8.GetBytes(text);byte[] headers=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+" Result\r\nContent-Type: application/json\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: "+body.Length+"\r\n\r\n");stream.Write(headers,0,headers.Length);stream.Write(body,0,body.Length);}
    public void Dispose(){stopped=true;listener.Stop();try{Publish(0);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
public static class StreamDeckIntegration {
    public static string Install(){
        string folder=Path.Combine(StreamDeckBridge.DirectoryPath,"Integrations");Directory.CreateDirectory(folder);string destination=Path.Combine(folder,"OpenMicAI.streamDeckPlugin");
        using(Stream source=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenMicAI.StreamDeck")){
            if(source==null)throw new InvalidOperationException("The Stream Deck installer is missing. Download the latest app.");
            using(FileStream output=new FileStream(destination,FileMode.Create,FileAccess.Write))source.CopyTo(output);
        }
        try{Process.Start(new ProcessStartInfo(destination){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){throw new InvalidOperationException("Install the Stream Deck app first, then try again.");}
        return "Confirm in Stream Deck, then drag Dictate onto a key.";
    }
}
}
