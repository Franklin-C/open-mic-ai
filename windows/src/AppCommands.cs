using System;
using System.Collections.Generic;
using System.Threading;

namespace LocalFlow {
// Fixed commands and per-user kernel events keep shell shortcuts out of inference input.
public sealed class AppCommandChannel : IDisposable {
    public static readonly string[] Commands={"dictate","copy","open","bar","settings","quit"};
    const string Prefix="Local\\OpenMicAI-Command-";
    readonly List<EventWaitHandle> events=new List<EventWaitHandle>();
    readonly List<RegisteredWaitHandle> waits=new List<RegisteredWaitHandle>();
    bool disposed;
    public AppCommandChannel(SynchronizationContext ui,Action<string> receive,string prefix=Prefix){foreach(string command in Commands){string action=command;EventWaitHandle signal=new EventWaitHandle(false,EventResetMode.AutoReset,prefix+action);events.Add(signal);waits.Add(ThreadPool.RegisterWaitForSingleObject(signal,(s,t)=>{if(!disposed)try{ui.Post(_=>{if(!disposed)receive(action);},null);}catch(System.ComponentModel.InvalidAsynchronousStateException){}},null,Timeout.Infinite,false));}}
    public static string Parse(string[] args){int index=Array.IndexOf(args,"--taskbar-action");return index>=0&&index+1<args.Length&&Array.IndexOf(Commands,args[index+1])>=0?args[index+1]:null;}
    public static bool Send(string command,string prefix=Prefix){if(Array.IndexOf(Commands,command)<0)return false;try{using(EventWaitHandle signal=EventWaitHandle.OpenExisting(prefix+command))return signal.Set();}catch(WaitHandleCannotBeOpenedException){return false;}}
    public void Dispose(){disposed=true;foreach(RegisteredWaitHandle wait in waits)wait.Unregister(null);foreach(EventWaitHandle signal in events)signal.Dispose();}
}
}
