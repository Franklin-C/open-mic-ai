using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LocalFlow {
public static class EdgeRockRuntime {
    public const string ModelName="edgerock-4b";
    public const string Revision="f8c04fa0ddf8fab1aefdd4726535a8c3fa305972";
    public const string AdapterVersion="1";
    public static Dictionary<string,string> Configuration(string root){
        string path=Path.Combine(root,"runtime","edgerock","setup.json");
        if(!File.Exists(path))throw new InvalidOperationException("EdgeRock setup is not finished. Choose Download & load models to install its local runtime and checkpoint.");
        Dictionary<string,string> config=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(path));
        if(config==null||!config.ContainsKey("revision")||config["revision"]!=Revision||!config.ContainsKey("python")||!File.Exists(config["python"])||!config.ContainsKey("model")||!File.Exists(Path.Combine(config["model"],"config.json")))throw new InvalidOperationException("EdgeRock setup is incomplete. Run windows/edgerock/setup.py again.");
        if(!config.ContainsKey("adapter_version")||config["adapter_version"]!=AdapterVersion)throw new InvalidOperationException("EdgeRock runtime needs an update. Choose Download & load models.");
        if(!config.ContainsKey("device")||Array.IndexOf(new[]{"auto","cpu","cuda"},config["device"])<0)throw new InvalidOperationException("Unsupported EdgeRock device.");
        using(Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenMicAI.EdgeRockManifest")){
            if(resource==null)throw new InvalidOperationException("This build does not include the EdgeRock checkpoint manifest.");
            using(StreamReader reader=new StreamReader(resource)){
                Dictionary<string,object> manifest=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
                foreach(Dictionary<string,object> file in (System.Collections.IEnumerable)manifest["files"]){string local=Path.Combine(config["model"],Convert.ToString(file["name"]).Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(local)||new FileInfo(local).Length!=Convert.ToInt64(file["bytes"]))throw new InvalidOperationException("EdgeRock checkpoint files are missing or incomplete. Choose Download & load models again.");}
            }
        }
        return config;
    }
    // Keep this separate from the GGUF catalog: these weights require their own runtime.
    public static bool Installed(string root){try{Configuration(root);return true;}catch{return false;}}
    internal static string Python(){
        foreach(string directory in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)){
            if(directory.IndexOf("WindowsApps",StringComparison.OrdinalIgnoreCase)>=0)continue;
            try{string candidate=Path.Combine(directory.Trim('"'),"python.exe");if(File.Exists(candidate))return candidate;}catch(ArgumentException){}
        }
        foreach(Microsoft.Win32.RegistryKey hive in new[]{Microsoft.Win32.Registry.CurrentUser,Microsoft.Win32.Registry.LocalMachine})using(Microsoft.Win32.RegistryKey python=hive.OpenSubKey(@"Software\Python\PythonCore")){
            if(python==null)continue;
            foreach(string version in python.GetSubKeyNames().OrderByDescending(v=>v))using(Microsoft.Win32.RegistryKey install=python.OpenSubKey(version+@"\InstallPath")){
                if(install==null)continue;string path=Convert.ToString(install.GetValue("ExecutablePath"));if(File.Exists(path))return path;
                path=Path.Combine(Convert.ToString(install.GetValue("")),"python.exe");if(File.Exists(path))return path;
            }
        }
        throw new InvalidOperationException("Install Python 3.10 or newer, then choose Download & load models again.");
    }
    static void Extract(string resourceName,string path){
        using(Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)){
            if(resource==null)throw new InvalidOperationException("Rebuild Open Mic AI to include the EdgeRock installer.");
            using(FileStream output=File.Create(path))resource.CopyTo(output);
        }
    }
    static bool SetupRunning(string root){
        string path=Path.Combine(root,"runtime","edgerock","setup.lock");if(!File.Exists(path))return false;
        try{using(FileStream file=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))return false;}catch(IOException){return true;}
    }
    static string SetupProgress(string root){try{return new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"runtime","edgerock","setup-progress.json")))["message"];}catch{return "EdgeRock setup is already running. Waiting for it to finish…";}}
    public static async Task Ensure(string root,IProgress<DownloadProgress> progress,CancellationToken cancel){
        if(Installed(root))return;cancel.ThrowIfCancellationRequested();
        while(SetupRunning(root)){if(progress!=null)progress.Report(new DownloadProgress {Message=SetupProgress(root)});await Task.Delay(500,cancel);}
        if(Installed(root))return;
        string folder=Path.Combine(root,"runtime","edgerock","installer");Directory.CreateDirectory(folder);
        string script=Path.Combine(folder,"setup.py");Extract("OpenMicAI.EdgeRockSetup",script);Extract("OpenMicAI.EdgeRockManifest",Path.Combine(folder,"model-manifest.json"));
        Action<string> report=message=>{if(progress!=null)progress.Report(new DownloadProgress {Message=message});};
        report("Setting up EdgeRock · 9.87 GB checkpoint + Python runtime");
        using(ChildJob job=new ChildJob())using(Process process=new Process()){
            process.StartInfo=new ProcessStartInfo(Python(),"-u \""+script+"\" --root \""+Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+"\" --device auto") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            process.OutputDataReceived+=(s,e)=>{if(!String.IsNullOrWhiteSpace(e.Data))report(e.Data);};
            process.ErrorDataReceived+=(s,e)=>{};
            process.Start();job.Add(process);process.BeginOutputReadLine();process.BeginErrorReadLine();
            using(cancel.Register(()=>{try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}))await Task.Run(()=>process.WaitForExit());
            cancel.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new InvalidOperationException("EdgeRock setup did not finish. Check your connection and free disk space, then retry; checkpoint downloads resume.");
        }
        Configuration(root);report("EdgeRock runtime and checkpoint installed");
    }
    public static string PrepareServer(string root){
        string path=Path.Combine(root,"runtime","edgerock","openmic-server.py");Directory.CreateDirectory(Path.GetDirectoryName(path));
        using(Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenMicAI.EdgeRockServer")){
            if(resource==null)throw new InvalidOperationException("This build does not include the EdgeRock adapter.");
            using(FileStream output=File.Create(path))resource.CopyTo(output);
        }
        return path;
    }
}
}
