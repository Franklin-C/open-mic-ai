using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LocalFlow {
// App-owned Jump List tasks. Windows keeps its own Pin/Close entries.
public static class TaskbarShortcuts {
    public const string AppId="OpenMicAI.Desktop";
    public static string LastError {get;private set;}
    public static string[] Selected(Settings value){List<string> tasks=new List<string>();if(value.TaskbarMenuDictate)tasks.Add("dictate");if(value.TaskbarMenuCopy)tasks.Add("copy");if(value.TaskbarMenuOpen)tasks.Add("open");if(value.TaskbarMenuBar)tasks.Add("bar");if(value.TaskbarMenuSettings)tasks.Add("settings");if(value.TaskbarMenuQuit)tasks.Add("quit");return tasks.ToArray();}
    public static string Title(string command){switch(command){case "dictate":return "Dictate / Stop";case "copy":return "Copy recent transcription";case "bar":return "Show dictation bar";case "settings":return "Settings";case "quit":return "Quit Open Mic AI";default:return "Open app";}}
    public static void SetProcessId(){try{SetCurrentProcessExplicitAppUserModelID(AppId);}catch(EntryPointNotFoundException){}}
    public static bool Publish(string executable,string[] commands,string appId=AppId){object destination=null,collection=null,removed=null;bool begun=false;try{
        destination=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("77f10cf0-3db5-4966-b520-b7c54fd35ed6")));IDestinationList list=(IDestinationList)destination;list.SetAppID(appId);uint slots;Guid arrayId=typeof(IObjectArray).GUID;list.BeginList(out slots,ref arrayId,out removed);begun=true;
        collection=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("2d3468c1-36a7-43b6-ac24-d3f02fd9607a")));IObjectCollection items=(IObjectCollection)collection;
        foreach(string command in commands){if(Array.IndexOf(AppCommandChannel.Commands,command)<0)continue;object link=CreateLink(executable,command,appId);try{items.AddObject(link);}finally{Release(link);}}
        if(commands.Length>0)list.AddUserTasks((IObjectArray)collection);list.CommitList();begun=false;LastError=null;return true;
    }catch(Exception error){LastError=error.Message;if(begun)try{((IDestinationList)destination).AbortList();}catch{}return false;}finally{Release(removed);Release(collection);Release(destination);}}
    public static object CreateLink(string executable,string command,string appId=AppId){if(Array.IndexOf(AppCommandChannel.Commands,command)<0)throw new ArgumentException("Unknown taskbar action");object value=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-c000-000000000046")));try{IShellLink link=(IShellLink)value;link.SetPath(executable);link.SetArguments("--background --taskbar-action "+command);link.SetWorkingDirectory(Path.GetDirectoryName(executable));link.SetIconLocation(executable,0);link.SetDescription(Title(command));link.SetShowCmd(7);IPropertyStore properties=(IPropertyStore)value;Set(properties,new PropertyKey("f29f85e0-4ff9-1068-ab91-08002b27b3d9",2),Title(command));Set(properties,new PropertyKey("9f4c2855-9f79-4b39-a8d0-e1d42de1d5f3",5),appId);properties.Commit();return value;}catch{Release(value);throw;}}
    static void Set(IPropertyStore store,PropertyKey key,string text){PropVariant value=new PropVariant {Type=31,Pointer=Marshal.StringToCoTaskMemUni(text)};try{store.SetValue(ref key,ref value);}finally{Marshal.FreeCoTaskMem(value.Pointer);}}
    public static string LinkArguments(object value){StringBuilder text=new StringBuilder(512);((IShellLink)value).GetArguments(text,text.Capacity);return text.ToString();}
    public static void DeleteTestList(string appId){if(appId==AppId)throw new ArgumentException("Use an isolated test identity");object value=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("77f10cf0-3db5-4966-b520-b7c54fd35ed6")));try{((IDestinationList)value).DeleteList(appId);}finally{Release(value);}}
    public static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [StructLayout(LayoutKind.Sequential)]struct PropertyKey {public Guid Format;public uint Id;public PropertyKey(string format,uint id){Format=new Guid(format);Id=id;}}
    [StructLayout(LayoutKind.Explicit,Size=24)]struct PropVariant {[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public IntPtr Pointer;}
    [ComImport,Guid("6332debf-87b5-4670-90c0-5e57b408a49e"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDestinationList {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)]string appId);void BeginList(out uint slots,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object removed);void AppendCategory([MarshalAs(UnmanagedType.LPWStr)]string name,IObjectArray items);void AppendKnownCategory(int category);void AddUserTasks(IObjectArray tasks);void CommitList();void GetRemovedDestinations(ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object removed);void DeleteList([MarshalAs(UnmanagedType.LPWStr)]string appId);void AbortList();
    }
    [ComImport,Guid("92ca9dcd-5622-4bba-a805-5e9f541bd8c9"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IObjectArray {void GetCount(out uint count);void GetAt(uint index,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object value);}
    [ComImport,Guid("5632b1a4-e38a-400a-928a-d4cd63230295"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IObjectCollection {
        void GetCount(out uint count);void GetAt(uint index,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object value);void AddObject([MarshalAs(UnmanagedType.IUnknown)]object value);void AddFromArray(IObjectArray values);void RemoveObjectAt(uint index);void Clear();
    }
    [ComImport,Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IPropertyStore {void GetCount(out uint count);void GetAt(uint index,out PropertyKey key);void GetValue(ref PropertyKey key,out PropVariant value);void SetValue(ref PropertyKey key,ref PropVariant value);void Commit();}
    [ComImport,Guid("000214f9-0000-0000-c000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IShellLink {
        void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int length,IntPtr findData,uint flags);void GetIDList(out IntPtr id);void SetIDList(IntPtr id);void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int length);void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string text);void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int length);void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string path);void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int length);void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string text);void GetHotkey(out short key);void SetHotkey(short key);void GetShowCmd(out int command);void SetShowCmd(int command);void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int length,out int index);void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string path,int index);void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);void Resolve(IntPtr window,uint flags);void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
    }
}
}
