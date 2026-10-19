using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace LocalFlow {
public static class TrayShortcuts {
    public static void Update(ContextMenuStrip menu,Settings settings,string status,bool recording,bool canDictate,bool canCopy,bool busy,Action<string> execute){
        ToolStripItem[] previous=menu.Items.Cast<ToolStripItem>().ToArray();menu.Items.Clear();foreach(ToolStripItem item in previous)item.Dispose();
        menu.Items.Add(new ToolStripMenuItem(status??"Open Mic AI") {Enabled=false});
        string[] commands=TaskbarShortcuts.Selected(settings);if(commands.Length>0)menu.Items.Add(new ToolStripSeparator());
        foreach(string command in commands){string action=command;string title=command=="dictate"?(recording?"Stop recording":"Dictate"):command=="copy"?"Copy recent":command=="quit"?"Quit":TaskbarShortcuts.Title(command);ToolStripMenuItem item=new ToolStripMenuItem(title) {Tag=command,Enabled=command=="dictate"?canDictate:command=="copy"?canCopy:command=="settings"?!busy:true};item.Click+=(s,e)=>execute(action);menu.Items.Add(item);}
    }
}
// Remember handles only, so a tray-menu command can return to the editor after
// Windows gives the notification menu its own temporary foreground window.
public sealed class TrayFocus {
    IntPtr window;uint process;
    public void Observe(IntPtr candidate,uint ownProcess){uint pid;Native.GetWindowThreadProcessId(candidate,out pid);if(candidate==IntPtr.Zero||pid==0||pid==ownProcess)return;StringBuilder name=new StringBuilder(128);Native.GetClassName(candidate,name,name.Capacity);if(new[]{"Shell_TrayWnd","Shell_SecondaryTrayWnd","NotifyIconOverflowWindow","TopLevelWindowForOverflowXamlIsland","XamlExplorerHostIslandWindow","Progman","WorkerW","#32768"}.Contains(name.ToString()))return;window=candidate;process=pid;}
    public bool Matches {get{return window!=IntPtr.Zero&&Native.GetForegroundWindow()==window;}}
    public bool Restore(){uint current;Native.GetWindowThreadProcessId(window,out current);return window!=IntPtr.Zero&&Native.IsWindow(window)&&current==process&&(Native.GetForegroundWindow()==window||SetForegroundWindow(window));}
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
}
}
