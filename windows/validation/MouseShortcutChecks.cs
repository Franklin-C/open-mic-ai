using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LocalFlow;

public static class MouseShortcutChecks {
    static int passed;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    sealed class QueueContext : SynchronizationContext {readonly Queue<Action> actions=new Queue<Action>();public override void Post(SendOrPostCallback action,object state){actions.Enqueue(()=>action(state));}public void Drain(){while(actions.Count>0)actions.Dequeue()();}}
    static bool Packet(HoldMouseInput input,string choice,bool down){int message=choice=="Middle mouse"?(down?0x207:0x208):(down?0x20B:0x20C);uint data=choice=="Mouse 4"?1u<<16:choice=="Mouse 5"?2u<<16:0;return (bool)typeof(HoldMouseInput).GetMethod("ProcessButton",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(input,new object[]{message,data});}
    static void Key(ShortcutPicker picker,Keys key){typeof(ShortcutPicker).GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(picker,new object[]{new Message(),key});}
    static void Pump(){DateTime end=DateTime.UtcNow.AddMilliseconds(140);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(5);}}
    [StructLayout(LayoutKind.Sequential)]struct Mouse {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Explicit)]struct Union {[FieldOffset(0)]public Mouse Mouse;}
    [StructLayout(LayoutKind.Sequential)]struct Input {public uint Type;public Union Value;}
    [DllImport("user32.dll")]static extern uint SendInput(uint count,Input[] input,int size);
    static void NativeClick(string choice,bool down){Input input=new Input();input.Type=0;input.Value.Mouse.Data=choice=="Mouse 4"?1u:choice=="Mouse 5"?2u:0;input.Value.Mouse.Flags=choice=="Middle mouse"?(down?0x20u:0x40u):(down?0x80u:0x100u);Check(SendInput(1,new[]{input},Marshal.SizeOf(typeof(Input)))==1,"Native mouse packet accepted");Pump();}
    [STAThread]public static void Main(string[] args){
        Application.EnableVisualStyles();QueueContext queue=new QueueContext();
        foreach(string choice in new[]{"Middle mouse","Mouse 4","Mouse 5"}){
            Check(Hotkey.Mouse(choice),choice+" is accepted as a shortcut");
            using(HoldMouseInput mouse=new HoldMouseInput(queue)){
                int down=0,up=0;mouse.Down=()=>down++;mouse.Up=()=>up++;mouse.CanStart=()=>true;mouse.Configure(choice);Check(mouse.Installed,"Global hook installs for "+choice);
                Check(Packet(mouse,choice,true)&&Packet(mouse,choice,true)&&Packet(mouse,choice,false),"Bound press, repeat and release are consumed for "+choice);queue.Drain();Check(down==1&&up==1,"One press and release dispatch for "+choice);
                Check(!Packet(mouse,choice,false),"Stray release passes through for "+choice);mouse.CanStart=()=>false;Check(!Packet(mouse,choice,true),"Unavailable recording leaves mouse usable for "+choice);
                string captured=null;mouse.BeginCapture(value=>captured=value);Check(Packet(mouse,choice,true),"Capture consumes mouse press for "+choice);queue.Drain();Check(captured==null,"Capture waits for release for "+choice);Check(Packet(mouse,choice,false),"Capture consumes mouse release for "+choice);queue.Drain();Check(captured==choice,"Capture reports correct button for "+choice);
                captured=null;mouse.BeginCapture(value=>captured=value);Packet(mouse,choice,true);Packet(mouse,choice,false);mouse.Dispose();queue.Drain();Check(captured==null&&!mouse.Installed,"Cancelling capture discards queued mouse choices for "+choice);
            }
        }
        using(Form form=new Form {ClientSize=new Size(550,280)})using(ShortcutPicker picker=new ShortcutPicker("Ctrl + Shift + Space")){
            form.Controls.Add(picker);TextBox other=new TextBox {Location=new Point(20,120)};form.Controls.Add(other);form.Show();picker.PerformClick();Check(picker.Capturing&&ShortcutWindow.Capturing,"Picker starts keyboard and global mouse capture together");Key(picker,Keys.Control|Keys.Alt|Keys.R);Check(!picker.Capturing&&picker.Value=="Ctrl + Alt + R","Keyboard selection still works");picker.PerformClick();Key(picker,Keys.Escape);Check(!picker.Capturing&&!ShortcutWindow.Capturing&&picker.Value=="Ctrl + Alt + R","Escape cancels both input listeners");picker.PerformClick();other.Focus();Check(!picker.Capturing&&!ShortcutWindow.Capturing,"Leaving picker cancels global capture");picker.PerformClick();picker.Visible=false;Check(!picker.Capturing,"Changing settings page cancels mouse capture");form.Close();
        }
        if(args.Contains("--native-input")){
            Point cursor=Cursor.Position;
            using(Form form=new Form {ClientSize=new Size(550,300),StartPosition=FormStartPosition.Manual,Location=new Point(cursor.X-70,cursor.Y-170),TopMost=true,Text="Open Mic AI isolated mouse fixture"})using(ShortcutPicker picker=new ShortcutPicker("Ctrl + Shift + Space"))using(HoldMouseInput runtime=new HoldMouseInput(new WindowsFormsSynchronizationContext())){
                picker.Location=new Point(20,20);picker.Width=500;form.Controls.Add(picker);int leaked=0,starts=0,ends=0;form.MouseDown+=(s,e)=>leaked++;runtime.CanStart=()=>!ShortcutWindow.Capturing;runtime.Down=()=>starts++;runtime.Up=()=>ends++;form.Show();form.Activate();Pump();
                foreach(string choice in new[]{"Middle mouse","Mouse 4","Mouse 5"}){runtime.Configure(choice);picker.PerformClick();NativeClick(choice,true);NativeClick(choice,false);Check(!picker.Capturing&&picker.Value==choice,"Mouse capture works with pointer away from picker: "+choice);Check(starts==0&&ends==0&&leaked==0,"Choosing a mouse button does not record or click through: "+choice);}
                runtime.Configure("Mouse 4");NativeClick("Mouse 4",true);NativeClick("Mouse 4",false);Check(starts==1&&ends==1,"Native side button supports hold to dictate and release to finish");form.Close();
            }
        }
        Console.WriteLine("PASS "+passed+" mouse shortcut checks");
    }
}
