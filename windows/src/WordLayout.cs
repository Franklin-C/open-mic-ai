using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LocalFlow {
// Recover the model's layout using source words. A model's grammar substitution
// must not throw away an otherwise useful list, nor remove the introduction.
public static class WordLayout {
    const string Word=@"[\p{L}\p{M}\p{N}_]+(?:['\u2019][\p{L}\p{M}\p{N}_]+)*";
    static string Key(string word){word=word.Replace('\u2019','\'').ToLowerInvariant();if(word.Length>5&&word.EndsWith("ing"))return word.Substring(0,word.Length-3);if(word.Length>4&&word.EndsWith("s"))return word.Substring(0,word.Length-1);return word;}
    public static string Project(string source,string layout){
        Match[] original=Regex.Matches(source,Word).Cast<Match>().ToArray();
        Match[] edited;
        // Keep original offsets for layout markers while excluding list numbers.
        edited=Regex.Matches(layout,Word).Cast<Match>().Where(m=>!Regex.IsMatch(layout.Substring(layout.LastIndexOf('\n',Math.Max(0,m.Index-1))+1,m.Index-(layout.LastIndexOf('\n',Math.Max(0,m.Index-1))+1)),@"^\s*$")||!Regex.IsMatch(layout.Substring(m.Index),@"^\d+[.)]\s+")).ToArray();
        int n=original.Length,mCount=edited.Length;if(n==0||mCount==0||n>3000||mCount>3000)return null;
        int[,] lengths=new int[n+1,mCount+1];string[] a=original.Select(w=>Key(w.Value)).ToArray(),b=edited.Select(w=>Key(w.Value)).ToArray();
        for(int i=n-1;i>=0;i--)for(int j=mCount-1;j>=0;j--)lengths[i,j]=a[i]==b[j]?lengths[i+1,j+1]+1:Math.Max(lengths[i+1,j],lengths[i,j+1]);
        if(lengths[0,0]<Math.Min(n,mCount)*.6)return null;
        Dictionary<int,int> map=new Dictionary<int,int>();int x=0,y=0;
        while(x<n&&y<mCount){if(a[x]==b[y]){map[y]=x;x++;y++;}else if(lengths[x+1,y]>=lengths[x,y+1])x++;else y++;}
        Dictionary<int,string> breaks=new Dictionary<int,string>(),punctuation=new Dictionary<int,string>(),casing=new Dictionary<int,string>();
        int previousLineEnd=-1,previousLineOffset=-1;bool previousList=false;
        foreach(Match line in Regex.Matches(layout,@"(?m)^[^\r\n]+")){
            int[] matches=map.Keys.Where(j=>edited[j].Index>=line.Index&&edited[j].Index<line.Index+line.Length).OrderBy(j=>j).ToArray();if(matches.Length==0)continue;
            int first=map[matches[0]],last=map[matches[matches.Length-1]];
            string marker=Regex.Match(line.Value,@"^[ \t]*(?:- (?:\[[ xX]\] )?|\d+[.)]\s+)").Value;
            // Words omitted between two model lines belong to the following
            // line. Words before the first list item remain introductory prose.
            int start=previousLineEnd>=0?previousLineEnd+1:first;
            if(marker.Length==0&&previousLineEnd<0)start=0;
            bool blank=previousLineOffset<0||Regex.IsMatch(layout.Substring(previousLineOffset,line.Index-previousLineOffset),@"\r?\n[ \t]*\r?\n");
            string separation=marker.Length>0?(previousList?"\n":"\n\n"):(blank?"\n\n":"\n");
            if(start>0)breaks[start]=separation+marker;else if(marker.Length>0)breaks[0]=marker;
            if(start>0&&marker.Length>0&&previousLineEnd<0)punctuation[start-1]=":";
            string tail=layout.Substring(edited[matches[matches.Length-1]].Index+edited[matches[matches.Length-1]].Length,line.Index+line.Length-edited[matches[matches.Length-1]].Index-edited[matches[matches.Length-1]].Length).Trim();
            if(Regex.IsMatch(tail,@"^[.!?,:;]*$"))punctuation[last]=tail;
            foreach(int j in matches){casing[map[j]]=edited[j].Value;int end=edited[j].Index+edited[j].Length;int next=j+1<mCount?edited[j+1].Index:layout.Length;string gap=layout.Substring(end,next-end);if(!gap.Contains("\n")&&Regex.IsMatch(gap,@"^[ \t]*[.!?,:;]+[ \t]*$"))punctuation[map[j]]=gap.Trim();}
            previousLineEnd=last;previousLineOffset=line.Index+line.Length;previousList=marker.Length>0;
        }
        StringBuilder result=new StringBuilder();
        result.Append(source.Substring(0,original[0].Index));
        for(int i=0;i<n;i++){
            string boundary;if(breaks.TryGetValue(i,out boundary))result.Append(boundary);else if(i>0)result.Append(' ');
            string word=original[i].Value,modelWord;
            if(word.Equals("i",StringComparison.OrdinalIgnoreCase))word="I";
            else if(casing.TryGetValue(i,out modelWord)&&Char.IsUpper(modelWord[0])&&(i==0||breaks.ContainsKey(i)||(punctuation.ContainsKey(i-1)&&Regex.IsMatch(punctuation[i-1],@"[.!?]$"))||(modelWord.Length>1&&!Regex.IsMatch(modelWord.Substring(1),@"^[a-z]+$"))))word=Char.ToUpperInvariant(word[0])+word.Substring(1);
            result.Append(word);string punct;
            if(punctuation.TryGetValue(i,out punct))result.Append(punct);
            else {int end=original[i].Index+original[i].Length,next=i+1<n?original[i+1].Index:source.Length;string gap=source.Substring(end,next-end).Trim();if(!gap.Contains("\n"))result.Append(gap);}
        }
        string output=Regex.Replace(result.ToString().Trim(),@"(?m)^(\s*(?:- (?:\[[ xX]\] )?|\d+[.)] ))and\s+","$1",RegexOptions.IgnoreCase);
        return Formatting.PolishWordsPreserved(source,output)?output:null;
    }
}
}
