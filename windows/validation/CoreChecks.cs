using System;
using System.IO;
using System.Threading.Tasks;
using LocalFlow;

public static class CoreChecks {
    public static void Main(){string root=Path.Combine(Path.GetTempPath(),"OpenMicAI-core-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
        using(Models models=new Models(root))PolishRetryChecks.Run(models,(ok,name)=>{Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}).GetAwaiter().GetResult();
        Settings value=new Settings();if(value.PolishReworded||value.PolishFormat!="Auto")throw new Exception("My words and Auto must be the defaults");value.PolishReworded=true;value.PolishFormat="Checklist";StyleOptions.Apply(value,"Polish");value.Save(root);Settings saved=Settings.Load(root);if(!saved.PolishReworded||!StyleRouting.Resolve(saved,"fixture").PolishReworded||StyleRouting.Resolve(saved,"fixture").PolishFormat!="Checklist")throw new Exception("Wording and format preferences must persist and reach routing");
        Console.WriteLine("PASS Polish wording preference persists and reaches routing");
        MediaChecks.Run(root).GetAwaiter().GetResult();
    }catch(Exception error){Console.WriteLine(error);Environment.ExitCode=1;}finally{foreach(string file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}}
}
