using System;
using System.Runtime.InteropServices;

namespace LocalFlow {
[ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]class DeviceEnumerator { }
[ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDeviceEnumerator {
    [PreserveSig]int EnumAudioEndpoints(int flow,uint state,out IntPtr devices);
    [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
}
[ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDevice {
    [PreserveSig]int Activate(ref Guid id,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object value);
}
[ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IEndpointVolume {
    [PreserveSig]int RegisterControlChangeNotify(IntPtr callback);
    [PreserveSig]int UnregisterControlChangeNotify(IntPtr callback);
    [PreserveSig]int GetChannelCount(out uint count);
    [PreserveSig]int SetMasterVolumeLevel(float value,ref Guid context);
    [PreserveSig]int SetMasterVolumeLevelScalar(float value,ref Guid context);
    [PreserveSig]int GetMasterVolumeLevel(out float value);
    [PreserveSig]int GetMasterVolumeLevelScalar(out float value);
    [PreserveSig]int SetChannelVolumeLevel(uint channel,float value,ref Guid context);
    [PreserveSig]int SetChannelVolumeLevelScalar(uint channel,float value,ref Guid context);
    [PreserveSig]int GetChannelVolumeLevel(uint channel,out float value);
    [PreserveSig]int GetChannelVolumeLevelScalar(uint channel,out float value);
    [PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool value,ref Guid context);
    [PreserveSig]int GetMute([MarshalAs(UnmanagedType.Bool)]out bool value);
}
public sealed class PlaybackMute : IDisposable {
    IEndpointVolume volume;bool changed;static readonly Guid Context=new Guid("6ad92f92-2fd0-4822-8b9d-95b2542aeb59");
    static IEndpointVolume Open() {
        IDeviceEnumerator enumerator=(IDeviceEnumerator)new DeviceEnumerator();IDevice device=null;
        try{Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0,1,out device));Guid id=typeof(IEndpointVolume).GUID;object value;Marshal.ThrowExceptionForHR(device.Activate(ref id,23,IntPtr.Zero,out value));return (IEndpointVolume)value;}
        finally{if(device!=null)Marshal.ReleaseComObject(device);Marshal.ReleaseComObject(enumerator);}
    }
    public static bool CurrentMuted(){IEndpointVolume endpoint=Open();try{bool mute;Marshal.ThrowExceptionForHR(endpoint.GetMute(out mute));return mute;}finally{Marshal.ReleaseComObject(endpoint);}}
    public PlaybackMute(){volume=Open();try{bool muted;Marshal.ThrowExceptionForHR(volume.GetMute(out muted));if(!muted){Guid context=Context;Marshal.ThrowExceptionForHR(volume.SetMute(true,ref context));changed=true;}}catch{Marshal.ReleaseComObject(volume);volume=null;throw;}}
    public void Dispose(){if(volume==null)return;try{if(changed){bool muted;if(volume.GetMute(out muted)>=0&&muted){Guid context=Context;volume.SetMute(false,ref context);}}}finally{Marshal.ReleaseComObject(volume);volume=null;}}
}
}
