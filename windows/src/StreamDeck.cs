using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
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
        string[] lines=header.Split(new[]{"\r\n"},StringSplitOptions.None);string route=lines.Length>0?lines[0]:"";string authorization="",clientName="";int length=0;bool origin=false;
        foreach(string line in lines){int colon=line.IndexOf(':');if(colon<0)continue;string name=line.Substring(0,colon).Trim(),value=line.Substring(colon+1).Trim();if(name.Equals("Authorization",StringComparison.OrdinalIgnoreCase))authorization=value;if(name.Equals("X-OpenMic-Client",StringComparison.OrdinalIgnoreCase))clientName=value;if(name.Equals("Content-Length",StringComparison.OrdinalIgnoreCase))if(!Int32.TryParse(value,out length))length=-1;if(name.Equals("Origin",StringComparison.OrdinalIgnoreCase))origin=true;}
        if(origin||authorization!="Bearer "+token){Reply(stream,403,"{\"ok\":false}");return;}
        if(clientName=="StreamDeck")StreamDeckIntegration.ClientSeen();
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
    public const string PluginId="com.openmicai.controls";
    static long lastSeen;
    public static void ClientSeen(){Interlocked.Exchange(ref lastSeen,DateTime.UtcNow.Ticks);}
    public static bool Connected {get{return DateTime.UtcNow.Ticks-Interlocked.Read(ref lastSeen)<TimeSpan.FromSeconds(4).Ticks;}}
    public static string PluginsPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Elgato","StreamDeck","Plugins");}}
    public static async Task<string> Install(){
        string deck=FindStreamDeck();if(deck==null)throw new InvalidOperationException("Install the Stream Deck app first, then try again.");
        long started=DateTime.UtcNow.Ticks;
        // Elgato restricts its plugin-reload URL to developer mode. Restart the
        // installed app instead; never change developer/security preferences.
        foreach(Process process in Process.GetProcessesByName("StreamDeck"))using(process){
            string path;try{path=process.MainModule.FileName;}catch(System.ComponentModel.Win32Exception){throw new InvalidOperationException("Close Stream Deck, then try Install plugin again.");}
            if(!String.Equals(Path.GetFullPath(path),Path.GetFullPath(deck),StringComparison.OrdinalIgnoreCase))continue;
            process.Kill();if(!await Task.Run(()=>process.WaitForExit(5000)))throw new InvalidOperationException("Stream Deck did not close. Close it, then try again.");
        }
        await Task.Delay(350);
        try{
        using(Stream source=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenMicAI.StreamDeck")){
            if(source==null)throw new InvalidOperationException("The Stream Deck installer is missing. Download the latest app.");
            await Task.Run(()=>StreamDeckPackage.Install(source,PluginsPath));
        }
        }
        finally{started=DateTime.UtcNow.Ticks;Interlocked.Exchange(ref lastSeen,0);Process.Start(new ProcessStartInfo(deck){UseShellExecute=false,CreateNoWindow=true});}
        for(int i=0;i<60;i++){if(Interlocked.Read(ref lastSeen)>=started)return "Installed and connected. Drag Dictate onto a Stream Deck key.";await Task.Delay(250);}
        return "Plugin installed. Restart Stream Deck if Open Mic AI is not in its action list.";
    }
    public static string FindStreamDeck(){
        using(Microsoft.Win32.RegistryKey key=Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"StreamDeck.streamDeckPlugin\shell\open\command")){
            string value=key==null?null:key.GetValue("") as string;if(!String.IsNullOrEmpty(value)&&value.StartsWith("\"")){int end=value.IndexOf('"',1);if(end>1){string candidate=value.Substring(1,end-1);if(Path.GetFileName(candidate).Equals("StreamDeck.exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(candidate))return candidate;}}
        }
        foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}){string candidate=Path.Combine(root,"Elgato","StreamDeck","StreamDeck.exe");if(File.Exists(candidate))return candidate;}return null;
    }
}
public static class StreamDeckPackage {
    public static void Install(Stream package,string pluginsRoot){
        string root=Path.GetFullPath(pluginsRoot);Directory.CreateDirectory(root);
        string target=Path.Combine(root,StreamDeckIntegration.PluginId+".sdPlugin");
        if(Directory.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Remove the development link for Open Mic AI before installing.");
        string staging=Path.Combine(root,".openmicai-install-"+Guid.NewGuid().ToString("N"));string backup=Path.Combine(root,".openmicai-backup-"+Guid.NewGuid().ToString("N"));
        string prefixRoot=root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        foreach(string path in new[]{target,staging,backup})if(!Path.GetFullPath(path).StartsWith(prefixRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Invalid plugin installation folder.");
        Directory.CreateDirectory(staging);bool moved=false;
        try{
            using(ZipArchive archive=new ZipArchive(package,ZipArchiveMode.Read,true)){
                long total=0;int count=0;string prefix=StreamDeckIntegration.PluginId+".sdPlugin/";
                foreach(ZipArchiveEntry entry in archive.Entries){string name=entry.FullName.Replace('\\','/');if(!name.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidDataException("Unexpected plugin package contents.");string relative=name.Substring(prefix.Length);if(relative.Length==0)continue;
                    if(++count>64||(total+=entry.Length)>8*1024*1024||entry.Length>2*1024*1024||(entry.ExternalAttributes>>16&0xF000)==0xA000)throw new InvalidDataException("Invalid plugin package.");
                    foreach(string part in relative.TrimEnd('/').Split('/'))if(part.Length==0||part=="."||part==".."||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("Invalid plugin package path.");
                    string destination=Path.GetFullPath(Path.Combine(staging,relative.Replace('/',Path.DirectorySeparatorChar)));if(!destination.StartsWith(staging+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid plugin package path.");
                    if(name.EndsWith("/")){Directory.CreateDirectory(destination);continue;}Directory.CreateDirectory(Path.GetDirectoryName(destination));using(Stream input=entry.Open())using(FileStream output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write))input.CopyTo(output);
                }
            }
            Dictionary<string,object> manifest=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(staging,"manifest.json")));
            if(!manifest.ContainsKey("UUID")||Convert.ToString(manifest["UUID"])!=StreamDeckIntegration.PluginId||!File.Exists(Path.Combine(staging,"bin","plugin.mjs")))throw new InvalidDataException("The Open Mic AI plugin is incomplete.");
            if(Directory.Exists(target)){Directory.Move(target,backup);moved=true;}
            try{Directory.Move(staging,target);}catch{if(moved)Directory.Move(backup,target);throw;}
            if(moved)try{Directory.Delete(backup,true);}catch(IOException){Debug.WriteLine("Old Open Mic AI plugin backup is still in use.");}catch(UnauthorizedAccessException){Debug.WriteLine("Old Open Mic AI plugin backup could not be removed.");}
        }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
    }
}
}
