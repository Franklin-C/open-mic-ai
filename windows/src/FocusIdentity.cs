using System;
using System.Windows.Automation;

namespace LocalFlow {
// Providers may replace an accessibility object while keeping the same editor.
// Reacquire only a named/id'd editor at the same visible position, never a new field.
public sealed class FocusIdentity {
    public int ProcessId;
    public string Id,Name,Type,Framework;
    public double X,Y,Width,Height;
    public static FocusIdentity Capture(AutomationElement element){
        AutomationElement.AutomationElementInformation info=element.Current;
        System.Windows.Rect bounds=info.BoundingRectangle;
        return new FocusIdentity {ProcessId=info.ProcessId,Id=info.AutomationId,Name=info.Name,Type=info.ControlType.ProgrammaticName,Framework=info.FrameworkId,X=bounds.X,Y=bounds.Y,Width=bounds.Width,Height=bounds.Height};
    }
    public bool SameEditor(FocusIdentity other){
        if(other==null||ProcessId<=0||ProcessId!=other.ProcessId||Type!=other.Type||Framework!=other.Framework||(Type!="ControlType.Edit"&&Type!="ControlType.Document"))return false;
        if(!String.IsNullOrWhiteSpace(Id)){if(Id!=other.Id)return false;}
        else if(!String.IsNullOrWhiteSpace(other.Id)||String.IsNullOrWhiteSpace(Name)||Name!=other.Name)return false;
        return Width>0&&Height>0&&other.Width>0&&other.Height>0&&Math.Abs(X-other.X)<=2&&Math.Abs(Y-other.Y)<=2&&Math.Abs(Width-other.Width)<=2&&Math.Abs(Height-other.Height)<=2;
    }
}
}
