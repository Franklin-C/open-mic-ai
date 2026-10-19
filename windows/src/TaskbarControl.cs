using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalFlow {
// A minimized, nonactivating window gives Explorer a live preview and native controls.
// Its thumbnail buttons never activate a window before selection capture or pasting.
public sealed class TaskbarControl : Form {
    public Action Toggle,OpenApp,CopyRecent,HideControl;
    public bool CanToggle {get;private set;}
    public bool CanCopy {get;private set;}
    public bool ButtonsReady {get;private set;}
    public int IconicResult {get;private set;}
    public int ShellResult {get;private set;}
    public int ThumbnailResult {get;private set;}
    public int LivePreviewResult {get;private set;}
    public string PreviewTitle {get;private set;}
    public string PreviewText {get;private set;}
    public bool IsRecording {get;private set;}
    public bool IsProcessing {get;private set;}
    ITaskbarList3 shell;
    string signature;
    DateTime retryAfter;
    readonly Icon mic=DrawIcon(0),stop=DrawIcon(1),copy=DrawIcon(2),app=Brand.CreateIcon(),working=DrawIcon(3);
    static readonly int taskbarCreated=RegisterWindowMessage("TaskbarButtonCreated");
    public TaskbarControl(){Text="Open Mic AI · Dictate";Icon=app;ShowInTaskbar=true;WindowState=FormWindowState.Minimized;FormBorderStyle=FormBorderStyle.FixedSingle;StartPosition=FormStartPosition.Manual;Size=new Size(320,176);PreviewTitle="Ready";PreviewText="Click a text box, then Dictate.";}
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{CreateParams value=base.CreateParams;value.ExStyle|=0x08040000;return value;}}
    void ConfigureIconic(){int enabled=1;IconicResult=DwmSetWindowAttribute(Handle,7,ref enabled,4);int bitmapResult=DwmSetWindowAttribute(Handle,10,ref enabled,4);if(IconicResult>=0)IconicResult=bitmapResult;}
    protected override void OnShown(EventArgs e){base.OnShown(e);ShowWindow(Handle,7);ConfigureIconic();Setup();}
    void Setup(){
        if(ButtonsReady||IsDisposed||DateTime.UtcNow<retryAfter)return;retryAfter=DateTime.UtcNow.AddSeconds(2);
        try{if(shell==null){shell=(ITaskbarList3)new TaskbarList();ShellResult=shell.HrInit();if(ShellResult<0){Marshal.ReleaseComObject(shell);shell=null;return;}}ShellResult=shell.ThumbBarAddButtons(Handle,3,Buttons());ButtonsReady=ShellResult>=0;if(ButtonsReady)UpdateShell();}
        catch(COMException){}catch(InvalidCastException){}
    }
    public void UpdateState(Pill pill,bool busy,string status){
        IsRecording=pill.Recording;IsProcessing=pill.Processing;
        CanToggle=IsRecording||pill.ChoosingAction||(!busy&&pill.Available);CanCopy=!String.IsNullOrWhiteSpace(pill.RecentText);
        PreviewTitle=IsRecording?"Listening":IsProcessing?pill.Caption:pill.ChoosingAction?"Selected text":pill.ResultReady?pill.Caption:status??"Ready";
        PreviewText=IsRecording||IsProcessing||pill.ChoosingAction||pill.ResultReady?pill.TranscriptText:pill.RecentText;
        if(String.IsNullOrWhiteSpace(PreviewText))PreviewText=IsRecording?"Speak…":IsProcessing?"Finishing your text…":"Click a text box, then Dictate.";
        // Keep the latest words readable during a long recording.
        if(PreviewText.Length>500)PreviewText="… "+PreviewText.Substring(PreviewText.Length-500);
        string next=PreviewTitle+"\n"+PreviewText+"\n"+IsRecording+IsProcessing+CanToggle+CanCopy;
        Setup();if(signature==next)return;signature=next;Text="Open Mic AI · "+PreviewTitle;
        UpdateShell();DwmInvalidateIconicBitmaps(Handle);
    }
    ThumbButton[] Buttons(){return new[]{Button(1,IsRecording?stop:mic,IsRecording?"Stop recording":"Dictate",CanToggle),Button(2,copy,"Copy recent transcription",CanCopy),Button(3,app,"Open app",true)};}
    static ThumbButton Button(uint id,Icon icon,string tip,bool enabled){return new ThumbButton {Mask=14,Id=id,Icon=icon.Handle,Tip=tip,Flags=enabled?2u:3u};}
    void UpdateShell(){if(!ButtonsReady||shell==null)return;try{shell.ThumbBarUpdateButtons(Handle,3,Buttons());shell.SetOverlayIcon(Handle,IsRecording?stop.Handle:IsProcessing?working.Handle:IntPtr.Zero,IsRecording?"Recording":IsProcessing?"Adjusting with AI":"Ready");string tip="Open Mic AI · "+PreviewTitle; shell.SetThumbnailTooltip(Handle,tip.Length>259?tip.Substring(0,259):tip);}catch(COMException){}}
    public void InvokeCommand(int command){if(command==1&&CanToggle&&Toggle!=null)Toggle();else if(command==2&&CanCopy&&CopyRecent!=null)CopyRecent();else if(command==3&&OpenApp!=null)OpenApp();}
    protected override void WndProc(ref Message message){
        if(message.Msg==taskbarCreated){ButtonsReady=false;retryAfter=DateTime.MinValue;Setup();}
        if(message.Msg==0x111&&((message.WParam.ToInt64()>>16)&0xffff)==0x1800){InvokeCommand((int)(message.WParam.ToInt64()&0xffff));return;}
        if(message.Msg==0x323){long size=message.LParam.ToInt64();SendBitmap(false,(int)((size>>16)&0xffff),(int)(size&0xffff));return;}
        if(message.Msg==0x326){SendBitmap(true,ClientSize.Width,ClientSize.Height);return;}
        if(message.Msg==0x21){message.Result=new IntPtr(3);return;}
        if(message.Msg==0x112&&(message.WParam.ToInt64()&0xfff0)==0xf120){if(OpenApp!=null)BeginInvoke(OpenApp);return;}
        base.WndProc(ref message);
    }
    protected override void OnFormClosing(FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;if(HideControl!=null)BeginInvoke(HideControl);}base.OnFormClosing(e);}
    public Bitmap RenderPreview(int maxWidth,int maxHeight){
        float scale=Math.Min(1,Math.Min(Math.Max(1,maxWidth)/320f,Math.Max(1,maxHeight)/176f));
        Bitmap bitmap=new Bitmap(Math.Max(1,(int)(320*scale)),Math.Max(1,(int)(176*scale)),PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(bitmap)){g.Clear(Color.White);g.ScaleTransform(scale,scale);g.SmoothingMode=SmoothingMode.AntiAlias;
            if(Brand.Logo!=null)g.DrawImage(Brand.Logo,new Rectangle(15,13,25,25));
            using(Font title=new Font("Segoe UI",11,FontStyle.Bold))g.DrawString(PreviewTitle??"Ready",title,Brushes.Black,new RectangleF(48,15,255,24));
            using(Font body=new Font("Segoe UI",11))using(Brush ink=new SolidBrush(Theme.Ink))using(StringFormat format=new StringFormat {Trimming=StringTrimming.EllipsisWord})g.DrawString(PreviewText??"",body,ink,new RectangleF(17,49,286,83),format);
            using(Pen line=new Pen(Theme.Line))g.DrawLine(line,17,142,303,142);
            using(Font hint=new Font("Segoe UI",8.5f))using(Brush muted=new SolidBrush(Theme.Muted))g.DrawString(IsRecording?"Stop below to finish":"Dictate  ·  Copy recent  ·  Open app",hint,muted,new PointF(17,151));
        }return bitmap;
    }
    void SendBitmap(bool live,int width,int height){using(Bitmap bitmap=RenderPreview(width,height)){
        BitmapInfo info=new BitmapInfo {Size=40,Width=bitmap.Width,Height=-bitmap.Height,Planes=1,Bits=32,ImageSize=(uint)(bitmap.Width*bitmap.Height*4)};IntPtr pixels;IntPtr handle=CreateDIBSection(IntPtr.Zero,ref info,0,out pixels,IntPtr.Zero,0);if(handle==IntPtr.Zero)return;
        try{BitmapData data=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{byte[] row=new byte[bitmap.Width*4];for(int y=0;y<bitmap.Height;y++){Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);Marshal.Copy(row,0,IntPtr.Add(pixels,y*row.Length),row.Length);}}finally{bitmap.UnlockBits(data);}if(live)LivePreviewResult=DwmSetIconicLivePreviewBitmap(Handle,handle,IntPtr.Zero,0);else ThumbnailResult=DwmSetIconicThumbnail(Handle,handle,0);}finally{DeleteObject(handle);}
    }}
    static Icon DrawIcon(int kind){using(Bitmap bitmap=new Bitmap(32,32,PixelFormat.Format32bppArgb)){using(Graphics g=Graphics.FromImage(bitmap)){g.SmoothingMode=SmoothingMode.AntiAlias;using(Pen pen=new Pen(Color.FromArgb(30,30,30),2.5f)){pen.StartCap=pen.EndCap=LineCap.Round;if(kind==0){using(GraphicsPath shape=Theme.Round(new Rectangle(12,5,8,14),4))g.DrawPath(pen,shape);g.DrawArc(pen,8,10,16,15,0,180);g.DrawLine(pen,16,25,16,29);g.DrawLine(pen,11,29,21,29);}else if(kind==2){g.DrawRectangle(pen,7,6,15,16);g.FillRectangle(Brushes.White,11,10,15,17);g.DrawRectangle(pen,11,10,15,17);}else if(kind==1){using(Brush red=new SolidBrush(Theme.Accent))g.FillEllipse(red,3,3,26,26);g.FillRectangle(Brushes.White,11,11,10,10);}else{using(Pen red=new Pen(Theme.Accent,4))g.DrawArc(red,5,5,22,22,-70,280);}}}IntPtr handle=bitmap.GetHicon();try{return (Icon)Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}}}
    protected override void Dispose(bool disposing){if(disposing){if(shell!=null){Marshal.ReleaseComObject(shell);shell=null;}mic.Dispose();stop.Dispose();copy.Dispose();working.Dispose();}base.Dispose(disposing);if(disposing)app.Dispose();}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int RegisterWindowMessage(string name);
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
    [StructLayout(LayoutKind.Sequential)]struct BitmapInfo {public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int XPixels,YPixels;public uint Colors,Important;}
    [DllImport("gdi32.dll")]static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr bitmap);
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    [DllImport("dwmapi.dll")]static extern int DwmInvalidateIconicBitmaps(IntPtr window);
    [DllImport("dwmapi.dll")]static extern int DwmSetIconicThumbnail(IntPtr window,IntPtr bitmap,uint flags);
    [DllImport("dwmapi.dll")]static extern int DwmSetIconicLivePreviewBitmap(IntPtr window,IntPtr bitmap,IntPtr point,uint flags);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct ThumbButton {public uint Mask,Id,Bitmap;public IntPtr Icon;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Tip;public uint Flags;}
    [ComImport,Guid("56FDF344-FD6D-11d0-958A-006097C9A090"),ClassInterface(ClassInterfaceType.None)]class TaskbarList {}
    [ComImport,Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ITaskbarList3 {
        [PreserveSig]int HrInit();[PreserveSig]int AddTab(IntPtr window);[PreserveSig]int DeleteTab(IntPtr window);[PreserveSig]int ActivateTab(IntPtr window);[PreserveSig]int SetActiveAlt(IntPtr window);
        [PreserveSig]int MarkFullscreenWindow(IntPtr window,[MarshalAs(UnmanagedType.Bool)]bool fullscreen);
        [PreserveSig]int SetProgressValue(IntPtr window,ulong complete,ulong total);[PreserveSig]int SetProgressState(IntPtr window,uint state);
        [PreserveSig]int RegisterTab(IntPtr tab,IntPtr parent);[PreserveSig]int UnregisterTab(IntPtr tab);[PreserveSig]int SetTabOrder(IntPtr tab,IntPtr before);[PreserveSig]int SetTabActive(IntPtr tab,IntPtr parent,uint flags);
        [PreserveSig]int ThumbBarAddButtons(IntPtr window,uint count,[In,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)]ThumbButton[] buttons);
        [PreserveSig]int ThumbBarUpdateButtons(IntPtr window,uint count,[In,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)]ThumbButton[] buttons);
        [PreserveSig]int ThumbBarSetImageList(IntPtr window,IntPtr list);
        [PreserveSig]int SetOverlayIcon(IntPtr window,IntPtr icon,[MarshalAs(UnmanagedType.LPWStr)]string description);
        [PreserveSig]int SetThumbnailTooltip(IntPtr window,[MarshalAs(UnmanagedType.LPWStr)]string tip);
        [PreserveSig]int SetThumbnailClip(IntPtr window,IntPtr rect);
    }
}
}
