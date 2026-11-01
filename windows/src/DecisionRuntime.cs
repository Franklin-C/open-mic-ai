using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LocalFlow {
public static class DecisionRuntime {
    public const string D1="d1-omni-600m",Simple="simple-jev-qwen-0.8b";
    public static bool Dedicated(string name){return name==D1||name==Simple;}
    static Dictionary<string,object> Bundle(string name){
        if(!Dedicated(name))throw new InvalidOperationException("Choose a supported decision backend.");
        using(Stream input=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenMicAI.DecisionManifest")){
            if(input==null)throw new InvalidOperationException("Rebuild Open Mic AI to include decision backends.");
            using(StreamReader reader=new StreamReader(input)){var manifest=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());return (Dictionary<string,object>)((Dictionary<string,object>)manifest["backends"])[name];}
        }
    }
    public static Dictionary<string,string> Configuration(string root,string name){
        var bundle=Bundle(name);string path=Path.Combine(root,"runtime","decisions",name+".json");
        if(!File.Exists(path))throw new InvalidOperationException("Choose Download & load models to install the selected decision backend.");
        var config=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(path));
        if(config==null||!config.ContainsKey("adapter_version")||config["adapter_version"]!="1"||!config.ContainsKey("revision")||config["revision"]!=Convert.ToString(bundle["revision"])||!config.ContainsKey("python")||!File.Exists(config["python"])||!config.ContainsKey("model"))throw new InvalidOperationException("Decision setup needs an update. Choose Download & load models.");
        foreach(Dictionary<string,object> file in (IEnumerable)bundle["files"]){string local=Path.Combine(config["model"],Convert.ToString(file["name"]).Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(local)||new FileInfo(local).Length!=Convert.ToInt64(file["bytes"]))throw new InvalidOperationException("Decision model files are incomplete. Choose Download & load models.");}
        return config;
    }
    public static bool Installed(string root,string name){try{Configuration(root,name);return true;}catch{return false;}}
    static void Extract(string resource,string target){using(Stream input=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)){if(input==null)throw new InvalidOperationException("This build does not include the decision runtime.");using(FileStream output=File.Create(target))input.CopyTo(output);}}
    public static string PrepareServer(string root){string target=Path.Combine(root,"runtime","decisions","openmic-server.py");Directory.CreateDirectory(Path.GetDirectoryName(target));Extract("OpenMicAI.DecisionServer",target);return target;}
    static bool SetupRunning(string folder){string path=Path.Combine(folder,"setup.lock");if(!File.Exists(path))return false;try{using(FileStream input=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))return false;}catch(IOException){return true;}}
    public static async Task Ensure(string root,string name,IProgress<DownloadProgress> progress,CancellationToken cancel){
        if(Installed(root,name))return;string folder=Path.Combine(root,"runtime","decisions");Directory.CreateDirectory(folder);
        Action<string> report=message=>{if(progress!=null)progress.Report(new DownloadProgress {Message=message});};
        while(SetupRunning(folder)){report("A decision model is being installed. Waiting for it to finish…");await Task.Delay(500,cancel);}
        if(Installed(root,name))return;cancel.ThrowIfCancellationRequested();
        string installer=Path.Combine(folder,"installer");Directory.CreateDirectory(installer);string script=Path.Combine(installer,"setup.py");Extract("OpenMicAI.DecisionSetup",script);Extract("OpenMicAI.DecisionManifest",Path.Combine(installer,"model-manifest.json"));
        report("Setting up "+name+" · model files + isolated CPU runtime");
        using(ChildJob job=new ChildJob())using(Process process=new Process()){
            process.StartInfo=new ProcessStartInfo(EdgeRockRuntime.Python(),"-u \""+script+"\" --root \""+Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+"\" --backend "+name) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            process.OutputDataReceived+=(s,e)=>{if(!String.IsNullOrWhiteSpace(e.Data))report(e.Data);};process.ErrorDataReceived+=(s,e)=>{};
            process.Start();job.Add(process);process.BeginOutputReadLine();process.BeginErrorReadLine();
            using(cancel.Register(()=>{try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}))await Task.Run(()=>process.WaitForExit());
            cancel.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new InvalidOperationException("Decision setup did not finish. Check the connection and free space, then retry; transfers resume.");
        }
        Configuration(root,name);report("Decision model installed");
    }
}
}
