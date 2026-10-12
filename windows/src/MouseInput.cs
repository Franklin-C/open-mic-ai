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
    readonly SynchronizationContext ui;readonly HookProc procedure;IntPtr hook;int button;bool pressed;
    public Func<bool> CanStart;public Action Down,Up;
    public bool Installed {get{return hook!=IntPtr.Zero;}}
    public HoldMouseInput(SynchronizationContext context){ui=context;procedure=Handle;}
    public void Configure(string choice) {
        int selected=choice=="Mouse 4"?1:choice=="Mouse 5"?2:0;if(selected==button&&((selected==0)||Installed))return;
        if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}button=selected;pressed=false;
        if(button!=0){hook=SetWindowsHookEx(14,procedure,GetModuleHandle(null),0);if(hook==IntPtr.Zero){button=0;throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows could not enable hold-to-talk.");}}
    }
    IntPtr Handle(int code,IntPtr message,IntPtr data) {
        int kind=message.ToInt32();
        if(code>=0&&(kind==0x20B||kind==0x20C)&&((MouseInfo)Marshal.PtrToStructure(data,typeof(MouseInfo))).Data>>16==(uint)button) {
            if(kind==0x20B&&(pressed||(CanStart!=null&&CanStart()))) {if(!pressed){pressed=true;ui.Post(s=>{Action down=Down;if(down!=null)down();},null);}return new IntPtr(1);}
            if(kind==0x20C&&pressed){pressed=false;ui.Post(s=>{Action up=Up;if(up!=null)up();},null);return new IntPtr(1);}
        }
        return CallNextHookEx(hook,code,message,data);
    }
    public void Dispose(){if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);hook=IntPtr.Zero;pressed=false;button=0;}
}
}
