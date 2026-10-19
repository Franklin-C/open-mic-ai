using System;
using System.Drawing;
using System.IO;
using System.Reflection;

[assembly: AssemblyTitle("Open Mic AI")]
[assembly: AssemblyProduct("Open Mic AI")]
[assembly: AssemblyDescription("Local dictation, rewriting, translation and summaries")]
[assembly: AssemblyVersion("3.11.0.0")]
[assembly: AssemblyFileVersion("3.11.0.0")]

namespace LocalFlow {
public static class Brand {
    public const string Name="Open Mic AI";
    public static readonly Image Logo=LoadLogo();
    static Image LoadLogo(){using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenMicAI.Logo.png")){if(stream==null)return null;using(Image image=Image.FromStream(stream))return new Bitmap(image);}}
    public static Icon CreateIcon(){using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenMicAI.Icon.ico")){if(stream==null)return (Icon)SystemIcons.Application.Clone();using(Icon icon=new Icon(stream))return (Icon)icon.Clone();}}
}
}
