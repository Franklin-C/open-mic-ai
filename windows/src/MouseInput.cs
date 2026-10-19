using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace LocalFlow {
public sealed class HoldMouseInput : IDisposable {
    delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)]struct MouseInfo {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,HookProc procedure,IntPtr module,uint thread);
    [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
    readonly SynchronizationContext ui;readonly HookProc procedure;IntPtr hook;int button,capturedButton,generation;bool pressed;
    Action<string> capture;
    public Func<bool> CanStart;public Action Down,Up;
    public bool Installed {get{return hook!=IntPtr.Zero;}}
    public HoldMouseInput(SynchronizationContext context){ui=context;procedure=Handle;}
    void Install(){if(hook!=IntPtr.Zero)return;hook=SetWindowsHookEx(14,procedure,GetModuleHandle(null),0);if(hook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows could not enable mouse shortcuts.");}
    public void BeginCapture(Action<string> selected){Dispose();capture=selected;Install();}
    void Post(Action action){int current=generation;ui.Post(s=>{if(current==generation&&action!=null)action();},null);}
    public void Configure(string choice) {
        int selected=choice=="Mouse 4"?1:choice=="Mouse 5"?2:choice=="Middle mouse"?3:0;if(capture==null&&selected==button&&((selected==0)||Installed))return;
        Dispose();button=selected;if(button!=0)try{Install();}catch{button=0;throw;}
    }
    IntPtr Handle(int code,IntPtr message,IntPtr data) {
        int kind=message.ToInt32();uint nativeData=code>=0&&(kind==0x20B||kind==0x20C)?((MouseInfo)Marshal.PtrToStructure(data,typeof(MouseInfo))).Data:0;
        if(code>=0&&ProcessButton(kind,nativeData))return new IntPtr(1);
        return CallNextHookEx(hook,code,message,data);
    }
    bool ProcessButton(int kind,uint nativeData){
        if(kind!=0x207&&kind!=0x208&&kind!=0x20B&&kind!=0x20C)return false;
        int selected=kind==0x207||kind==0x208?3:(int)(nativeData>>16);bool down=kind==0x207||kind==0x20B;
        if(selected<1||selected>3||((kind==0x20B||kind==0x20C)&&selected==3))return false;
        if(capture!=null){if(down&&capturedButton==0)capturedButton=selected;if(capturedButton==selected){if(!down){Action<string> done=capture;capture=null;capturedButton=0;string name=selected==3?"Middle mouse":selected==1?"Mouse 4":"Mouse 5";Post(()=>done(name));}return true;}}
        if(selected==button){if(down&&(pressed||(CanStart!=null&&CanStart()))){if(!pressed){pressed=true;Post(Down);}return true;}if(!down&&pressed){pressed=false;Post(Up);return true;}}
        return false;
    }
    public void Dispose(){generation++;if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);hook=IntPtr.Zero;pressed=false;button=capturedButton=0;capture=null;}
}
}
