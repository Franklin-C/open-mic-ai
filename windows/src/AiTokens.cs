using System;
using System.Collections.Generic;

namespace LocalFlow {
public sealed class AiTokenUsage {
    public readonly long Input,Output;
    public AiTokenUsage(long input,long output){Input=input;Output=output;}
    static bool Count(Dictionary<string,object> usage,string key,out long count){
        count=0;object value;if(!usage.TryGetValue(key,out value)||!(value is int||value is long||value is decimal||value is double))return false;
        try{decimal number=Convert.ToDecimal(value);if(number<0||number>long.MaxValue||number!=Decimal.Truncate(number))return false;count=(long)number;return true;}catch(OverflowException){return false;}
    }
    public static AiTokenUsage Read(Dictionary<string,object> response){
        object value;if(response==null||!response.TryGetValue("usage",out value))return null;
        Dictionary<string,object> usage=value as Dictionary<string,object>;long input,output;
        return usage!=null&&Count(usage,"prompt_tokens",out input)&&Count(usage,"completion_tokens",out output)?new AiTokenUsage(input,output):null;
    }
}
}
