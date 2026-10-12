using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LocalFlow {
public sealed class PreparedText {
    public string Text, ExtraInstruction="", Tone="";
    public bool Question;
}
public static class Formatting {
    public static string RemoveUnrequestedClosing(string source,string edited){
        if(Regex.IsMatch(source??"",@"\b(?:thanks|thank you|regards|sincerely|appreciate)\b",RegexOptions.IgnoreCase))return edited;
        return Regex.Replace(edited,@"(?:^|\n)\s*(?:Thanks|Thank you|Regards|Best regards|Sincerely)[,.!\s]*$","",RegexOptions.IgnoreCase).TrimEnd();
    }
    public static bool RewordedScopePreserved(string source,string edited){
        int original=Regex.Matches(source??"",@"[\p{L}\p{N}]+").Count,result=Regex.Matches(edited??"",@"[\p{L}\p{N}]+").Count;
        if(result>original*1.25+8)return false;
        if(!NoNewRepeatedSentences(source,edited))return false;
        if(Regex.IsMatch(edited??"",@"(?:^|\n)\s*(?:Thanks|Thank you|Regards|Best regards|Sincerely)[,.!\s]*$",RegexOptions.IgnoreCase)&&!Regex.IsMatch(source??"",@"\b(?:thanks|thank you|regards|sincerely|appreciate)\b",RegexOptions.IgnoreCase))return false;
        foreach(Match date in Regex.Matches(source??"",@"\b(?:today|tomorrow|yesterday|tonight|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|January|February|March|April|June|July|August|September|October|November|December)\b",RegexOptions.IgnoreCase))if(!Regex.IsMatch(edited??"",@"\b"+Regex.Escape(date.Value)+@"\b",RegexOptions.IgnoreCase))return false;
        return true;
    }
    static string FlatWords(string text){return String.Join(" ",Regex.Matches(text??"",@"[\p{L}\p{N}]+(?:['\u2019][\p{L}\p{N}]+)*").Cast<Match>().Select(m=>m.Value.Replace('\u2019','\'').ToLowerInvariant()));}
    public static bool NoNewRepeatedSentences(string source,string edited){
        string before=FlatWords(source),after=FlatWords(edited);
        foreach(Match sentence in Regex.Matches(edited??"",@"[^.!?\r\n]+[.!?]")){
            string words=FlatWords(sentence.Value);if(words.Split(' ').Length<6)continue;
            string pattern=@"(?<!\S)"+Regex.Escape(words)+@"(?!\S)";
            if(Regex.Matches(after,pattern).Count>Math.Max(1,Regex.Matches(before,pattern).Count))return false;
        }
        return true;
    }
    public static PreparedText PreparePolish(string text){
        PreparedText result=Prepare(text);
        if(Regex.IsMatch(text,@"\b(literal|literally|quotation marks|quoted words)\b",RegexOptions.IgnoreCase))return result;
        result.Text=Regex.Replace(result.Text,@"^\s*all right(?:[,!.]\s+|\s+(?=(?:I|we)\b))","",RegexOptions.IgnoreCase);
        result.Text=Regex.Replace(result.Text,@"\bmm[- ]hmm[.!?,]?\s*","",RegexOptions.IgnoreCase);
        string quantity=@"(?:\d+|zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty)";
        string correctionPattern=@"\b(?:actually(?:[, ]+make (?:that|it))?|no[, ]+make (?:that|it)|sorry(?:[, ]+(?:make (?:that|it)|I mean))?)[, ]+(?<replacement>"+quantity+@"\s+[^,.!?;\r\n]+)[,.!?;]?";
        // Resolve in speech order: the latest correction wins even when the
        // original item occurred earlier in the list, rather than beside it.
        int offset=0;Match correction;
        while(offset<result.Text.Length&&(correction=Regex.Match(result.Text.Substring(offset),correctionPattern,RegexOptions.IgnoreCase)).Success){
            int index=offset+correction.Index;
            string replacement=correction.Groups["replacement"].Value.Trim();
            string item=Regex.Replace(replacement,@"^"+quantity+@"\s+","",RegexOptions.IgnoreCase);
            offset=index+correction.Length;
            if(Regex.Matches(item,@"\S+").Count>4)continue;
            string prefix=result.Text.Substring(0,index);
            Match[] earlier=Regex.Matches(prefix,@"\b(?:"+quantity+@"\s+)?"+Regex.Escape(item)+@"\b",RegexOptions.IgnoreCase).Cast<Match>().ToArray();
            if(earlier.Length==0)continue;
            Match previous=earlier[earlier.Length-1];
            prefix=prefix.Substring(0,previous.Index)+replacement+prefix.Substring(previous.Index+previous.Length);
            result.Text=prefix+result.Text.Substring(index+correction.Length);
            offset=prefix.Length;
        }
        result.Text=result.Text.Trim();return result;
    }
    // An ordered word check, separate from the model's meaning review. Only
    // list numbering, punctuation, whitespace and capitalization are free.
    // Deletions still need the completeness review (fillers/corrections only).
    public static bool PolishWordsPreserved(string source,string edited){
        Func<string,string[]> words=value=>Regex.Matches((value??"").Replace('\u2019','\''),@"[\p{L}\p{M}\p{N}_]+(?:'[\p{L}\p{M}\p{N}_]+)*").Cast<Match>().Select(m=>m.Value).ToArray();
        string[] original=words(source);
        string numbered=Regex.Replace(edited??"",@"(?m)^[ \t]*\d+[.)][ \t]+","");
        string[] written=words(numbered);int position=0;
        foreach(string word in written){
            while(position<original.Length&&!String.Equals(original[position],word,StringComparison.OrdinalIgnoreCase))position++;
            if(position==original.Length)return false;
            position++;
        }
        return written.Length>0||original.Length==0;
    }
    public static bool PolishContentComplete(string source,string edited){
        Func<string,string[]> words=value=>Regex.Matches(Regex.Replace(value??"",@"(?m)^\s*\d+[.)]\s+","").Replace('\u2019','\''),@"[\p{L}\p{M}\p{N}_]+(?:'[\p{L}\p{M}\p{N}_]+)*").Cast<Match>().Select(m=>m.Value.ToLowerInvariant()).Where(w=>w!="and").ToArray();
        return words(source).SequenceEqual(words(edited));
    }
    // Supplement semantic review where a small model can confuse fulfilling a
    // list request with preserving the speaker's personal intention to make it.
    public static bool PolishPersonalContextPreserved(string source,string edited){
        Func<string,string> expand=value=>Regex.Replace(Regex.Replace(value??"",@"\b(I|we)['\u2019]d\s+like\b","$1 would like",RegexOptions.IgnoreCase),@"\b(I|we)\s+don['\u2019]t\b","$1 do not",RegexOptions.IgnoreCase);
        source=expand(source);edited=expand(edited);
        string intent=@"\b(I|we)\s+(?:(?:do\s+not|really|just|still)\s+)*(?:want|would\s+like|wish|need|plan|intend|hope)\b";
        foreach(Match statement in Regex.Matches(source,intent,RegexOptions.IgnoreCase)){
            string remainder=source.Substring(statement.Index+statement.Length);
            if(Regex.IsMatch(remainder,@"^\s+(?:you\s+to\s+)?(?:to\s+)?(?:make|format|rewrite|translate|summari[sz]e|polish|clean\s+up)\s+(?:this|it|that|the\s+text)\b",RegexOptions.IgnoreCase))continue;
            string samePerson=@"\b"+statement.Groups[1].Value+@"\s+(?:(?:do\s+not|really|just|still)\s+)*(?:want|would\s+like|wish|need|plan|intend|hope)\b";
            if(!Regex.IsMatch(edited,samePerson,RegexOptions.IgnoreCase))return false;
        }
        return true;
    }
    public static bool PolishContextAnchorsPreserved(string source,string edited){
        HashSet<string> ignored=new HashSet<string>("the a an it its they them their we us our you your i my me this that these those is are was were be been being do does did not no can could will would shall should may might must have has had to of for in on at by with from and or but then only also please get buy bring keep skip make need want all any some just really very so".Split(' '),StringComparer.OrdinalIgnoreCase);
        Func<string,string> word=value=>value.Length>4&&value.EndsWith("s",StringComparison.OrdinalIgnoreCase)?value.Substring(0,value.Length-1).ToLowerInvariant():value.ToLowerInvariant();
        HashSet<string> written=new HashSet<string>(Array.ConvertAll(Regex.Matches(edited??"",@"[\p{L}\p{N}]+").Cast<Match>().ToArray(),m=>word(m.Value)));
        foreach(Match clause in Regex.Matches(source??"",@"\b(?:if|unless|until|because|before|after|provided(?: that)?|as long as|so that|otherwise|except)\b(?<detail>[^.!?;\n]+)",RegexOptions.IgnoreCase)){
            string[] anchors=Array.ConvertAll(Regex.Matches(clause.Groups["detail"].Value,@"[\p{L}\p{N}]+").Cast<Match>().Where(m=>m.Value.Length>=3&&!ignored.Contains(m.Value)).ToArray(),m=>word(m.Value));
            if(anchors.Length>0&&!anchors.Any(w=>written.Contains(w)))return false;
        }
        return true;
    }
    static Match Recipient(string source){return Regex.Match(source,@"^\s*((?:hey|hi|hello|dear)\s+([\p{L}][\p{L}'-]*))(?=[\s,!:])",RegexOptions.IgnoreCase);}
    static bool RecipientChangeRequested(string command,string name) {
        return Regex.IsMatch(command,@"\b(?:change|replace|remove|delete|drop)\b",RegexOptions.IgnoreCase)&&
            (command.IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0||Regex.IsMatch(command,@"\b(?:greeting|recipient|salutation)\b",RegexOptions.IgnoreCase));
    }
    public static string RestoreRecipient(string source,string command,string edited) {
        Match recipient=Recipient(source);
        if(recipient.Success&&!RecipientChangeRequested(command,recipient.Groups[2].Value)&&edited.IndexOf(recipient.Groups[2].Value,StringComparison.OrdinalIgnoreCase)<0)
            return recipient.Groups[1].Value+",\n\n"+edited;
        return edited;
    }
    public static bool CriticalDetailsPreserved(string source,string command,string edited) {
        foreach(Match placeholder in Regex.Matches(edited,@"\[(?:your name|sender(?:'s)? name|recipient(?:'s)? name|insert[^\]]*|signature)\]",RegexOptions.IgnoreCase))
            if(source.IndexOf(placeholder.Value,StringComparison.OrdinalIgnoreCase)<0&&!Regex.IsMatch(command,@"\b(?:placeholder|signature|sign.off)\b",RegexOptions.IgnoreCase))return false;
        Match recipient=Recipient(source);
        if(recipient.Success) {
            string name=recipient.Groups[2].Value;
            bool explicitlyChanged=RecipientChangeRequested(command,name);
            if(!explicitlyChanged&&edited.IndexOf(name,StringComparison.OrdinalIgnoreCase)<0)return false;
        }
        foreach(Match code in Regex.Matches(source,@"\b\d{4,}\b"))
            if(!edited.Contains(code.Value)&&!command.Contains(code.Value))return false;
        return true;
    }
    public static PreparedText Prepare(string text) {
        PreparedText result=new PreparedText {Text=text};
        // Literal requests belong to the model; do not interpret their quoted words as commands.
        if(Regex.IsMatch(text,@"\b(literal|literally|quotation marks|quoted words)\b",RegexOptions.IgnoreCase))return result;
        Dictionary<string,string> punctuation=new Dictionary<string,string> {
            {@"\bquestion mark\b","?"}, {@"\bexclamation (?:mark|point)\b","!"},
            {@"\bopen parenthes(?:is|es)\b","("}, {@"\bclose parenthes(?:is|es)\b",")"},
            {@"\bnew paragraph\b","\n\n"}, {@"\bnew line\b","\n"}
        };
        foreach(KeyValuePair<string,string> pair in punctuation)
            result.Text=Regex.Replace(result.Text,pair.Key,pair.Value,RegexOptions.IgnoreCase);
        result.Text=Regex.Replace(result.Text,@"\s+([?!,)])","$1");
        result.Text=Regex.Replace(result.Text,@"\(\s+","(");
        Match tone=Regex.Match(result.Text,@"\s*\b(?:make (?:this|it)(?: sound)?|keep (?:this|it)) (?:a )?(?:more )?(?:short )?(professional|casual|friendly|concise)(?: message)?[.!]?\s*$",RegexOptions.IgnoreCase);
        if(tone.Success) {result.Tone=tone.Groups[1].Value.ToLowerInvariant();result.ExtraInstruction="The speaker requests a "+result.Tone+" rewrite. Adjust the wording to that tone while preserving recipients and facts.";result.Text=result.Text.Substring(0,tone.Index).Trim();}
        else {
            Match shorter=Regex.Match(result.Text,@"\s*\bmake (?:this|it) (?:shorter(?: and more direct)?|more concise)[.!]?\s*$",RegexOptions.IgnoreCase);
            if(shorter.Success){result.Tone="concise";result.Text=result.Text.Substring(0,shorter.Index).Trim();}
        }
        result.Question=result.Text.TrimEnd().EndsWith("?");
        return result;
    }
    public static string Finish(string output,PreparedText original,bool professional=false) {
        // Written codes must not acquire digit separators during cleanup.
        foreach(Match number in Regex.Matches(original.Text,@"\b\d{4,}\b")) {
            string pattern=@"\b"+String.Join(@"[ ,]*",Array.ConvertAll(number.Value.ToCharArray(),c=>Regex.Escape(c.ToString())))+@"\b";
            output=Regex.Replace(output,pattern,number.Value);
        }
        output=Regex.Replace(output,@"[ \t]+\r?$","",RegexOptions.Multiline);
        if(professional){
            output=Regex.Replace(output,@"^hey\b","Hello",RegexOptions.IgnoreCase);
            foreach(KeyValuePair<string,string> pair in new Dictionary<string,string>{{"can't","cannot"},{"I'm","I am"},{"I've","I have"},{"don't","do not"},{"it's","it is"}})
                output=Regex.Replace(output,@"\b"+Regex.Escape(pair.Key)+@"\b",pair.Value,RegexOptions.IgnoreCase);
        }
        if(!Regex.IsMatch(original.Text,@"\b(literal|literally|quotation marks|quoted words)\b",RegexOptions.IgnoreCase)) {
            output=Regex.Replace(output,@"^([\s\-\d.()]*)([a-z])",m=>m.Groups[1].Value+m.Groups[2].Value.ToUpperInvariant(),RegexOptions.Multiline);
            if(!output.Contains("\n")&&Regex.IsMatch(output,@"[a-zA-Z]$"))output+=Regex.IsMatch(output,@"^(Hello|Hi|Dear|Hey) [a-zA-Z]+$",RegexOptions.IgnoreCase)?",":".";
        }
        if(original.Question)output=output.TrimEnd(' ','.','!','?')+"?";
        return output;
    }
}
}
