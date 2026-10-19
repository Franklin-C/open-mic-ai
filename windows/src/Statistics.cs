using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LocalFlow {
// Statistics operate on aggregates, never on the capped transcript history.
public sealed class StatsSnapshot {
    public long Words,Sessions,TimedWords,InputTokens,OutputTokens,Longest;
    public double Seconds,Fastest,PreviousWords;
    public int ActiveDays,CurrentStreak,LongestStreak;
    public DateTime Start,End;
    public UsageDay[] Days;
    public UsageApp[] Apps,Modes;
    public long[] Hours=new long[24],Weekdays=new long[7];
    public double Wpm {get{return Seconds>0?TimedWords*60/Seconds:0;}}
    public double SavedSeconds(int typingWpm){return Seconds>0?Math.Max(0,TimedWords*60d/Math.Max(10,typingWpm)-Seconds):0;}
    public long TotalTokens {get{return InputTokens+OutputTokens;}}
    public static bool Date(string text,out DateTime date){return DateTime.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date);}
    static UsageApp[] Merge(IEnumerable<UsageApp> values){return values.Where(a=>a!=null&&!String.IsNullOrEmpty(a.Name)).GroupBy(a=>a.Name,StringComparer.OrdinalIgnoreCase).Select(g=>new UsageApp {Name=g.Key,Words=g.Sum(a=>a.Words),Sessions=g.Sum(a=>a.Sessions)}).OrderByDescending(a=>a.Words).ThenBy(a=>a.Name).ToArray();}
    public static StatsSnapshot Create(UsageData data,int range,DateTime today){
        StatsSnapshot value=new StatsSnapshot();today=today.Date;value.End=today;
        UsageDay[] valid=(data.Days??new List<UsageDay>()).Where(d=>{DateTime date;return d!=null&&Date(d.Date,out date)&&date<=today;}).ToArray();
        value.Start=range>0?today.AddDays(1-range):valid.Select(d=>System.DateTime.ParseExact(d.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture)).DefaultIfEmpty(today).Min();
        value.Days=valid.Where(d=>String.CompareOrdinal(d.Date,value.Start.ToString("yyyy-MM-dd"))>=0).OrderBy(d=>d.Date).ToArray();
        if(range==0){value.Words=data.Words;value.Sessions=data.Sessions;value.TimedWords=data.TimedWords;value.Seconds=data.Seconds;value.InputTokens=data.InputTokens;value.OutputTokens=data.OutputTokens;value.Apps=Merge(data.Apps??new List<UsageApp>());}
        else {value.Words=value.Days.Sum(d=>d.Words);value.Sessions=value.Days.Sum(d=>d.Sessions);value.TimedWords=value.Days.Sum(d=>d.TimedWords);value.Seconds=value.Days.Sum(d=>d.Seconds);value.InputTokens=value.Days.Sum(d=>d.InputTokens);value.OutputTokens=value.Days.Sum(d=>d.OutputTokens);value.Apps=Merge(value.Days.SelectMany(d=>d.Apps??new List<UsageApp>()));string previous=value.Start.AddDays(-range).ToString("yyyy-MM-dd"),end=value.Start.ToString("yyyy-MM-dd");value.PreviousWords=valid.Where(d=>String.CompareOrdinal(d.Date,previous)>=0&&String.CompareOrdinal(d.Date,end)<0).Sum(d=>d.Words);}
        value.Modes=Merge(value.Days.SelectMany(d=>d.Modes??new List<UsageApp>()));value.ActiveDays=value.Days.Where(d=>d.Sessions>0).Select(d=>d.Date).Distinct().Count();value.Longest=value.Days.Select(d=>d.Longest).DefaultIfEmpty(0).Max();value.Fastest=value.Days.Select(d=>d.Fastest).DefaultIfEmpty(0).Max();
        foreach(UsageDay day in value.Days){DateTime date;Date(day.Date,out date);value.Weekdays[((int)date.DayOfWeek+6)%7]+=day.Words;if(day.Hours!=null)for(int i=0;i<Math.Min(24,day.Hours.Length);i++)value.Hours[i]+=day.Hours[i];}
        // Streaks are lifetime records regardless of the selected chart range.
        HashSet<DateTime> active=new HashSet<DateTime>(valid.Where(d=>d.Sessions>0).Select(d=>System.DateTime.ParseExact(d.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture)));int run=0;DateTime? previousDate=null;
        foreach(DateTime day in active.OrderBy(d=>d)){run=previousDate.HasValue&&day==previousDate.Value.AddDays(1)?run+1:1;value.LongestStreak=Math.Max(value.LongestStreak,run);previousDate=day;}
        DateTime cursor=active.Contains(today)?today:today.AddDays(-1);while(active.Contains(cursor)){value.CurrentStreak++;cursor=cursor.AddDays(-1);}return value;
    }
    public static string Duration(double seconds){if(seconds<60)return seconds>0?"<1 min":"0 min";if(seconds<3600)return (seconds/60).ToString("0")+" min";return (seconds/3600).ToString("0.#")+" hr";}
}
}
