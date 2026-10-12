using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LocalFlow {
public sealed class UsageDay {public string Date;public long Words,TimedWords,ChangedWords,Sessions;public double Seconds;}
public sealed class UsageApp {public string Name;public long Words,Sessions;}
public sealed class UsageData {public long Words,TimedWords,ChangedWords,Sessions;public double Seconds;public List<UsageDay> Days=new List<UsageDay>();public List<UsageApp> Apps=new List<UsageApp>();}
public sealed class UsageStore {
    readonly LocalJson<UsageData> file;public UsageData Data;public event Action Changed;
    public event Action<string> Failed {add{file.Failed+=value;}remove{file.Failed-=value;}}
    public UsageStore(string root){file=new LocalJson<UsageData>(root,"usage.json");Data=file.Read();if(Data.Days==null)Data.Days=new List<UsageDay>();if(Data.Apps==null)Data.Apps=new List<UsageApp>();}
    static string[] Tokens(string text){return Regex.Matches(text??"",@"[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)?").Cast<Match>().Select(m=>m.Value.ToLowerInvariant()).ToArray();}
    public static int WordCount(string text){return Tokens(text).Length;}
    public static int ChangedWords(string original,string result){if(String.IsNullOrEmpty(result))return 0;Dictionary<string,int> counts=new Dictionary<string,int>();foreach(string word in Tokens(original)){if(!counts.ContainsKey(word))counts[word]=0;counts[word]++;}foreach(string word in Tokens(result)){if(!counts.ContainsKey(word))counts[word]=0;counts[word]--;}return Math.Max(counts.Values.Where(n=>n>0).Sum(),-counts.Values.Where(n=>n<0).Sum());}
    public void Record(string original,string result,double seconds,string application,DateTime createdUtc) {
        long words=WordCount(original),changed=ChangedWords(original,result);seconds=Double.IsNaN(seconds)||Double.IsInfinity(seconds)?0:Math.Max(0,seconds);long timed=seconds>0?words:0;string date=createdUtc.ToLocalTime().ToString("yyyy-MM-dd");
        Data.Words+=words;Data.ChangedWords+=changed;Data.Sessions++;Data.Seconds+=seconds;Data.TimedWords+=timed;
        UsageDay day=Data.Days.FirstOrDefault(d=>d.Date==date);if(day==null){day=new UsageDay {Date=date};Data.Days.Add(day);}day.Words+=words;day.ChangedWords+=changed;day.Sessions++;day.Seconds+=seconds;day.TimedWords+=timed;
        string name=String.IsNullOrWhiteSpace(application)?"Other":application;UsageApp app=Data.Apps.FirstOrDefault(a=>String.Equals(a.Name,name,StringComparison.OrdinalIgnoreCase));if(app==null){app=new UsageApp {Name=name};Data.Apps.Add(app);}app.Words+=words;app.Sessions++;
        file.Save(Data);Action change=Changed;if(change!=null)change();
    }
    public double WordsPerMinute {get{return Data.Seconds>0?Data.TimedWords*60/Data.Seconds:0;}}
    public int Streak(DateTime today){HashSet<string> dates=new HashSet<string>(Data.Days.Where(d=>d.Sessions>0).Select(d=>d.Date));DateTime current=today.Date;if(!dates.Contains(current.ToString("yyyy-MM-dd")))current=current.AddDays(-1);int days=0;while(dates.Contains(current.ToString("yyyy-MM-dd"))){days++;current=current.AddDays(-1);}return days;}
    public void Clear(){Data=new UsageData();file.Save(Data);Action change=Changed;if(change!=null)change();}
    public Task FlushAsync(){return file.FlushAsync();}
}
public sealed class AppStyle {public string Application,Style="Keep my wording",Custom="",Cleanup="Light";public bool PolishReworded;public string PolishFormat="Auto";}
public static class StyleRouting {
    public static AppStyle Resolve(Settings value,string application){string preset=StyleOptions.Selected(value);return new AppStyle {Application=application,Style=preset=="Raw transcript"?"Original transcript":preset=="Polish"?"Smart formatting":preset=="Custom"?"Custom":"Keep my wording",Custom=value.Custom,Cleanup=preset=="Raw transcript"?"None":preset=="Clean up"?"Light":"Medium",PolishReworded=value.PolishReworded,PolishFormat=PolishLayout.Normalize(value.PolishFormat)};}
}
}
