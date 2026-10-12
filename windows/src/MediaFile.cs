using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace LocalFlow {
public sealed class MediaChunk {
    public byte[] Pcm;
    public double Seconds;
    public bool Overlap;
}

// Windows decodes and resamples the first audio track. No extra codec binary or
// temporary audio file is bundled. Vtable slots follow mfobjects.h/mfreadwrite.h.
public static class MediaFile {
    const uint AudioStream=0xfffffffd,AllStreams=0xfffffffe;
    const int ChunkBytes=30*32000,OverlapBytes=32000;
    static readonly Guid Major=new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"),Subtype=new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"),Audio=new Guid("73647561-0000-0010-8000-00aa00389b71"),Pcm=new Guid("00000001-0000-0010-8000-00aa00389b71");
    static readonly Guid Channels=new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a"),Rate=new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba"),Bits=new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669"),Align=new Guid("322de230-9eeb-43bd-ab7a-ff412251541d"),Average=new Guid("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint flags);
    [DllImport("ole32.dll")]static extern void CoUninitialize();
    [DllImport("mfplat.dll")]static extern int MFStartup(uint version,uint flags);
    [DllImport("mfplat.dll")]static extern int MFShutdown();
    [DllImport("mfplat.dll")]static extern int MFCreateMediaType(out IntPtr type);
    [DllImport("mfreadwrite.dll",CharSet=CharSet.Unicode)]static extern int MFCreateSourceReaderFromURL(string path,IntPtr attributes,out IntPtr reader);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GuidSetter(IntPtr self,ref Guid key,ref Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int UIntSetter(IntPtr self,ref Guid key,uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int UIntGetter(IntPtr self,ref Guid key,out uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SelectStream(IntPtr self,uint stream,int selected);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SetType(IntPtr self,uint stream,IntPtr reserved,IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetMediaType(IntPtr self,uint stream,out IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int ReadSample(IntPtr self,uint stream,uint control,out uint actual,out uint flags,out long time,out IntPtr sample);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetBuffer(IntPtr self,out IntPtr buffer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int LockBuffer(IntPtr self,out IntPtr data,out uint maximum,out uint length);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int UnlockBuffer(IntPtr self);
    static T Method<T>(IntPtr obj,int slot){return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size),typeof(T));}
    static void Check(int result){if(result<0)Marshal.ThrowExceptionForHR(result);}
    static void Release(ref IntPtr obj){if(obj!=IntPtr.Zero){Marshal.Release(obj);obj=IntPtr.Zero;}}
    static void Set(IntPtr type,Guid key,Guid value){Check(Method<GuidSetter>(type,24)(type,ref key,ref value));}
    static void Set(IntPtr type,Guid key,uint value){Check(Method<UIntSetter>(type,21)(type,ref key,value));}
    static uint Get(IntPtr type,Guid key){uint value;Check(Method<UIntGetter>(type,7)(type,ref key,out value));return value;}
    public static string ValidatePath(string path) {
        if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||path.StartsWith("\\\\")||path.Contains("://"))throw new ArgumentException("Choose a local audio or video file.");
        string full=Path.GetFullPath(path);if(!File.Exists(full))throw new FileNotFoundException("The file could not be found.");return full;
    }
    public static IEnumerable<MediaChunk> Read(string path,CancellationToken cancel) {
        path=ValidatePath(path);cancel.ThrowIfCancellationRequested();
        IntPtr reader=IntPtr.Zero,type=IntPtr.Zero,actualType=IntPtr.Zero;bool com=false,started=false;
        try {
            Check(CoInitializeEx(IntPtr.Zero,0));com=true;Check(MFStartup(0x20070,0));started=true;
            Check(MFCreateSourceReaderFromURL(path,IntPtr.Zero,out reader));
            SelectStream select=Method<SelectStream>(reader,4);Check(select(reader,AllStreams,0));Check(select(reader,AudioStream,1));
            Check(MFCreateMediaType(out type));Set(type,Major,Audio);Set(type,Subtype,Pcm);Set(type,Channels,1u);Set(type,Rate,16000u);Set(type,Bits,16u);Set(type,Align,2u);Set(type,Average,32000u);
            Check(Method<SetType>(reader,7)(reader,AudioStream,IntPtr.Zero,type));Check(Method<GetMediaType>(reader,6)(reader,AudioStream,out actualType));
            if(Get(actualType,Rate)!=16000||Get(actualType,Channels)!=1||Get(actualType,Bits)!=16)throw new InvalidDataException("This file could not be converted to speech audio.");
            ReadSample read=Method<ReadSample>(reader,9);byte[] chunk=new byte[ChunkBytes];int count=0;long total=0;bool overlap=false;
            while(true) {
                cancel.ThrowIfCancellationRequested();IntPtr sample=IntPtr.Zero,buffer=IntPtr.Zero;byte[] decoded=null;uint stream,flags;long time;
                try {
                    Check(read(reader,AudioStream,0,out stream,out flags,out time,out sample));
                    if((flags&1)!=0||(flags&0x30)!=0)throw new InvalidDataException("The audio stream changed or could not be decoded.");
                    if(sample!=IntPtr.Zero) {
                        Check(Method<GetBuffer>(sample,41)(sample,out buffer));IntPtr data;uint capacity,length;Check(Method<LockBuffer>(buffer,3)(buffer,out data,out capacity,out length));
                        try {if(length>8*1024*1024||length%2!=0)throw new InvalidDataException("Invalid audio sample.");decoded=new byte[length];Marshal.Copy(data,decoded,0,decoded.Length);}
                        finally {Check(Method<UnlockBuffer>(buffer,4)(buffer));}
                    }
                } finally {Release(ref buffer);Release(ref sample);}
                if(decoded!=null)for(int offset=0;offset<decoded.Length;) {
                    cancel.ThrowIfCancellationRequested();int take=Math.Min(chunk.Length-count,decoded.Length-offset);Array.Copy(decoded,offset,chunk,count,take);offset+=take;count+=take;total+=take;
                    if(count==chunk.Length) {
                        yield return new MediaChunk {Pcm=chunk,Seconds=total/32000.0,Overlap=overlap};
                        byte[] next=new byte[ChunkBytes];Array.Copy(chunk,chunk.Length-OverlapBytes,next,0,OverlapBytes);chunk=next;count=OverlapBytes;overlap=true;
                    }
                }
                if((flags&2)!=0)break;
            }
            if(count>(overlap?OverlapBytes:0)){byte[] last=new byte[count];Array.Copy(chunk,last,count);yield return new MediaChunk {Pcm=last,Seconds=total/32000.0,Overlap=overlap};}
            if(total==0)throw new InvalidDataException("This file has no readable audio.");
        } finally {Release(ref actualType);Release(ref type);Release(ref reader);if(started)MFShutdown();if(com)CoUninitialize();}
    }
    public static bool HasSound(byte[] pcm) {
        double sum=0;for(int i=0;i+1<pcm.Length;i+=2){double value=(short)(pcm[i]|pcm[i+1]<<8)/32768.0;sum+=value*value;}
        return pcm.Length>0&&sum/(pcm.Length/2)>0.000012;
    }
}
}
