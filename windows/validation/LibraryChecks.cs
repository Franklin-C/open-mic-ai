using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalFlow;

public static class LibraryChecks {
    sealed class Server : IDisposable {
        readonly TcpListener listener=new TcpListener(IPAddress.Loopback,0);
        readonly byte[] data; readonly bool ranges,slow;
        public string Url;
        public long RequestedOffset=-1;
        public int Requests;
        readonly Task worker;
        public Server(byte[] content,bool range=true,bool delay=false) {
            data=content;ranges=range;slow=delay;listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/fixture";
            worker=Task.Run(async()=>{
                try {while(true){using(TcpClient client=await listener.AcceptTcpClientAsync())using(NetworkStream stream=client.GetStream()) {
                    StreamReader reader=new StreamReader(stream,Encoding.ASCII,false,4096,true);long requested=0;string line;
                    while(!String.IsNullOrEmpty(line=await reader.ReadLineAsync()))if(line.StartsWith("Range: bytes=",StringComparison.OrdinalIgnoreCase))requested=Int64.Parse(line.Substring(13).Split('-')[0]);
                    RequestedOffset=requested;Requests++;long offset=ranges?requested:0;
                    string headers="HTTP/1.1 "+(offset>0?"206 Partial Content":"200 OK")+"\r\nContent-Length: "+(data.Length-offset)+"\r\n"+(offset>0?"Content-Range: bytes "+offset+"-"+(data.Length-1)+"/"+data.Length+"\r\n":"")+"Connection: close\r\n\r\n";
                    byte[] header=Encoding.ASCII.GetBytes(headers);await stream.WriteAsync(header,0,header.Length);
                    for(long i=offset;i<data.Length;i+=16384){await stream.WriteAsync(data,(int)i,(int)Math.Min(16384,data.Length-i));if(slow)await Task.Delay(15);}
                }}}
                catch(ObjectDisposedException){}catch(SocketException){}catch(IOException){}
            });
        }
        public void Dispose(){listener.Stop();}
    }
    static string Hash(byte[] data){using(SHA256 sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
    public static async Task Run(string root,Dictionary<string,object> report,Action<bool,string> check) {
        ComputerSpecs specs=ComputerSpecs.Detect();report["computer_specs"]=new {ram=specs.RamBytes,gpu=specs.Gpu,gpu_bytes=specs.GpuBytes,cpu_threads=specs.CpuThreads,recommended=ModelLibrary.Recommend(specs).Id};
        check(specs.RamBytes>0&&specs.GpuBytes>10L*1073741824&&specs.Gpu.Contains("5070"),"DXGI detects RTX 5070 memory without 32-bit truncation");
        check(ModelLibrary.Recommend(specs).Id=="balanced","Balanced recommended for this computer");
        check(ModelLibrary.Recommend(new ComputerSpecs {RamBytes=8L*1073741824,GpuBytes=0}).Id=="compact","Compact recommended for modest computer");
        check(ModelLibrary.Recommend(new ComputerSpecs {RamBytes=16L*1073741824,GpuBytes=6L*1073741824}).Id=="balanced","Memory recommendation threshold");
        string folder=Path.Combine(Path.GetDirectoryName(report.ContainsKey("fixture_path")?(string)report["fixture_path"]:root),"download-fixture");Directory.CreateDirectory(folder);
        byte[] data=new byte[1024*1024];new Random(7319).NextBytes(data);string hash=Hash(data),destination=Path.Combine(folder,"fixture.gguf");
        using(HttpClient http=new HttpClient(new HttpClientHandler {UseProxy=false})) {
            using(Server server=new Server(data)) {
                File.WriteAllBytes(destination+".partial",data.Take(131072).ToArray());
                await ModelLibrary.Download(http,server.Url,destination,data.Length,hash,null,CancellationToken.None);
                check(server.RequestedOffset==131072&&await ModelLibrary.Verify(destination,data.Length,hash,CancellationToken.None),"Interrupted model download resumes and verifies");
                await ModelLibrary.Download(http,server.Url,destination,data.Length,hash,null,CancellationToken.None);
                check(server.Requests==1,"Verified installed model avoids network download");
            }
            File.Delete(destination);
            using(Server server=new Server(data,false)) {
                File.WriteAllBytes(destination+".partial",data.Take(65536).ToArray());
                await ModelLibrary.Download(http,server.Url,destination,data.Length,hash,null,CancellationToken.None);
                check(server.RequestedOffset==65536&&await ModelLibrary.Verify(destination,data.Length,hash,CancellationToken.None),"Server ignoring Range safely restarts download");
            }
            File.Delete(destination);
            using(Server server=new Server(data)) {
                bool rejected=false;try{await ModelLibrary.Download(http,server.Url,destination,data.Length,new string('0',64),null,CancellationToken.None);}catch(IOException){rejected=true;}
                check(rejected&&!File.Exists(destination)&&!File.Exists(destination+".partial"),"Corrupt download never becomes an installed model");
            }
            using(Server server=new Server(data,true,true))using(CancellationTokenSource cancel=new CancellationTokenSource()) {
                cancel.CancelAfter(180);bool cancelled=false;
                try{await ModelLibrary.Download(http,server.Url,destination,data.Length,hash,null,cancel.Token);}catch(OperationCanceledException){cancelled=true;}
                check(cancelled&&File.Exists(destination+".partial")&&new FileInfo(destination+".partial").Length>0&&!File.Exists(destination),"Cancelled download keeps only resumable partial file");
            }
            using(Server server=new Server(data)) {
                await ModelLibrary.Download(http,server.Url,destination,data.Length,hash,null,CancellationToken.None);
                check(server.RequestedOffset>0&&await ModelLibrary.Verify(destination,data.Length,hash,CancellationToken.None),"Cancelled download resumes to verified model");
            }
        }
        File.Delete(destination);
    }
    public static void Render(string root) {
        ComputerSpecs specs=ComputerSpecs.Detect();
        using(SettingsWindow window=new SettingsWindow(new Settings(),new[]{"Ctrl + Shift + Space"})) {
            window.Show();using(Bitmap image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"settings-preview.png"));}window.Close();
        }
        using(ModelWindow window=new ModelWindow(root,new Settings(),specs)) {
            window.Show();using(Bitmap image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"models-preview.png"));}window.Close();
        }
    }
}
