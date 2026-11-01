using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalFlow {
public sealed class ChatTurn {
    public readonly string Role,Text;
    public ChatTurn(string role,string text){Role=role;Text=text;}
}
public sealed class ChatSession {
    readonly List<ChatTurn> turns=new List<ChatTurn>();
    public ChatTurn[] Turns {get{return turns.ToArray();}}
    public ChatTurn[] Request(string question){
        if(String.IsNullOrWhiteSpace(question)||question.Length>4000)throw new InvalidOperationException("Enter a message up to 4,000 characters.");
        List<ChatTurn> request=new List<ChatTurn>(turns);request.Add(new ChatTurn("user",question.Trim()));
        // Drop complete old exchanges, retaining the most recent question and its context.
        while(request.Count>1&&(request.Count>17||request.Sum(turn=>turn.Text.Length)>12000))request.RemoveRange(0,2);
        return request.ToArray();
    }
    public void Complete(string question,string answer){
        ChatTurn[] request=Request(question);turns.Clear();turns.AddRange(request);turns.Add(new ChatTurn("assistant",answer));
        while(turns.Count>2&&(turns.Count>16||turns.Sum(turn=>turn.Text.Length)>12000))turns.RemoveRange(0,2);
    }
    public void Clear(){turns.Clear();}
}
}
