using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LocalFlow;

public static class QualityChecks {
    // Different purposes, lengths, punctuation, corrections, uncertainty and
    // surrounding prose. Required facts are checked independently of the LLM.
    public static readonly string[][] Variations={
        new[]{"prose","I bought apples, milk and bread yesterday, but I forgot the receipt.","apples|milk|bread|yesterday|receipt"},
        new[]{"prose","Please don't call Nina or Lee until Friday because the office is closed.","Nina|Lee|Friday|closed|don't|do not"},
        new[]{"paragraphs","I am trying a different morning routine. I usually check messages before breakfast and then rush to catch the bus. This week I left my phone in the hallway and ate first. I arrived on time on Monday, but Tuesday's bus was late. I do not know whether the new routine will help long term. I will try it until Friday and then decide.","Monday|Tuesday|Friday|bus|routine"},
        new[]{"paragraphs","The garden has been dry for a month. The tomatoes survived because we watered them each evening. The lettuce did not survive. We are collecting rainwater now, but there has been no rain this week. If it rains on Saturday, we will use the collected water first. Otherwise we will use the tap. I do not want to buy new plants yet.","tomatoes|lettuce|Saturday|tap|plants"},
        new[]{"list","For groceries, add bananas, pears and three apples. Actually two bananas. Keep the fruit separate from the cleaning products.","two|bananas|pears|three|apples|separate|cleaning"},
        new[]{"list","Please bring a red folder for Nina, two pens for Lee and a blue notebook for Maya. Do not buy a replacement notebook if the blue one is unavailable.","red|Nina|two|Lee|blue|Maya|unavailable"},
        new[]{"goals","By Friday I want to finish the draft, read two chapters and save twenty dollars. I will review my progress with Nina on Monday.","Friday|two|twenty|Nina|Monday"},
        new[]{"goals","My targets for next month are walking three times a week and completing the first course module. I am not committing to a second module yet.","three|first|second|not"},
        new[]{"steps","To restart the router, switch it off, wait thirty seconds, then turn it back on. Finally check whether the laptop reconnects. Do not press the reset button.","thirty|laptop|reset|not"},
        new[]{"steps","First save the document. Next close the editor. Then install the update and reopen the document. If installation fails, keep the original file.","save|close|install|reopen|fails|original"},
        new[]{"recipe","To cook the pasta, boil two litres of water, add the pasta and cook for ten minutes. Then drain it and stir in the sauce. Do not add salt.","two|ten|sauce|salt|not"},
        new[]{"recipe","For the soup, chop two carrots, add them to a pan with one cup of stock and simmer for twenty minutes. Blend until smooth, then taste it before adding pepper.","two|one|twenty|smooth|pepper"},
        new[]{"sections","Project update. Progress the microphone works. Risks the export may fail on long files. Next steps Lee will investigate on Tuesday. We have not approved the release.","microphone|export|long|Lee|Tuesday|not"},
        new[]{"sections","Status report. Budget we have spent fifty dollars. Timeline the trial ends on Friday. Open questions we do not know whether the printer can be moved. Nina will check the room.","fifty|Friday|printer|Nina|not"},
        new[]{"email","Dear Nina, please review the attached draft before Monday. Do not forward it until Lee approves it. Kind regards, Maya.","Nina|Monday|Lee|Maya|not"},
        new[]{"email","Hi Lee. The parcel has not arrived yet. Could you check reference 7319 and send me an update on Friday? Thanks, Nina.","Lee|not|7319|Friday|Nina"},
        new[]{"table","Compare prices and stock for these products. Apples cost two dollars and we have five. Pears cost three dollars and we have four. Prices exclude tax.","Apples|two|five|Pears|three|four|tax"},
        new[]{"table","Comparison table. Plan price users. Basic five dollars two users. Team twelve dollars eight users. The prices are monthly and do not include support.","Basic|five|two|Team|twelve|eight|monthly|support|not"},
        new[]{"blog","For my blog about trying the early bus. On Monday I left at seven and reached work at eight. On Tuesday the bus was delayed. I liked having time to read, but I do not know whether this will save time every day. I will try again on Friday.","Monday|seven|eight|Tuesday|Friday|read"},
        new[]{"blog","An article for my website about growing tomatoes. I planted three seeds in March. Two plants survived the dry weather. I watered them every evening. I have not harvested anything yet. Next month I plan to try a larger pot using the soil I already have.","three|March|Two|not|harvested|pot|soil"},
        new[]{"essay","For my school essay I argue that buses should run later. Evening workers need a way home. More services may help, but the cost needs review. A Friday trial for one month could show whether enough people use it. I am not proposing a permanent change yet.","workers|cost|Friday|month|not|permanent"},
        new[]{"essay","In my assignment I argue that the school should keep its library open during lunch. Students need a quiet space. Staffing could be difficult and the room still needs supervision. A two week trial would let us test demand before deciding. We do not have demand figures yet.","lunch|quiet|staffing|supervision|two|demand|not"},
        new[]{"notes","Notes from today's meeting. We discussed the printer noise and decided to test the spare room. Lee will inspect it on Monday. Nina will check the cable. We did not approve a purchase.","printer|Lee|Monday|Nina|cable|not|purchase"},
        new[]{"notes","We met yesterday about the late parcel. Nina will check reference 7319 on Friday. Lee will contact the supplier. The cause is still unknown and we have not promised a new delivery date.","yesterday|Nina|7319|Friday|Lee|unknown|not"},
        new[]{"checklist","Before submitting the application I need boxes to tick for checking my name, attaching the receipt and signing the form. Do not submit it until Nina confirms the address.","name|receipt|signing|Nina|address|not"},
        new[]{"checklist","Packing checklist. Pack two shirts, charge the laptop and download the ticket before Monday. Leave the old passport at home.","two|laptop|ticket|Monday|old|passport"},
        new[]{"outline","Outline for my presentation. Main topics setup and privacy. Under setup put microphone choice and shortcuts. Under privacy put local recording and deleting history. Keep the talk under ten minutes.","setup|privacy|microphone|shortcuts|recording|history|ten"},
        new[]{"outline","For my talk the main points are travel and costs. Under travel include the Friday train and the Monday bus. Under costs include tickets and meals. We do not have prices yet.","travel|costs|Friday|Monday|tickets|meals|not|prices"},
        new[]{"proposal","I propose testing later opening hours because evening workers cannot visit now. The trial would run on Fridays for one month using the existing staff. Please approve it only if Lee confirms the rota. We do not know the extra cost yet.","workers|Fridays|month|staff|Lee|rota|not|cost"},
        new[]{"proposal","Proposal for a quieter workspace. Move the printer to the spare room for two weeks. Keep the existing printer and do not buy a new cable. Nina needs to check the socket before approving the trial.","printer|two|not|cable|Nina|socket"},
        new[]{"agenda","At Monday's meeting first review the parcel with Nina, then discuss the invoice with Lee, finally decide whether to contact the supplier. Keep it under twenty minutes.","Monday|parcel|Nina|invoice|Lee|supplier|twenty"},
        new[]{"agenda","Agenda for Friday. First review the trial. Second discuss the budget. Third decide whether to extend the opening hours. We have not agreed on a budget yet.","Friday|trial|budget|hours|not"},
        new[]{"faq",PolishChecks.QuestionRegression,"sky|ocean|blue"},
        new[]{"faq","Question one: Is lunch included? The answer is no, bring your own lunch. Question two: Where can I park? We have not confirmed parking yet. Please check again on Friday.","lunch|no|parking|not|Friday"},
        new[]{"social","For a LinkedIn post: We are trying later opening hours on Fridays this month. Come between six and eight and tell us what you think. These hours are temporary and we have not decided on a permanent change.","Fridays|six|eight|temporary|not|permanent"},
        new[]{"social","My Instagram caption. I tried the park route on Monday and got wet because I forgot my jacket. I plan to cycle again on Friday if it stays dry.","Monday|wet|jacket|Friday|dry"},
        new[]{"script","Nina says: Has the parcel arrived? Lee says: No, the supplier has not confirmed a date. Nina says: Please check again on Friday. Lee says: I will check reference 7319.","Nina|Lee|not|Friday|7319"},
        new[]{"script","Dialogue for the video. Maya says: Is the microphone on? Nina replies: Yes, but the window is open. Maya says: Close it before we start. Nina replies: I will close it now.","Maya|Nina|microphone|window|Close|start"}
    };
    static bool Facts(string source,string result,string required){
        result=result.Replace('’','\'');
        string[] numbers={"zero","one","two","three","four","five","six","seven","eight","nine","ten","eleven","twelve","thirteen","fourteen","fifteen","sixteen","seventeen","eighteen","nineteen","twenty"};
        // Alternate negative forms are valid phrasing changes.
        foreach(string word in required.Split('|')){
            if(word=="don't"||word=="do not"||word=="not"){if(!Regex.IsMatch(result,@"\b(?:not|no|don't|do not|haven't|hasn't|isn't|aren't|cannot|can't|didn't|doesn't|won't)\b",RegexOptions.IgnoreCase))return false;}
            else if(Array.IndexOf(numbers,word)>=0){if(!Regex.IsMatch(result,@"\b(?:"+word+"|"+Array.IndexOf(numbers,word)+@")\b",RegexOptions.IgnoreCase))return false;}
            else if(word=="fifty"){if(!Regex.IsMatch(result,@"\b(?:fifty|50)\b",RegexOptions.IgnoreCase))return false;}
            else if(result.IndexOf(word,StringComparison.OrdinalIgnoreCase)<0)return false;
        }
        foreach(Match date in Regex.Matches(source,@"\b(?:today|tomorrow|yesterday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\b",RegexOptions.IgnoreCase))if(result.IndexOf(date.Value,StringComparison.OrdinalIgnoreCase)<0)return false;
        return Formatting.NoNewRepeatedSentences(source,result);
    }
    static bool Shape(string layout,string source,string result){
        if(!PolishLayout.Fits(layout,result))return false;
        if(layout=="prose")return !result.Contains("\n");
        if(layout=="steps"||layout=="recipe"||layout=="agenda")return Regex.Matches(result,@"(?m)^\s*\d+[.)]\s").Count>=3;
        if(layout=="list"||layout=="goals")return Regex.Matches(result,@"(?m)^\s*- \S").Count>=2;
        if(layout=="checklist")return Regex.Matches(result,@"(?m)^\s*- \[ \] \S").Count>=3&&!result.Contains("[x]");
        if(layout=="outline")return Regex.Matches(result,@"(?m)^  +- \S").Count>=4;
        if(layout=="faq")return Regex.Matches(result,@"\?\n[^\n]+(?:\n\n|$)").Count>=2;
        if(layout=="script")return Regex.Matches(result,@"(?m)^[^\n]+:").Count>=4;
        if(layout=="social")return !result.Contains("#")&&!result.Contains("http");
        if(layout=="table"){
            if(source.Contains("Basic five dollars two users"))return Regex.IsMatch(result,@"(?mi)^\|[^\n]*Basic[^\n]*(?:five|5)[^\n]*(?:two|2)[^\n]*\|")&&Regex.IsMatch(result,@"(?mi)^\|[^\n]*Team[^\n]*(?:twelve|12)[^\n]*(?:eight|8)[^\n]*\|")&&result.Contains("monthly")&&result.Contains("support");
            return Regex.Matches(result,@"(?m)^\|[^\n]+\|\s*$").Count>=4;
        }
        return true;
    }
    public static async Task Run(Models models,Action<bool,string> check,Dictionary<string,object> report,string only=null){
        List<object> results=new List<object>();List<string> failures=new List<string>();report["results"]=results;
        MethodInfo router=typeof(Models).GetMethod("ChoosePolishLayout",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(string[] item in PolishChecks.AutomaticCases.Select(c=>new[]{c[0],c[1],""}).Concat(Variations).Where(c=>only==null||c[0]==only||(only=="selected"&&c.Length==2))){
            bool selected=false;
                string route=await (Task<string>)router.Invoke(models,new object[]{Formatting.PreparePolish(item[1]).Text,CancellationToken.None});
                string output=await models.Rewrite(item[1],"Smart formatting","",CancellationToken.None,"","Medium",true);
                bool ok=route==item[0]&&Shape(item[0],item[1],output)&&Facts(item[1],output,item[2]);
                results.Add(new {layout=item[0],source=item[1],result=output,route,selected,passed=ok});Console.WriteLine((ok?"PASS ":"FAIL ")+item[0]+" "+results.Count+" "+output.Replace("\n"," | "));if(!ok)failures.Add(item[0]+" "+results.Count);
        }
        foreach(string[] item in PolishChecks.AutomaticCases.Where(c=>only==null||c[0]==only||(only=="selected"&&c.Length==2))){
            string output=await models.EditSelection(item[1],"rewrite this",CancellationToken.None);
            bool ok=Shape(item[0],item[1],output)&&Facts(item[1],output,"");results.Add(new {layout=item[0],source=item[1],result=output,selected=true,passed=ok});Console.WriteLine((ok?"PASS ":"FAIL ")+"selected "+item[0]+" "+output.Replace("\n"," | "));if(!ok)failures.Add("selected "+item[0]);
        }
        foreach(string source in PolishChecks.ProseCases.Take(only!=null?0:8)){string output=await models.Rewrite(source,"Smart formatting","",CancellationToken.None,"","Medium",true);bool ok=PolishLayout.Fits("prose",output)&&!output.Contains("\n");results.Add(new {layout="prose",source,result=output,passed=ok});Console.WriteLine((ok?"PASS ":"FAIL ")+"ordinary message "+output.Replace("\n"," | "));if(!ok)failures.Add("ordinary message");}
        PersonalData personal=new PersonalData();personal.Words.Add(new WordRule {Heard="open mike",Written="Open Mic AI"});personal.Snippets.Add(new Snippet {Trigger="my email",Text="alex@example.test"});TextRules rules=new TextRules(personal);
        foreach(string style in StyleOptions.Names.Take(only!=null?0:4)){Settings prefs=new Settings();StyleOptions.Apply(prefs,style);PreparedDictation prepared=rules.Prepare("Please send the open mike report to my email before Friday.");string edited=await models.Rewrite(prepared.Text,prefs.Style,style=="Custom"?"Make the message clear. Preserve snippet placeholders exactly.":"",CancellationToken.None,"Open Mic AI",prefs.Cleanup,true);string output=rules.Finish(edited,prepared);bool ok=output.Contains("Open Mic AI")&&output.Contains("alex@example.test")&&output.Contains("Friday")&&!output.Contains("LF_SNIP_");results.Add(new {layout=style,source="Please send the open mike report to my email before Friday.",result=output,passed=ok});Console.WriteLine((ok?"PASS ":"FAIL ")+"vocabulary and snippet "+style+" "+output);if(!ok)failures.Add("personalization "+style);}
        report["failures"]=failures;report["cases"]=results.Count;check(failures.Count==0,"All 19 automatic layouts, varied content, selected text, ordinary messages and four personalized styles: "+String.Join(", ",failures));
    }
}
