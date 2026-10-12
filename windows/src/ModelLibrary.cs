using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LocalFlow {
public sealed class ComputerSpecs {
    public long RamBytes, GpuBytes;
    public int CpuThreads=Environment.ProcessorCount;
    public string Gpu="No dedicated GPU detected";
    [StructLayout(LayoutKind.Sequential)] struct Memory {
        public uint Length, Load;
        public ulong Total, Available, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, Extended;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct AdapterDescription {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Name;
        public uint Vendor, Device, Subsystem, Revision;
        public UIntPtr DedicatedVideo, DedicatedSystem, Shared;
        public uint LuidLow; public int LuidHigh; public uint Flags;
    }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref Memory memory);
    [DllImport("dxgi.dll")] static extern int CreateDXGIFactory1(ref Guid id,out IntPtr factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Enumerate(IntPtr self,uint index,out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Describe(IntPtr self,out AdapterDescription description);
    static Delegate Method(IntPtr instance,int slot,Type kind) {
        return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),slot*IntPtr.Size),kind);
    }
    public static ComputerSpecs Detect() {
        ComputerSpecs result=new ComputerSpecs();
        Memory memory=new Memory {Length=(uint)Marshal.SizeOf(typeof(Memory))};
        if(GlobalMemoryStatusEx(ref memory))result.RamBytes=(long)memory.Total;
        IntPtr factory=IntPtr.Zero;
        try {
            Guid id=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
            if(CreateDXGIFactory1(ref id,out factory)<0)return result;
            Enumerate enumerate=(Enumerate)Method(factory,12,typeof(Enumerate));
            for(uint i=0;i<16;i++) {
                IntPtr adapter;
                if(enumerate(factory,i,out adapter)<0)break;
                try {
                    AdapterDescription description;
                    if(((Describe)Method(adapter,10,typeof(Describe)))(adapter,out description)>=0 && (description.Flags&2)==0) {
                        long bytes=(long)description.DedicatedVideo.ToUInt64();
                        if(bytes>result.GpuBytes){result.GpuBytes=bytes;result.Gpu=description.Name.Trim();}
                    }
                } finally {Marshal.Release(adapter);}
            }
        } catch(DllNotFoundException){} catch(EntryPointNotFoundException){} catch(COMException){}
        finally {if(factory!=IntPtr.Zero)Marshal.Release(factory);}
        return result;
    }
    public string Description {get {return (RamBytes/1073741824.0).ToString("0")+" GB RAM / "+CpuThreads+" CPU threads\n"+Gpu+(GpuBytes>0?" / "+(GpuBytes/1073741824.0).ToString("0.#")+" GB graphics memory":"");}}
}

public sealed class ModelFile {
    public string Name, Repo, Revision, Sha256, Title, Detail;
    public long Bytes;
    public string Url {get{return "https://huggingface.co/"+Repo+"/resolve/"+Revision+"/"+Name;}}
    public override string ToString(){return (Title??Name)+" · "+(Bytes/1000000000.0).ToString("0.00")+" GB";}
}
public sealed class ModelPair {
    public string Id, Title, Detail;
    public ModelFile Speech, Text;
    public long Bytes {get{return Speech.Bytes+Text.Bytes;}}
    public override string ToString(){return Title+" / "+(Bytes/1000000000.0).ToString("0.00")+" GB";}
}
public sealed class DownloadProgress {
    public string Message;
    public long Done, Total;
}

public static class ModelLibrary {
    static readonly ModelFile VoiceQ8=new ModelFile {Name="tdt-0.6b-v2-q8_0.gguf",Repo="mudler/parakeet-cpp-gguf",Revision="741158ae71e64ef5c89385862c18f777d07a97a1",Sha256="2027e2e1a4dc60ccdd8558f93b15e7c0db4ef8895b4e82e889f3a6275d8119c6",Bytes=903835936};
    static readonly ModelFile VoiceQ4=new ModelFile {Name="tdt-0.6b-v2-q4_k.gguf",Repo=VoiceQ8.Repo,Revision=VoiceQ8.Revision,Sha256="417e8a8e994ec4bcce7010ab1e205f8b88291a4535ddd3152d24d0e19517bfc8",Bytes=638373152};
    static readonly ModelFile TextQ3=new ModelFile {Name="Qwen3-4B-Instruct-2507-Q3_K_S.gguf",Repo="unsloth/Qwen3-4B-Instruct-2507-GGUF",Revision="a06e946bb6b655725eafa393f4a9745d460374c9",Sha256="0ce20058cc0ed6b6c9213bb383589327e458c12ffce0842fc96867042d669c75",Bytes=1886997600};
    static readonly ModelFile TextQ4=new ModelFile {Name="Qwen3-4B-Instruct-2507-Q4_K_M.gguf",Repo=TextQ3.Repo,Revision=TextQ3.Revision,Sha256="3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597",Bytes=2497281120};
    static readonly ModelFile TextSmall=new ModelFile {Name="Qwen3.5-2B-Q4_K_M.gguf",Repo="unsloth/Qwen3.5-2B-GGUF",Revision="f6d5376be1edb4d416d56da11e5397a961aca8ae",Sha256="aaf42c8b7c3cab2bf3d69c355048d4a0ee9973d48f16c731c0520ee914699223",Bytes=1280835840};
    static readonly ModelFile TextQ5=new ModelFile {Name="Qwen3-4B-Instruct-2507-Q5_K_M.gguf",Repo=TextQ3.Repo,Revision=TextQ3.Revision,Sha256="5bde5e9d883622acb02bf77fe7dcbc56a8b9a9ad4be78a72ca23a532658b4ecb",Bytes=2889514080};
    static readonly ModelFile TextTiny=new ModelFile {Name="Qwen3-1.7B-Q4_K_M.gguf",Repo="unsloth/Qwen3-1.7B-GGUF",Revision="d7f544eead698dbd1f15126ef60b45a1e1933222",Sha256="b139949c5bd74937ad8ed8c8cf3d9ffb1e99c866c823204dc42c0d91fa181897",Bytes=1107409472};
    static readonly ModelFile TextLarge=new ModelFile {Name="Qwen3-8B-Q4_K_M.gguf",Repo="unsloth/Qwen3-8B-GGUF",Revision="a6adef130ffb23ddaf1a62fec9dced968c9bc482",Sha256="120307ba529eb2439d6c430d94104dabd578497bc7bfe7e322b5d9933b449bd4",Bytes=5027784512};
    public static readonly ModelFile[] SpeechModels={VoiceQ8,VoiceQ4};
    public static readonly ModelFile[] TextModels={TextTiny,TextSmall,TextQ3,TextQ4,TextQ5,TextLarge};
    static ModelLibrary(){
        VoiceQ8.Title="Parakeet TDT 0.6B v2 · Q8";VoiceQ8.Detail="English · best tested speech accuracy";
        VoiceQ4.Title="Parakeet TDT 0.6B v2 · Q4";VoiceQ4.Detail="English · smaller download";
        TextTiny.Title="Qwen3 1.7B · Q4";TextTiny.Detail="Smallest download";
        TextSmall.Title="Qwen3.5 2B · Q4";TextSmall.Detail="Compact download";
        TextQ3.Title="Qwen3 4B Instruct · Q3";TextQ3.Detail="Balanced speed and quality";
        TextQ4.Title="Qwen3 4B Instruct · Q4";TextQ4.Detail="Higher precision";
        TextQ5.Title="Qwen3 4B Instruct · Q5";TextQ5.Detail="Highest 4B precision";
        TextLarge.Title="Qwen3 8B · Q4";TextLarge.Detail="Largest download";
    }
    public static readonly ModelPair[] Pairs={
        new ModelPair {Id="balanced",Title="Balanced",Speech=VoiceQ8,Text=TextQ3,Detail="Best tested balance of accuracy, speed and size. About 2.79 GB on disk. Recommended with 6+ GB dedicated graphics memory and 12+ GB RAM."},
        new ModelPair {Id="compact",Title="Compact",Speech=VoiceQ4,Text=TextSmall,Detail="Smaller weights for modest computers: about 1.92 GB. Less reliable for complex corrections and formatting. CPU processing can be slower; this is a memory recommendation, not a speed guarantee."},
        new ModelPair {Id="quality",Title="Higher precision",Speech=VoiceQ8,Text=TextQ4,Detail="The same 4B rewrite model with less quantization: about 3.40 GB. Uses more memory. No measured accuracy advantage over Balanced; choose only if you want to compare."}
    };
    // Leave room for memory reserved by firmware and the Windows display driver.
    public static ModelPair Recommend(ComputerSpecs specs){return specs.RamBytes>=11L*1073741824 && specs.GpuBytes>=5500L*1024*1024?Pairs[0]:Pairs[1];}
    public static ModelPair Find(string speech,string text){ModelPair preset=Pairs.FirstOrDefault(p=>p.Speech.Name==speech&&p.Text.Name==text);if(preset!=null)return preset;ModelFile voice=SpeechModels.FirstOrDefault(f=>f.Name==speech),language=TextModels.FirstOrDefault(f=>f.Name==text);return voice==null||language==null?null:new ModelPair {Id="custom",Title="Your models",Speech=voice,Text=language};}
    public static bool Installed(string root,ModelFile file){string path=Path.Combine(root,"models",file.Name);return File.Exists(path)&&new FileInfo(path).Length==file.Bytes;}
    public static bool Installed(string root,ModelPair pair){return Installed(root,pair.Speech)&&Installed(root,pair.Text);}
    public static async Task Ensure(string root,ModelPair pair,IProgress<DownloadProgress> progress,CancellationToken cancel) {
        ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072; // TLS 1.2 on older .NET Framework defaults.
        Directory.CreateDirectory(Path.Combine(root,"models"));
        using(HttpClient http=new HttpClient(new HttpClientHandler {UseProxy=true})) {
            http.Timeout=Timeout.InfiniteTimeSpan;
            http.DefaultRequestHeaders.UserAgent.ParseAdd("OpenMicAI/3.0");
            foreach(ModelFile model in new[]{pair.Speech,pair.Text})
                await Download(http,model.Url,Path.Combine(root,"models",model.Name),model.Bytes,model.Sha256,progress,cancel);
        }
    }
    public static async Task<bool> Verify(string path,long bytes,string hash,CancellationToken cancel) {
        if(!File.Exists(path)||new FileInfo(path).Length!=bytes)return false;
        return await Task.Run(()=>{
            using(SHA256 sha=SHA256.Create())using(FileStream input=File.OpenRead(path)) {
                byte[] buffer=new byte[1024*1024];int count;
                while((count=input.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,count,null,0);}
                sha.TransformFinalBlock(new byte[0],0,0);
                return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant()==hash;
            }
        },cancel);
    }
    // Public for an isolated HTTP fixture test. Production calls only use the pinned HTTPS catalog.
    public static async Task Download(HttpClient http,string url,string destination,long size,string hash,IProgress<DownloadProgress> progress,CancellationToken cancel) {
        string name=Path.GetFileName(destination),partial=destination+".partial";
        Action<string,long> report=(message,done)=>{if(progress!=null)progress.Report(new DownloadProgress {Message=message,Done=done,Total=size});};
        report("Checking "+name,0);
        if(await Verify(destination,size,hash,cancel)){report(name+" verified",size);return;}
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if(File.Exists(partial)&&new FileInfo(partial).Length>size)File.Delete(partial);
        long offset=File.Exists(partial)?new FileInfo(partial).Length:0;
        if(offset<size) {
            DriveInfo drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destination)));
            if(drive.AvailableFreeSpace<size-offset+64*1024*1024)throw new IOException("There is not enough free disk space for "+name+".");
            using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,url)) {
                if(offset>0)request.Headers.Range=new RangeHeaderValue(offset,null);
                using(HttpResponseMessage response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel)) {
                    response.EnsureSuccessStatusCode();
                    if(response.StatusCode==HttpStatusCode.PartialContent) {
                        ContentRangeHeaderValue range=response.Content.Headers.ContentRange;
                        if(range==null||range.From!=offset||range.Length!=size)throw new IOException("The server returned an unexpected download range.");
                    } else offset=0; // Servers may ignore Range; start over instead of appending duplicates.
                    long? receivedSize=response.Content.Headers.ContentLength;
                    if(receivedSize.HasValue&&receivedSize.Value!=size-offset)throw new IOException("The model size does not match the pinned catalog.");
                    using(Stream input=await response.Content.ReadAsStreamAsync())
                    using(FileStream output=new FileStream(partial,offset==0?FileMode.Create:FileMode.Append,FileAccess.Write,FileShare.Read,262144,true)) {
                        byte[] buffer=new byte[262144];int read;long done=offset;Stopwatch clock=Stopwatch.StartNew();
                        while((read=await input.ReadAsync(buffer,0,buffer.Length,cancel))>0) {
                            if(done+read>size)throw new IOException("The downloaded model exceeds its expected size.");
                            await output.WriteAsync(buffer,0,read,cancel);done+=read;
                            if(clock.ElapsedMilliseconds>=100){report("Downloading "+name+" / "+(done/1000000.0).ToString("0")+" of "+(size/1000000.0).ToString("0")+" MB",done);clock.Restart();}
                        }
                    }
                }
            }
        }
        report("Verifying SHA-256 / "+name,size);
        if(!await Verify(partial,size,hash,cancel)){File.Delete(partial);throw new IOException("Model verification failed. Retry to download a fresh copy.");}
        cancel.ThrowIfCancellationRequested();
        if(File.Exists(destination))File.Delete(destination);
        File.Move(partial,destination);report(name+" ready",size);
    }
}

public sealed class ModelWindow : Form {
    public ModelPair Selected;public Func<ModelPair,Task<bool>> ApplyModels;public Func<bool> CanApply;
    readonly string root;readonly ComboBox speechChoice,textChoice;readonly Label state,speechName,textName,loaded;readonly ProgressBar bar;readonly Button install,cancel;CancellationTokenSource download;bool busy,disposed;
    ModelPair Choice {get{return ModelLibrary.Find(((ModelFile)speechChoice.SelectedItem).Name,((ModelFile)textChoice.SelectedItem).Name);}}
    public ModelWindow(string directory,Settings settings,ComputerSpecs specs) {
        root=directory;Text=Brand.Name+" models";ClientSize=new Size(760,620);Font=new Font("Segoe UI",10);BackColor=Color.White;
        FlowLayoutPanel layout=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};Controls.Add(layout);
        layout.Controls.Add(new Label {Text="Local models",Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,0,0,10)});
        ModelPair recommended=ModelLibrary.Recommend(specs),current=ModelLibrary.Find(settings.Speech,settings.Text)??recommended;
        loaded=new Label {AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(0,0,0,12)};layout.Controls.Add(loaded);
        speechChoice=Selector(ModelLibrary.SpeechModels,current.Speech,recommended.Speech,"Speech-to-text");textChoice=Selector(ModelLibrary.TextModels,current.Text,recommended.Text,"Language model");
        speechName=Description();textName=Description();
        foreach(Control control in new Control[]{Heading("Speech-to-text"),speechChoice,speechName,Heading("Language model"),textChoice,textName})layout.Controls.Add(control);
        state=new Label {Size=new Size(580,42),Margin=new Padding(0,6,0,8)};bar=new ProgressBar {Size=new Size(580,8),Margin=new Padding(0,0,0,14)};layout.Controls.AddRange(new Control[]{state,bar});
        FlowLayoutPanel actions=new FlowLayoutPanel {Width=620,Height=43,WrapContents=false};install=Theme.Button("Load models",true);install.UseMnemonic=false;cancel=Theme.Button("Cancel download");cancel.Visible=false;
        cancel.Click+=(s,e)=>{if(download!=null)download.Cancel();};install.Click+=async(s,e)=>await Install();actions.Controls.AddRange(new Control[]{install,cancel});layout.Controls.Add(actions);
        layout.Controls.Add(new Label {Text="Local processing · Models stay ready",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(0,12,0,0)});
        speechChoice.SelectedIndexChanged+=(s,e)=>RefreshChoice();textChoice.SelectedIndexChanged+=(s,e)=>RefreshChoice();RefreshChoice();UpdateStatus("",settings);Theme.Apply(this);
    }
    static Label Heading(string title){return new Label {Text=title,AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold),Margin=new Padding(0,7,0,8)};}
    static Label Description(){return new Label {Size=new Size(580,43),ForeColor=Theme.Muted,Margin=new Padding(0,0,0,8)};}
    static ComboBox Selector(ModelFile[] files,ModelFile current,ModelFile recommended,string role){ComboBox box=new ComboBox {Width=580,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName=role,Name=role,Margin=new Padding(0,0,0,8),FormattingEnabled=true};box.Items.AddRange(files);box.Format+=(s,e)=>{ModelFile file=(ModelFile)e.ListItem;e.Value=file.ToString()+(file==recommended?" · Recommended":"");};box.SelectedItem=current;return box;}
    public void UpdateStatus(string text,Settings settings){ModelPair pair=ModelLibrary.Find(settings.Speech,settings.Text);bool ready=text.StartsWith("Ready");loaded.Text=pair!=null&&ModelLibrary.Installed(root,pair)?(ready?"Models ready":"Models downloaded"):"Choose your models";if(!busy&&pair!=null&&pair.Speech==Choice.Speech&&pair.Text==Choice.Text&&ready)state.Text="Ready";}
    void RefreshChoice(){ModelPair pair=Choice;speechName.Text=pair.Speech.Detail;textName.Text=pair.Text.Detail;long needed=new[]{pair.Speech,pair.Text}.Where(f=>!ModelLibrary.Installed(root,f)).Sum(f=>f.Bytes);state.Text=needed==0?"Ready to load":(needed/1000000000.0).ToString("0.00")+" GB download";install.Text=needed==0?"Load models":"Download & load models";}
    async Task Install(){if(busy)return;if(CanApply!=null&&!CanApply()){state.Text="Finish recording or model loading first.";return;}busy=true;speechChoice.Enabled=textChoice.Enabled=install.Enabled=false;cancel.Visible=true;CancellationTokenSource pending=new CancellationTokenSource();download=pending;ModelPair pair=Choice;bool transferring=true;
        try{Progress<DownloadProgress> progress=new Progress<DownloadProgress>(p=>{if(disposed||!transferring)return;state.Text=p.Message;bar.Value=p.Total>0?(int)Math.Min(100,p.Done*100/p.Total):0;});await ModelLibrary.Ensure(root,pair,progress,pending.Token);pending.Token.ThrowIfCancellationRequested();if(disposed)return;if(CanApply!=null&&!CanApply()){state.Text="Downloaded. Finish recording, then click Load models.";return;}transferring=false;cancel.Visible=false;Selected=pair;state.Text="Loading both models…";bar.Style=ProgressBarStyle.Marquee;if(ApplyModels==null)throw new InvalidOperationException("Reopen Settings to load your models.");bool ready=await ApplyModels(pair);if(disposed)return;state.Text=ready?"Ready · both models stay loaded":"Could not load models. Click Load models to retry.";}
        catch(OperationCanceledException){if(!disposed)state.Text="Download paused. Click Download & load models to resume.";}catch(Exception error){if(!disposed)state.Text="Could not prepare models: "+error.Message;}finally{transferring=false;busy=false;if(!disposed){speechChoice.Enabled=textChoice.Enabled=install.Enabled=true;cancel.Visible=false;bar.Style=ProgressBarStyle.Continuous;install.Text=ModelLibrary.Installed(root,Choice)?"Load models":"Download & load models";}if(download==pending)download=null;pending.Dispose();}}
    protected override void Dispose(bool disposing){if(disposing){disposed=true;if(download!=null)download.Cancel();}base.Dispose(disposing);}
}

public static class PortableRuntime {
    public static string Prepare() {
        string root=AppDomain.CurrentDomain.BaseDirectory;
        using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LocalFlow.Runtime.zip")) {
            if(stream==null)return root;
            string legacy=Path.Combine(root,"LocalFlow-data");string branded=Path.Combine(root,"OpenMicAI-data");root=Directory.Exists(branded)||!Directory.Exists(legacy)?branded:legacy;Directory.CreateDirectory(root);
            using(ZipArchive zip=new ZipArchive(stream,ZipArchiveMode.Read)) {
                string prefix=Path.GetFullPath(root)+Path.DirectorySeparatorChar;
                foreach(ZipArchiveEntry entry in zip.Entries) {
                    string destination=Path.GetFullPath(Path.Combine(root,entry.FullName));
                    if(!destination.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid bundled runtime path.");
                    if(entry.FullName.EndsWith("/"))continue;
                    if(File.Exists(destination)&&new FileInfo(destination).Length==entry.Length)continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    string pending=destination+".extracting";
                    using(Stream input=entry.Open())using(FileStream output=File.Create(pending))input.CopyTo(output);
                    if(File.Exists(destination))File.Delete(destination);File.Move(pending,destination);
                }
            }
        }
        return root;
    }
}
}
