using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace LocalFlow {
public static class Wav {
    public static byte[] Encode(byte[] pcm) {
        using (MemoryStream stream = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(stream)) {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length); writer.Write(pcm);
            return stream.ToArray();
        }
    }
}

public sealed class Utterances {
    public Action<byte[]> Submit;
    public float Level;
    MemoryStream current = new MemoryStream();
    Queue<byte[]> preroll = new Queue<byte[]>();
    readonly object gate=new object();int revision;
    long audioBytes,lastVoiceBytes,voiceBytes;
    public void Activity(out bool speech,out double duration,out double silence){lock(gate){speech=voiceBytes>=6400;duration=audioBytes/32000.0;silence=(audioBytes-lastVoiceBytes)/32000.0;}}
    public int Revision {get{lock(gate)return revision;}}
    public byte[] Preview(out int version){lock(gate){version=revision;if(!spoke||current.Length<16000)return null;int length=(int)current.Length-Math.Max(0,silentBytes-6400);byte[] audio=new byte[length+6400];Array.Copy(current.GetBuffer(),audio,length);return audio;}}
    int silentBytes;
    bool spoke;
    const int PauseBytes = 14400; // 450 ms; trailing silence is trimmed before inference.
    public void Push(byte[] pcm) {lock(gate)PushLocked(pcm);}
    void PushLocked(byte[] pcm) {
        double squares=0;
        for (int i=0;i+1<pcm.Length;i+=2) {
            short sample=(short)(pcm[i] | pcm[i+1]<<8); double value=sample/32768.0; squares+=value*value;
        }
        Level=pcm.Length==0 ? 0 : (float)Math.Sqrt(squares/Math.Max(1,pcm.Length/2));
        bool voice=Level>0.0035;
        audioBytes+=pcm.Length;if(voice){lastVoiceBytes=audioBytes;voiceBytes+=pcm.Length;}
        if (!spoke) {
            preroll.Enqueue(pcm); while (preroll.Count>5) preroll.Dequeue();
            if (!voice) return;
            foreach (byte[] block in preroll) current.Write(block,0,block.Length);
            preroll.Clear(); spoke=true;
        } else current.Write(pcm,0,pcm.Length);
        silentBytes=voice ? 0 : silentBytes+pcm.Length;
        if (silentBytes>=PauseBytes || current.Length>=15*32000) Commit();
    }
    void Commit() {
        if (spoke && current.Length>=3200) {
            // Preserve 200 ms silence after speech, avoid transcribing a long silent tail.
            int length=(int)current.Length-Math.Max(0,silentBytes-6400);
            byte[] audio=new byte[length+6400]; // Pad the edge to avoid clipped final phonemes.
            Array.Copy(current.GetBuffer(),audio,length);
            if (Submit!=null) Submit(audio);
        }
        current.SetLength(0); silentBytes=0; spoke=false; preroll.Clear();revision++;
    }
    public void Finish() {lock(gate)Commit();}
}

public sealed class Microphone : IDisposable {
    const uint DataMessage=0x3C0, CallbackFunction=0x30000;
    [StructLayout(LayoutKind.Sequential)] struct Format {
        public ushort Tag, Channels; public uint Rate, BytesPerSecond; public ushort BlockAlign, Bits, Extra;
    }
    [StructLayout(LayoutKind.Sequential)] struct Header {
        public IntPtr Data; public uint Length, Recorded; public UIntPtr User;
        public uint Flags, Loops; public IntPtr Next; public UIntPtr Reserved;
    }
    delegate void Callback(IntPtr input,uint message,UIntPtr instance,IntPtr header,UIntPtr extra);
    [DllImport("winmm.dll")] static extern uint waveInOpen(out IntPtr input,uint device,ref Format format,Callback callback,UIntPtr instance,uint flags);
    [DllImport("winmm.dll")] static extern uint waveInPrepareHeader(IntPtr input,IntPtr header,uint size);
    [DllImport("winmm.dll")] static extern uint waveInUnprepareHeader(IntPtr input,IntPtr header,uint size);
    [DllImport("winmm.dll")] static extern uint waveInAddBuffer(IntPtr input,IntPtr header,uint size);
    [DllImport("winmm.dll")] static extern uint waveInStart(IntPtr input);
    [DllImport("winmm.dll")] static extern uint waveInStop(IntPtr input);
    [DllImport("winmm.dll")] static extern uint waveInReset(IntPtr input);
    [DllImport("winmm.dll")] static extern uint waveInClose(IntPtr input);
    [DllImport("winmm.dll")] public static extern uint waveInGetNumDevs();
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Caps {
        public ushort Manufacturer, Product; public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Name;
        public uint Formats; public ushort Channels, Reserved;
    }
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] static extern uint waveInGetDevCaps(UIntPtr id,out Caps caps,uint size);
    public static List<string> Names() {
        List<string> names=new List<string>{"System default"};
        for (uint i=0;i<waveInGetNumDevs();i++) {
            Caps caps;
            if (waveInGetDevCaps(new UIntPtr(i),out caps,(uint)Marshal.SizeOf(typeof(Caps)))==0) names.Add(caps.Name);
            else names.Add("Microphone "+i);
        }
        return names;
    }
    class Buffer { public IntPtr Header, Data; }
    IntPtr handle;
    List<Buffer> buffers=new List<Buffer>();
    HashSet<IntPtr> headers=new HashSet<IntPtr>();
    Callback callback;
    object gate=new object();
    bool capturing;
    volatile bool closing;
    public Action<byte[]> Audio;
    public long TotalBytes;
    public Microphone(int device) {
        callback=OnBuffer;
        Format format=new Format {Tag=1,Channels=1,Rate=16000,BytesPerSecond=32000,BlockAlign=2,Bits=16};
        uint result=waveInOpen(out handle,device<0 ? uint.MaxValue : (uint)device,ref format,callback,UIntPtr.Zero,CallbackFunction);
        if (result!=0) throw new InvalidOperationException("Microphone unavailable (code "+result+"). Choose another microphone in Settings.");
        try {
            for (int i=0;i<8;i++) {
                Buffer b=new Buffer { Data=Marshal.AllocHGlobal(1280), Header=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Header))) };
                buffers.Add(b);
                headers.Add(b.Header);
                Header header=new Header {Data=b.Data,Length=1280};
                Marshal.StructureToPtr(header,b.Header,false);
                if (waveInPrepareHeader(handle,b.Header,(uint)Marshal.SizeOf(typeof(Header)))!=0 ||
                    waveInAddBuffer(handle,b.Header,(uint)Marshal.SizeOf(typeof(Header)))!=0)
                    throw new InvalidOperationException("Could not prepare the microphone.");
            }
        } catch { Dispose(); throw; }
    }
    void OnBuffer(IntPtr input,uint message,UIntPtr instance,IntPtr pointer,UIntPtr extra) {
        if (message!=DataMessage) return;
        lock(gate) {
            if (!headers.Contains(pointer)) return;
            Header header=(Header)Marshal.PtrToStructure(pointer,typeof(Header));
            if (capturing && header.Recorded>0) {
                byte[] data=new byte[header.Recorded]; Marshal.Copy(header.Data,data,0,data.Length); TotalBytes+=data.Length;
                try { if (Audio!=null) Audio(data); } catch { }
            }
            if (!closing) waveInAddBuffer(input,pointer,(uint)Marshal.SizeOf(typeof(Header)));
        }
    }
    public void Start() {
        capturing=true;
        if (waveInStart(handle)!=0) {capturing=false;throw new InvalidOperationException("Could not start recording.");}
    }
    public void Stop() {
        if (handle==IntPtr.Zero) return;
        closing=true; waveInStop(handle); waveInReset(handle);
        lock(gate) capturing=false;
    }
    public void Dispose() {
        if (handle==IntPtr.Zero) return;
        Stop();
        lock(gate) {
            foreach (Buffer buffer in buffers) {
                waveInUnprepareHeader(handle,buffer.Header,(uint)Marshal.SizeOf(typeof(Header)));
                headers.Remove(buffer.Header);
                Marshal.FreeHGlobal(buffer.Header); Marshal.FreeHGlobal(buffer.Data);
            }
        }
        waveInClose(handle); handle=IntPtr.Zero; buffers.Clear();
        GC.KeepAlive(callback);
    }
}
}
