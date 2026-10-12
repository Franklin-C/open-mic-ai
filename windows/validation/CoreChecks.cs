using System;
using System.IO;
using System.Threading.Tasks;
using LocalFlow;

public static class CoreChecks {
    public static void Main(){string root=Path.Combine(Path.GetTempPath(),"OpenMicAI-core-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
        using(Models models=new Models(root))PolishRetryChecks.Run(models,(ok,name)=>{Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}).GetAwaiter().GetResult();
        Settings value=new Settings();if(value.PolishReworded)throw new Exception("My words must be the default");value.PolishReworded=true;StyleOptions.Apply(value,"Polish");value.Save(root);
        string path=Path.Combine(root,"preferences.json"),json=File.ReadAllText(path);File.WriteAllText(path,json.Insert(1,"\"PolishFormat\":\"Checklist\","));
        Settings saved=Settings.Load(root);if(!saved.PolishReworded||!StyleRouting.Resolve(saved,"fixture").PolishReworded)throw new Exception("Wording preferences must survive legacy format migration");saved.Save(root);if(File.ReadAllText(path).Contains("PolishFormat"))throw new Exception("Legacy manual formats must be discarded so layout is always automatic");
        Console.WriteLine("PASS Polish wording preference persists and reaches routing");
        if(SpokenAction.Parse("Could you translate this into Spanish, please?","French").Language!="Spanish"||SpokenAction.Parse("Please summarize this.","French").Action!="Summarize"||SpokenAction.Parse("Rewrite this as an email.","French").Action!="Rewrite"||SpokenAction.Parse("The invoice will arrive Friday.","French").Action!="Dictate")throw new Exception("Selected text voice commands must be distinguished from replacement dictation");Console.WriteLine("PASS Spoken cues distinguish rewrite, translation, summary and replacement dictation");
        MediaChecks.Run(root).GetAwaiter().GetResult();
    }catch(Exception error){Console.WriteLine(error);Environment.ExitCode=1;}finally{foreach(string file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}}
}
