using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Forms;

namespace LocalFlow {
public static class Native {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint thread, ref GUIInfo info);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] public static extern int WindowStyle(IntPtr window,int index);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError=true)] public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window,StringBuilder name,int max);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr window,StringBuilder text,int max);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll",EntryPoint="SendMessageW")] public static extern IntPtr GetSelection(IntPtr window,uint message,out int start,out int end);
    [DllImport("user32.dll",EntryPoint="SendMessageTimeoutW",SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
    [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, Input[] events, int size);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct GUIInfo {
        public uint Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public Rect CaretRect;
    }
    [StructLayout(LayoutKind.Sequential)] struct Keyboard {
        public ushort Key, Scan;
        public uint Flags, Time;
        public UIntPtr Extra;
    }
    [StructLayout(LayoutKind.Sequential)] struct Mouse {
        public int X, Y;
        public uint Data, Flags, Time;
        public UIntPtr Extra;
    }
    [StructLayout(LayoutKind.Explicit)] struct InputData {
        [FieldOffset(0)] public Keyboard Keyboard;
        [FieldOffset(0)] public Mouse Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] struct Input { public uint Type; public InputData Data; }
    public static bool ModifiersDown() {
        return new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0);
    }
    public static bool Paste() {
        Input[] inputs = new Input[4];
        ushort[] keys = {0x11, 0x56, 0x56, 0x11};
        for (int i=0;i<4;i++) {
            inputs[i].Type = 1;
            inputs[i].Data.Keyboard.Key = keys[i];
            inputs[i].Data.Keyboard.Flags = i >= 2 ? 2U : 0U;
        }
        return SendInput(4, inputs, Marshal.SizeOf(typeof(Input))) == 4;
    }
    public static IntPtr FocusedHandle(IntPtr window) {
        uint pid;
        uint thread = GetWindowThreadProcessId(window, out pid);
        GUIInfo info = new GUIInfo { Size=(uint)Marshal.SizeOf(typeof(GUIInfo)) };
        return GetGUIThreadInfo(thread, ref info) ? info.Focus : IntPtr.Zero;
    }
    public static bool EditSelection(IntPtr window,out string text,out int start,out int end) {
        text=null;start=end=-1;IntPtr result;
        StringBuilder name=new StringBuilder(128);GetClassName(window,name,name.Capacity);
        string className=name.ToString().ToUpperInvariant();
        if(className!="EDIT"&&!className.StartsWith("RICHEDIT")&&!className.StartsWith("WINDOWSFORMS10.EDIT")&&!className.StartsWith("WINDOWSFORMS10.RICHEDIT"))return false;
        if(SendMessageTimeout(window,0x000E,IntPtr.Zero,IntPtr.Zero,2,250,out result)==IntPtr.Zero)return false;
        int length=(int)result.ToInt64();if(length<0||length>100000)return false;
        IntPtr buffer=Marshal.AllocHGlobal(Math.Max(8,(length+1)*2));
        try {
            if(SendMessageTimeout(window,0x00B0,buffer,IntPtr.Add(buffer,4),2,250,out result)==IntPtr.Zero)return false;
            start=Marshal.ReadInt32(buffer);end=Marshal.ReadInt32(buffer,4);
            if(start<0||end<start||end>length)return false;
            if(SendMessageTimeout(window,0x000D,new IntPtr(length+1),buffer,2,250,out result)==IntPtr.Zero)return false;
            string all=Marshal.PtrToStringUni(buffer,(int)result.ToInt64());
            if(end>all.Length)return false;text=all.Substring(start,end-start);return true;
        }finally{Marshal.FreeHGlobal(buffer);}
    }
}

public sealed class FocusTarget {
    public IntPtr Window, Control;
    public int[] RuntimeId;
    public bool IsPassword;
    public bool SelectionWritable;
    int selectionStart=-1,selectionEnd=-1;
    string selectedText;
    System.Windows.Automation.Text.TextPatternRange selectedRange;
    AutomationElement selectionElement;
    public static FocusTarget Capture() {
        FocusTarget target = new FocusTarget();
        target.Window = Native.GetForegroundWindow();
        target.Control = Native.FocusedHandle(target.Window);
        try {
            AutomationElement element = AutomationElement.FocusedElement;
            if (element != null) {
                target.IsPassword = element.Current.IsPassword;
                target.RuntimeId = element.GetRuntimeId();
            }
        } catch (ElementNotAvailableException) { } catch (InvalidOperationException) { }
          catch (COMException) { }
        return target;
    }
    public bool MatchesCurrent() {
        if (Window == IntPtr.Zero || !Native.IsWindow(Window) || Native.GetForegroundWindow() != Window) return false;
        if (Control != IntPtr.Zero && Native.FocusedHandle(Window) != Control) return false;
        if (RuntimeId != null) {
            try {
                AutomationElement element = AutomationElement.FocusedElement;
                return element != null && !element.Current.IsPassword && RuntimeId.SequenceEqual(element.GetRuntimeId());
            } catch (Exception) { return false; }
        }
        return true;
    }
    public string CaptureSelection() {
        if(IsPassword||!MatchesCurrent())return null;
        string nativeText;int start,end;
        if(Native.EditSelection(Control,out nativeText,out start,out end)) {
            SelectionWritable=(Native.WindowStyle(Control,-16)&0x0800)==0;selectionStart=start;selectionEnd=end;selectedText=nativeText;return selectedText;
        }
        try {
            AutomationElement element=AutomationElement.FocusedElement;object raw;
            for(int depth=0;element!=null&&depth<6;depth++,element=TreeWalker.ControlViewWalker.GetParent(element)) {
                if(element.Current.IsPassword)return null;
                if(!element.TryGetCurrentPattern(TextPattern.Pattern,out raw))continue;
                TextPattern pattern=(TextPattern)raw;
                System.Windows.Automation.Text.TextPatternRange[] ranges=pattern.GetSelection();
                if(ranges.Length!=1)continue;
                string text=ranges[0].GetText(10001);
                if(text.Length>10000)return text;
                if(String.IsNullOrWhiteSpace(text))continue;
                object readOnly=ranges[0].GetAttributeValue(TextPattern.IsReadOnlyAttribute);
                SelectionWritable=readOnly is bool&&!(bool)readOnly;
                selectionElement=element;selectedRange=ranges[0].Clone();selectedText=text;return text;
            }
        }catch(ElementNotAvailableException){}catch(InvalidOperationException){}catch(COMException){}
        return null;
    }
    public bool SelectionMatches() {
        if(!MatchesCurrent()||selectedText==null)return false;
        try {
            if(selectionStart>=0) {
                string text;int start,end;
                return Native.EditSelection(Control,out text,out start,out end)&&start==selectionStart&&end==selectionEnd&&text==selectedText;
            }
            AutomationElement element=selectionElement??AutomationElement.FocusedElement;object raw;
            if(element==null||!element.TryGetCurrentPattern(TextPattern.Pattern,out raw))return false;
            System.Windows.Automation.Text.TextPatternRange[] ranges=((TextPattern)raw).GetSelection();
            return ranges.Length==1&&ranges[0].GetText(10001)==selectedText&&
                ranges[0].CompareEndpoints(System.Windows.Automation.Text.TextPatternRangeEndpoint.Start,selectedRange,System.Windows.Automation.Text.TextPatternRangeEndpoint.Start)==0&&
                ranges[0].CompareEndpoints(System.Windows.Automation.Text.TextPatternRangeEndpoint.End,selectedRange,System.Windows.Automation.Text.TextPatternRangeEndpoint.End)==0;
        }catch(Exception){return false;}
    }
}

public sealed class ShortcutWindow : NativeWindow, IDisposable {
    public static bool Capturing;
    public readonly string[] Choices = {"Ctrl + Shift + Space", "Ctrl + Alt + Space", "Ctrl + Shift + F9"};
    public string Label;
    public bool Registered;
    public string EditLabel;
    public string ActionLabel;
    public Action Toggle, Cancel, Edit, TextAction;
    public Action Released;
    readonly System.Windows.Forms.Timer release=new System.Windows.Forms.Timer {Interval=25};
    uint registeredKey;bool awaitingRelease;
    const int ToggleId = 19526, CancelId = 19527, EditId=19528, ActionId=19529;
    public ShortcutWindow() { CreateHandle(new CreateParams { Caption="Open Mic AI input", Parent=new IntPtr(-3) });release.Tick+=(s,e)=>{if(!awaitingRelease||(Native.GetAsyncKeyState((int)registeredKey)&0x8000)!=0)return;awaitingRelease=false;release.Stop();if(Released!=null)Released();}; }
    public bool Register(string preferred) {
        Native.UnregisterHotKey(Handle, ToggleId);
        Registered = false;
        release.Stop();awaitingRelease=false;Label=null;
        if(Hotkey.Mouse(preferred)){Label=preferred;Registered=true;return true;}
        foreach (string choice in new[] {preferred}.Concat(Choices).Distinct()) {
            uint mods,key;if(!Hotkey.Parse(choice,out mods,out key))continue;
            if (Native.RegisterHotKey(Handle, ToggleId, mods, key)) {
                registeredKey=key;Label = choice; Registered = true; return true;
            }
        }
        return false;
    }
    public void ListenForEscape(bool enabled) {
        Native.UnregisterHotKey(Handle, CancelId);
        if (enabled) Native.RegisterHotKey(Handle, CancelId, 0x4000, 0x1B);
    }
    public void RegisterEdit() {
        Native.UnregisterHotKey(Handle,EditId);EditLabel=null;
        if(Native.RegisterHotKey(Handle,EditId,0x4006,0x45))EditLabel="Ctrl + Shift + E";
        else if(Native.RegisterHotKey(Handle,EditId,0x4006,0x79))EditLabel="Ctrl + Shift + F10";
    }
    public bool RegisterAction(string preferred) {
        Native.UnregisterHotKey(Handle,ActionId);ActionLabel=null;
        foreach(string choice in new[]{preferred}.Concat(TextActions.ShortcutChoices).Where(c=>TextActions.ShortcutChoices.Contains(c)).Distinct()) {
            uint mods=choice=="Ctrl + Alt + A"?0x4003U:0x4006U,key=choice=="Ctrl + Shift + F8"?0x77U:0x41U;
            if(Native.RegisterHotKey(Handle,ActionId,mods,key)){ActionLabel=choice;return true;}
        }
        return false;
    }
    protected override void WndProc(ref Message message) {
        if (message.Msg == 0x0312&&!Capturing) {
            if (message.WParam.ToInt32() == ToggleId && Toggle != null) {Toggle();awaitingRelease=true;release.Start();}
            if (message.WParam.ToInt32() == CancelId && Cancel != null) Cancel();
            if (message.WParam.ToInt32() == EditId && Edit != null) Edit();
            if (message.WParam.ToInt32() == ActionId && TextAction != null) TextAction();
        }
        base.WndProc(ref message);
    }
    public void Dispose() {
        release.Dispose();
        Native.UnregisterHotKey(Handle, ToggleId);
        Native.UnregisterHotKey(Handle, CancelId);
        Native.UnregisterHotKey(Handle, EditId);
        Native.UnregisterHotKey(Handle, ActionId);
        DestroyHandle();
    }
}

public sealed class ChildJob : IDisposable {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr security, string name);
    [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int kind, ref Limits limits, uint length);
    [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)] struct Basic {
        public long ProcessTime, JobTime;
        public uint Flags;
        public UIntPtr MinWorking, MaxWorking;
        public uint ActiveCount;
        public UIntPtr Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] struct Counters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct Limits {
        public Basic Basic;
        public Counters IO;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    IntPtr handle;
    public ChildJob() {
        handle = CreateJobObject(IntPtr.Zero, null);
        Limits limits = new Limits(); limits.Basic.Flags = 0x2000;
        if (handle == IntPtr.Zero || !SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf(typeof(Limits)))) {
            if (handle != IntPtr.Zero) CloseHandle(handle);
            handle = IntPtr.Zero;
        }
    }
    public void Add(Process process) { if (handle != IntPtr.Zero) AssignProcessToJobObject(handle, process.Handle); }
    public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle=IntPtr.Zero; } }
}
}
