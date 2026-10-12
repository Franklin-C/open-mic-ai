using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LocalFlow {
public sealed class HistoryEntry {
    public string Id, Mode, Transcript, Result, SelectedText;
    public DateTime CreatedUtc;
    public bool VoiceEdit;
}

public sealed class HistoryStore {
    public const int Limit=200;
    readonly object gate=new object();
    readonly string path;
    readonly List<HistoryEntry> entries=new List<HistoryEntry>();
    Task writer=Task.FromResult(0);
    bool unreadableFilePreserved;
    public event Action Changed;
    public event Action<string> SaveFailed;
    public string LoadError {get;private set;}
    public string LastSaveError {get;private set;}
    public HistoryStore(string root) {
        path=Path.Combine(root,"history.json");
        if(!File.Exists(path))return;
        try {
            HistoryEntry[] loaded=Serializer().Deserialize<HistoryEntry[]>(File.ReadAllText(path));
            if(loaded==null)throw new InvalidDataException("The history file is empty or invalid.");
            entries.AddRange(loaded.Where(e=>e!=null&&!String.IsNullOrEmpty(e.Id)).Take(Limit));
        } catch(Exception error) {
            LoadError=error.Message;
            // Preserve an unreadable file before allowing new history to be saved.
            try{File.Move(path,path+".unreadable-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));}
            catch{unreadableFilePreserved=true;LoadError+=" New history cannot be saved until the file can be accessed.";}
        }
    }
    static JavaScriptSerializer Serializer(){return new JavaScriptSerializer {MaxJsonLength=64*1024*1024,RecursionLimit=20};}
    static HistoryEntry Clone(HistoryEntry e){return new HistoryEntry {Id=e.Id,CreatedUtc=e.CreatedUtc,Mode=e.Mode,Transcript=e.Transcript,Result=e.Result,SelectedText=e.SelectedText,VoiceEdit=e.VoiceEdit};}
    public HistoryEntry[] Snapshot(){lock(gate)return entries.Select(Clone).ToArray();}
    public string Add(string transcript,string result,string mode,string selectedText=null) {
        HistoryEntry entry=new HistoryEntry {Id=Guid.NewGuid().ToString("N"),CreatedUtc=DateTime.UtcNow,Mode=mode,Transcript=transcript??"",Result=result??"",SelectedText=selectedText??"",VoiceEdit=selectedText!=null};
        lock(gate){entries.Insert(0,entry);if(entries.Count>Limit)entries.RemoveRange(Limit,entries.Count-Limit);QueueSave();}
        Signal();return entry.Id;
    }
    public bool Delete(string id) {
        lock(gate){int removed=entries.RemoveAll(e=>e.Id==id);if(removed==0)return false;QueueSave();}
        Signal();return true;
    }
    public void Clear(){lock(gate){entries.Clear();QueueSave();}Signal();}
    void Signal(){Action changed=Changed;if(changed!=null)changed();}
    void QueueSave() {
        HistoryEntry[] snapshot=entries.Select(Clone).ToArray();
        writer=writer.ContinueWith(previous=>Save(snapshot),CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.Default);
    }
    void Save(HistoryEntry[] snapshot) {
        string temporary=path+".tmp";
        try {
            if(unreadableFilePreserved&&File.Exists(path))throw new IOException("The unreadable history file could not be moved. It has been preserved.");
            byte[] json=new UTF8Encoding(false).GetBytes(Serializer().Serialize(snapshot));
            using(FileStream file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(json,0,json.Length);file.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            LastSaveError=null;
        } catch(Exception error) {
            LastSaveError=error.Message;
            try{if(File.Exists(temporary))File.Delete(temporary);}catch{}
            Action<string> failed=SaveFailed;if(failed!=null)try{failed(error.Message);}catch{}
        }
    }
    public Task FlushAsync(){lock(gate)return writer;}
}

}
