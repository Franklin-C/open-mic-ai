using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LocalFlow;

public static class PolishChecks {
    public static readonly string[] ProseCases={
        "I enjoyed the apples bananas and strawberries we ate after the walk.",
        "Can you ask Nina Leo and Maya whether they can join us on Friday?",
        "The new screen is cleaner faster and easier to use and I really like it.",
        "I will send the invoice tomorrow once Maya confirms the address.",
        "Do you prefer the blue folder or the red one for Friday's meeting?",
        "I need you to call Maya when you get home because her phone is broken.",
        "I was tired yesterday but the walk helped and I feel better today.",
        "What colour is the sky? Could you explain it to me?"
    };
    public static readonly string[][] AutomaticCases={
        new[]{"prose","Could you send the blue folder to Maya before Friday? Don't include the old invoice."},
        new[]{"paragraphs","I have been thinking about how I spend my evenings. Checking messages after dinner leaves me distracted, and I end up going to bed later than planned. Last week I put my phone in the kitchen and read instead. I slept better on those nights, but I missed a call from my sister. This week I will tell her to call the house phone if she needs me. I want to try this for another week before changing anything else."},
        new[]{"list","For the picnic I need apples, bananas, pears and three bottles of water. Actually two bananas. Keep the drinks in a separate bag."},
        new[]{"goals","This month I aim to finish two books, save fifty dollars and walk for twenty minutes every morning. I will review how it went on the last Friday."},
        new[]{"steps","To reset the printer first switch it off, then unplug it for thirty seconds, plug it back in and print a test page. Don't reset it while a job is printing."},
        new[]{"recipe","For dinner wash the rice, put it in a pan with two cups of water, bring it to the boil, then reduce the heat and simmer for fifteen minutes. Leave the lid on while it cooks."},
        new[]{"sections","Progress the beta is ready and the design is approved. Blockers the export fails on large files. Next actions Maya will check the logs on Friday and Lee will test the fix. The release date is still undecided."},
        new[]{"email","Hi Maya, could you send the revised invoice before Friday? Please keep reference 7319 on it. Thanks, Alex."},
        new[]{"table","Compare these plans by price and storage. Basic costs five dollars and has ten gigabytes. Pro costs twelve dollars and has one hundred gigabytes."},
        new[]{"blog","For an article on my personal website about learning to cycle to work. My first ride on Monday took twenty minutes. On Thursday I tried the park route and avoided the traffic. I got wet on Friday because I forgot my jacket. I plan to ride twice next week if it stays dry."},
        new[]{"essay","For my class I argue that libraries should stay open later. Students who work during the day need somewhere quiet in the evening. Longer hours might help, but staffing costs need review. A trial on Thursdays for one month would give us evidence before deciding."},
        new[]{"notes","We met this morning to discuss the export error. We agreed to keep the current release date for now. Maya will investigate the large file on Friday and Lee will check the logs. We have not agreed on a fix yet."},
        new[]{"checklist","Before the trip I want boxes to tick off for packing my passport, charging the camera and checking the train time before Friday. Don't buy another ticket until Maya confirms."},
        new[]{"outline","For my talk the main topics are privacy, speed and limitations. Under privacy include offline recording and local history. Under speed include loaded models and short recordings. Under limitations include transcription errors."},
        new[]{"proposal","I suggest moving the printer to the spare room because it interrupts calls. We could try this for two weeks using the existing equipment. No new purchases are needed. Please approve the trial after Lee checks the room on Monday."},
        new[]{"agenda","At tomorrow's meeting first discuss the export error with Maya, then review the design with Lee, and finally decide whether to run another test. Keep it under thirty minutes."},
        new[]{"faq","Okay. So here are some of the questions. Number one, Mm-hmm. It's questions and answers actually. But number one, what colour is the sky? The answer would be blue. Number two is what color is water The answer would be blue."},
        new[]{"social","For my Instagram caption: We are testing later library hours on Thursdays this month. Visit between six and eight and tell us how it works for you. This is a trial and permanent hours have not been decided."},
        new[]{"script","Maya says: The export failed again. Lee replies: Was it the large test file? Maya says: Yes, the small file worked. Lee replies: Let's check the logs before changing the release date."}
    };
    public const string QuestionRegression="All right, question one. is what color is the sky. The answer is blue. Question two is what color is the ocean? The answer is also blue. Yeah.";
    public static async Task Faq(Models models,Action<bool,string> check,Dictionary<string,object> report){
        MethodInfo router=typeof(Models).GetMethod("ChoosePolishLayout",BindingFlags.Instance|BindingFlags.NonPublic);string source=Formatting.PreparePolish(QuestionRegression).Text;
        report["route"]=await (Task<string>)router.Invoke(models,new object[]{source,CancellationToken.None});List<object> results=new List<object>();report["results"]=results;bool passed=true;
        foreach(bool reworded in new[]{true})foreach(bool selected in new[]{false,true}){
            string result=selected?await models.TransformSelection(source,"Rewrite","Smart formatting","","English",CancellationToken.None,"",reworded):await models.Rewrite(QuestionRegression,"Smart formatting","",CancellationToken.None,"","Medium",reworded);
            bool paired=Regex.IsMatch(result,@"sky[^\n]*\?\n[^\n]*blue[^\n]*\n\n[^\n]*ocean[^\n]*\?\n[^\n]*blue",RegexOptions.IgnoreCase)&&Regex.Matches(result,@"\bblue\b",RegexOptions.IgnoreCase).Count==2;
            results.Add(new {source=QuestionRegression,result,reworded,selected,paired});Console.WriteLine((paired?"PASS ":"FAIL ")+(reworded?"Reworded":"My words")+" "+(selected?"selected":"dictated")+" questions: "+result.Replace("\n"," | "));passed&=paired;
        }
        check(passed&&Convert.ToString(report["route"])=="faq","Automatic Polish pairs numbered spoken questions with their supplied answers");
    }
    public static async Task Tokens(Models models,Action<bool,string> check,Dictionary<string,object> report){
        long input=0,output=0;int responses=0;Action<AiTokenUsage> record=counts=>{input+=counts.Input;output+=counts.Output;responses++;};models.TokensUsed+=record;
        try{
            await models.Rewrite("Could you send the blue folder to Maya before Friday?","Smart formatting","",CancellationToken.None,"","Medium",false);
            await models.TransformSelection("Maya will send the invoice on Friday.","Translate","Smart formatting","","French",CancellationToken.None,"",false);
            await models.TransformSelection("Maya will send the invoice on Friday. Lee will check the reference before it is sent.","Summarize","Smart formatting","","English",CancellationToken.None,"",false);
            report["input_tokens"]=input;report["output_tokens"]=output;report["total_tokens"]=input+output;report["responses"]=responses;
            check(responses>=5&&input>0&&output>0,"Resident local model reports token usage for planning, rewriting, translation, summary and checks");
        }finally{models.TokensUsed-=record;}
    }
    public static async Task Routes(Models models,Action<bool,string> check,Dictionary<string,object> report){
        MethodInfo router=typeof(Models).GetMethod("ChoosePolishLayout",BindingFlags.Instance|BindingFlags.NonPublic);List<object> results=new List<object>();List<string> failures=new List<string>();report["automatic_routes"]=results;
        foreach(string[] item in AutomaticCases.Concat(ProseCases.Select(source=>new[]{"prose",source}))){string layout=await (Task<string>)router.Invoke(models,new object[]{item[1],CancellationToken.None});results.Add(new {source=item[1],expected=item[0],layout});Console.WriteLine((layout==item[0]?"PASS ":"FAIL ")+"Auto "+item[0]+" -> "+layout);if(layout!=item[0])failures.Add(item[0]+" -> "+layout);}
        check(failures.Count==0,"All 19 implied purposes choose the expected layout: "+String.Join(", ",failures));
    }
    public static async Task Formats(Models models,Action<bool,string> check,Dictionary<string,object> report){
        string[][] cases={
            new[]{"table","Comparison table. Plan price storage. Basic five dollars ten gigabytes. Pro twelve dollars one hundred gigabytes."},
            new[]{"blog","Blog post about my first week cycling to work. On Monday the ride took twenty minutes and I arrived feeling awake. By Friday I knew a quieter route through the park. I still need a better rain jacket because I got wet on Thursday. I plan to keep riding twice a week, weather permitting."},
            new[]{"essay","Essay about keeping the library open late. Students who work during the day need somewhere quiet to study in the evening. Longer hours could help them, but staffing costs would need review. I think a one month trial on Thursdays would give us useful information before we decide."},
            new[]{"notes","Meeting notes. Discussion the export is failing on large files. Decision keep the current release date for now. Actions Maya will investigate the export on Friday and Lee will check the test files. We have not agreed on a fix yet."},
            new[]{"checklist","Travel checklist. Pack the passport, charge the camera and check the train time before Friday. Don't book another ticket until Maya confirms."},
            new[]{"outline","Outline for the talk. First topic local privacy with subpoints offline recording and local history. Second topic speed with subpoints loaded models and short recordings. Final topic limitations with subpoint transcription errors."},
            new[]{"proposal","Proposal for a quieter office. Problem the printer interrupts calls. Approach move it to the spare room for a two week trial. Scope keep the same printer and don't buy new equipment. Next action ask Lee to check the room on Monday."},
            new[]{"agenda","Agenda for Friday's review. First discuss the export error with Maya, then review the design with Lee, finally decide whether to run another test. Keep the meeting under thirty minutes."},
            new[]{"faq","FAQ about the workshop. Can I bring my laptop? Yes, please bring it fully charged. Is lunch included? No, bring your own lunch. Where should I park? We have not confirmed parking yet."},
            new[]{"social","Social post. We are testing later library hours on Thursdays this month. Come by between six and eight and tell us how it works for you. This is a trial, and we have not decided on permanent hours."},
            new[]{"script","Script for two speakers. Maya: The export failed again. Lee: Was it the large test file? Maya: Yes, but the small file worked. Lee: Let's check the logs before changing the release date."}
        };
        List<object> results=new List<object>();report["format_results"]=results;
        foreach(bool reworded in new[]{false,true})foreach(string[] item in cases.Concat(AutomaticCases)){
            Stopwatch timer=Stopwatch.StartNew();string source=Formatting.PreparePolish(item[1]).Text;
            string result=await models.Rewrite(item[1],"Smart formatting","",CancellationToken.None,"","Medium",reworded);
            results.Add(new {format=item[0],reworded,source,result,seconds=timer.Elapsed.TotalSeconds});Console.WriteLine((reworded?"REWORDED ":"MY WORDS ")+item[0]+" "+result.Replace("\n"," | "));
            check(PolishLayout.Fits(item[0],result),(reworded?"Reworded":"My words")+" renders "+item[0]);
            if(item[0]=="prose")check(!Regex.IsMatch(result,@"(?m)^\s*(?:- |\d+[.)] |\|)"),"A brief message stays a natural sentence instead of becoming a list or table");
            if(item[0]=="recipe")check(Regex.Matches(result,@"(?m)^\s*\d+[.)]\s").Count>=4&&result.Contains("fifteen")&&result.Contains("lid"),"Automatic recipe uses cooking steps and preserves timing and lid instructions");
            if(!reworded)check(Formatting.PolishContentComplete(source,result)&&Formatting.PolishWordsPreserved(source,result),"My words retains the complete wording for "+item[0]);
            if(item[0]=="table")check(Regex.IsMatch(result,@"(?mi)^\|[^\r\n]*Basic[^\r\n]*(?:five|5)[^\r\n]*(?:ten|10)[^\r\n]*\|")&&Regex.IsMatch(result,@"(?mi)^\|[^\r\n]*Pro[^\r\n]*(?:twelve|12)[^\r\n]*(?:one hundred|100)[^\r\n]*\|"),"Table keeps each plan's price and storage together");
            if(item[0]=="checklist")check(Regex.Matches(result,@"(?m)^\s*- \[ \] ").Count>=3&&!result.Contains("[x]"),"Checklist separates tasks without marking them complete");
            if(item[0]=="outline")check(Regex.IsMatch(result,@"(?m)^[ \t]+- ")&&Regex.IsMatch(result,@"(?mi)^- [^\n]*privacy[^\n]*\n(?:  - [^\n]*\n)*?  - [^\n]*local history")&&Regex.IsMatch(result,@"(?mi)^- [^\n]*speed[^\n]*\n(?:  - [^\n]*\n)*?  - [^\n]*short recordings"),"Outline keeps privacy and speed subpoints under their correct parent");
            if(item[0]=="faq")check(item[1].Contains("sky")?Regex.IsMatch(result,@"sky[^\n]*\?\n[^\n]*blue[^\n]*\n\n[^\n]*water[^\n]*\?\n[^\n]*blue",RegexOptions.IgnoreCase)&&Regex.Matches(result,@"\bblue\b",RegexOptions.IgnoreCase).Count==2:Regex.Matches(result,@"\?").Count>=3&&result.IndexOf("not confirmed",StringComparison.OrdinalIgnoreCase)>=0,"FAQ pairs supplied questions with answers and preserves uncertainty");
            if(item[0]=="social")check(!result.Contains("#")&&!result.Contains("http"),"Social formatting adds no hashtag or link");
            if(item[0]=="agenda")check(result.Contains(item[1].Contains("tomorrow")?"tomorrow":"Friday")&&result.Contains("Maya")&&result.Contains("Lee")&&Regex.IsMatch(result,@"(?:thirty|30)",RegexOptions.IgnoreCase)&&!result.Contains("logs")&&!result.Contains("assigned"),"Agenda retains timing and owners without inventing preparation");
            if(item[0]=="blog")check(!result.Contains("less rushed")&&!result.Contains("more natural")&&result.Contains("Thursday"),"Blog editing retains the reported experience without adding feelings");
            check(Formatting.NoNewRepeatedSentences(source,result),"Formatting introduces no repeated sentence: "+item[0]+" "+reworded);
            if(AutomaticCases.Contains(item)){
                string selected=await models.TransformSelection(source,"Rewrite","Smart formatting","","English",CancellationToken.None,"",reworded);results.Add(new {format=item[0],reworded,source,result=selected,selected=true});
                check(PolishLayout.Fits(item[0],selected),(reworded?"Reworded":"My words")+" automatically formats selected "+item[0]);
                if(!reworded)check(Formatting.PolishContentComplete(source,selected)&&Formatting.PolishWordsPreserved(source,selected),"Selected My words preserves complete wording: "+item[0]);
            }
        }
        // Spoken intent is content, not a saved format preference.
        string chosen=await models.Rewrite("Travel checklist. Pack the passport and charge the camera before Friday.","Smart formatting","",CancellationToken.None,"","Medium",false);
        check(PolishLayout.Fits("checklist",chosen)&&chosen.Contains("Friday"),"Spoken intent chooses a checklist without a saved format");
        await Prose(models,check,report);
        report["format_checks_complete"]=true;
    }
    public static async Task Prose(Models models,Action<bool,string> check,Dictionary<string,object> report){
        List<object> results=new List<object>();report["prose_checks"]=results;
        foreach(bool reworded in new[]{false,true})foreach(bool selected in new[]{false,true})foreach(string source in ProseCases){
            string result=selected?await models.TransformSelection(source,"Rewrite","Smart formatting","","English",CancellationToken.None,"",reworded):await models.Rewrite(source,"Smart formatting","",CancellationToken.None,"","Medium",reworded);
            results.Add(new {source,result,reworded,selected});Console.WriteLine("PROSE "+result.Replace("\n"," | "));
            check(PolishLayout.Fits("prose",result)&&!result.Contains("\n"),(reworded?"Reworded":"My words")+" keeps ordinary "+(selected?"selected text":"dictation")+" as sentences: "+results.Count);
            if(!reworded)check(Formatting.PolishWordsPreserved(source,result)&&Formatting.PolishContentComplete(source,result),"Sentence retains complete original wording: "+results.Count);
            if(source.Contains("colour"))check(!Regex.IsMatch(result,@"\bblue\b",RegexOptions.IgnoreCase),"An unanswered question remains unanswered: "+results.Count);
        }
        report["prose_checks_complete"]=true;
    }
    static bool Bullets(string text){return Regex.Matches(text,@"(?m)^\s*[-*•]\s+\S").Count>=2;}
    static bool Headings(string text){return text.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Count(line=>line.Trim().Length<48&&!Regex.IsMatch(line.Trim(),@"^(?:[-*•]|\d+[.)])\s")&&!Regex.IsMatch(line.Trim(),@"[.!?]$"))>=2;}
    public static async Task Run(Models models,Action<bool,string> check,Dictionary<string,object> report){
        List<object> results=new List<object>();report["polish_formatting_results"]=results;
        Func<string,Task<string>> polish=async source=>{Stopwatch timer=Stopwatch.StartNew();string result=await models.Rewrite(source,"Smart formatting","",CancellationToken.None,"","Medium");results.Add(new {source,result,seconds=timer.Elapsed.TotalSeconds});Console.WriteLine("POLISH "+result.Replace("\n"," | "));check(!result.Contains("```")&&!Regex.IsMatch(result,@"(?m)^#{1,6}\s")&&!result.Contains("**"),"Polish uses portable text formatting: "+results.Count);check(Formatting.PolishWordsPreserved(Formatting.Prepare(source).Text,result),"Polish adds or substitutes no words: "+results.Count);return result;};
        string exact="All right, I want to make a grocery list and I want to put apples, bananas. strawberries. Actually make that two strawberries. Mm-hmm.";
        string text=await polish(exact);check(Bullets(text)&&Regex.IsMatch(text,@"(?:want|would like).*grocery list",RegexOptions.IgnoreCase|RegexOptions.Singleline)&&text.IndexOf("apples",StringComparison.OrdinalIgnoreCase)>=0&&text.IndexOf("bananas",StringComparison.OrdinalIgnoreCase)>=0&&Regex.IsMatch(text,@"(?mi)^\s*[-*]\s*(?:(?:two|2) strawberries|strawberries[^\n]*(?:two|2))")&&!text.Contains("Actually")&&!text.Contains("Mm-hmm"),"User's grocery example retains its introductory intention and final corrected quantity");
        text=await polish("I want to make a grocery list for Friday's picnic. Please get ripe apples and three bananas, sorry make that four bananas, and two boxes of strawberries. If the strawberries are not fresh, skip them. Keep the total under 25 dollars.");check(Bullets(text)&&Regex.IsMatch(text,@"(?:want|would like).*grocery list",RegexOptions.IgnoreCase|RegexOptions.Singleline)&&new[]{"Friday","picnic","ripe","fresh","skip","25"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)&&Regex.IsMatch(text,@"(?:four|4) bananas",RegexOptions.IgnoreCase)&&Regex.IsMatch(text,@"(?:two|2) boxes",RegexOptions.IgnoreCase)&&Regex.IsMatch(text,@"(?:not|aren't|isn't).*fresh",RegexOptions.IgnoreCase),"Polish retains grocery purpose, descriptions, units, budget and conditional instructions");
        text=await polish("I'm worried about the launch because we only have one week left. I want to write down what still needs work: the login screen, the mobile layout and the export button. Please keep the existing design and don't add any new features.");check(Bullets(text)&&Regex.IsMatch(text,@"(?:worried|concerned)",RegexOptions.IgnoreCase)&&Regex.IsMatch(text,@"(?:one|1) week",RegexOptions.IgnoreCase)&&Regex.IsMatch(text,@"(?:want|would like).*write",RegexOptions.IgnoreCase|RegexOptions.Singleline)&&new[]{"login","mobile","export","existing design"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)&&Regex.IsMatch(text,@"(?:don't|do not|no).*new features",RegexOptions.IgnoreCase),"Polish retains emotional context, purpose, scope and negation around a work list");
        text=await polish("Hi Rowan, I would like to plan our visit for Saturday because that is the only day my sister can join. We should bring lunch, a camera and the blue umbrella. Please call before you leave so I can meet you at the station. Thanks, Jules.");check(Bullets(text)&&new[]{"Rowan","Saturday","sister","only day","lunch","camera","blue umbrella","station","Thanks","Jules"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)&&Regex.IsMatch(text,@"(?:would like|want).*plan",RegexOptions.IgnoreCase|RegexOptions.Singleline)&&text.IndexOf("before",StringComparison.OrdinalIgnoreCase)>=0,"Polish keeps a message's greeting, intention, reason, descriptions, closing request and sign-off");
        text=await polish("I wanted to tell you about the walk yesterday. The park was quiet, the light was beautiful and I finally felt relaxed. It reminded me of visiting my grandmother when I was little.");check(!Bullets(text)&&new[]{"walk","yesterday","park","quiet","light","beautiful","relaxed","grandmother"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)&&Regex.IsMatch(text,@"(?:wanted|want).*tell",RegexOptions.IgnoreCase|RegexOptions.Singleline),"Polish keeps descriptive narrative as prose with its personal context");
        text=await polish("I loved the apples bananas and strawberries we ate after the walk.");check(!text.Contains("\n")&&!Bullets(text)&&new[]{"apples","bananas","strawberries","walk"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0),"Polish recognizes a conversational sentence about groceries rather than making a shopping list");
        text=await polish("Can you ask Nina Leo and Maya whether they can join us on Friday?");check(text.EndsWith("?")&&!text.Contains("\n")&&!Bullets(text)&&new[]{"Nina","Leo","Maya","Friday"}.All(word=>text.Contains(word)),"Polish keeps a brief question with several names as a sentence");
        text=await polish("The new screen is cleaner faster and easier to use and I really like it.");check(!text.Contains("\n")&&!Bullets(text)&&new[]{"cleaner","faster","easier","like"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0),"Polish keeps a short opinion as prose without unnecessary headings or bullets");
        text=await polish("I want to write down the steps for backing up the photos before we replace the laptop. First copy the photos to the external drive, then open a few files to check them, and only then erase the old laptop. Do not erase anything until the backup is verified.");check(Regex.IsMatch(text,@"(?m)^1\. ")&&Regex.IsMatch(text,@"(?m)^2\. ")&&Regex.IsMatch(text,@"(?m)^3\. ")&&new[]{"photos","replace","laptop","external drive","verified"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)&&Regex.IsMatch(text,@"(?:want|would like).*write",RegexOptions.IgnoreCase|RegexOptions.Singleline)&&Regex.IsMatch(text,@"(?:not|don't|do not).*erase.*until",RegexOptions.IgnoreCase|RegexOptions.Singleline),"Polish retains the purpose and safety condition around numbered steps");
        text=await polish("The groceries we need are milk, eggs, bread and apples.");check(Bullets(text)&&new[]{"grocer","milk","eggs","bread","apples"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0),"Polish infers a bullet list while retaining its grocery context");
        text=await polish("To set up the printer first connect the USB cable then install the driver and finally print a test page.");check(Regex.IsMatch(text,@"(?m)^1\. ")&&Regex.IsMatch(text,@"(?m)^2\. ")&&Regex.IsMatch(text,@"(?m)^3\. ")&&text.Contains("USB")&&text.IndexOf("driver",StringComparison.OrdinalIgnoreCase)>=0,"Polish numbers ordered steps");
        text=await polish("Website update the deadline is Friday. Current problems a broken login button and a slow export. Next actions Maya will fix the login and Tom will investigate the export.");check(Headings(text)&&Bullets(text)&&new[]{"Friday","Maya","Tom","login","export"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0),"Polish uses existing topic words as headings in a multi-topic update");
        text=await polish("Project status the beta is due on Tuesday new paragraph blockers the export fails and the dashboard loads slowly new paragraph next actions Priya will fix the export and Leo will investigate the dashboard.");check(Headings(text)&&Bullets(text)&&text.Contains("Tuesday")&&text.Contains("Priya")&&text.Contains("Leo"),"Polish structures the whole dictation across explicit paragraph breaks");
        text=await polish("Thanks Nina I'll send the report tomorrow.");check(!Bullets(text)&&!text.Contains("\n")&&text.Contains("Nina")&&text.IndexOf("tomorrow",StringComparison.OrdinalIgnoreCase)>=0,"Polish leaves a simple message simple");
        text=await polish("Can you confirm whether the review is still on Thursday?");check(text.EndsWith("?")&&text.Contains("Thursday")&&!text.Contains("Yes")&&!Bullets(text),"Polish keeps a question rather than answering it");
        text=await polish("Meeting details the review is Wednesday at 2 PM and the access code is 7319. Do not cancel the review. Tasks Nina will bring the draft and Alex will check the budget.");check(text.Contains("7319")&&text.Contains("Wednesday")&&text.Contains("Nina")&&text.Contains("Alex")&&Regex.IsMatch(text,@"2(?::00)?\s*PM",RegexOptions.IgnoreCase)&&Regex.IsMatch(text,@"(?:not|don't|do not)\s+cancel",RegexOptions.IgnoreCase),"Polish preserves people, codes, time and negation while formatting");
        text=await polish("Tasks for Friday send the invoice to Nina and confirm the appointment with Leo make this friendly.");check(Bullets(text)&&text.Contains("Nina")&&text.Contains("Leo")&&text.Contains("Friday")&&!text.ToLowerInvariant().Contains("make this friendly"),"A spoken tone request cannot override wording-preserving Polish");
        text=await models.Rewrite("We need milk eggs and bread.","Keep my wording","",CancellationToken.None,"","Light");check(!text.Contains("\n")&&!Bullets(text),"Clean up retains its lighter editing without inferred lists");
        text=await models.Rewrite("Dear Olivia new paragraph the report is attached new paragraph please review it by Thursday.","Keep my wording","",CancellationToken.None);check(text.Split(new[]{"\n\n"},StringSplitOptions.None).Length==3&&text.Contains("Olivia")&&text.Contains("Thursday"),"Clean up keeps explicitly requested paragraph boundaries");
        string raw="um groceries milk eggs and bread";check(await models.Rewrite(raw,"Original transcript","",CancellationToken.None,"","None")==raw,"Raw transcript remains untouched");
        string context="I want to make a grocery list with apples and two strawberries.";
        check(!await models.MeaningPreserved(context,"- Apples\n- Two strawberries",CancellationToken.None,true),"Polish meaning check rejects a bare list that drops the introductory intention");
        check(!await models.MeaningPreserved(context,"Grocery list\n\n- Apples\n- Two strawberries",CancellationToken.None,true),"Polish meaning check rejects replacing an intention sentence with just a heading");
        check(await models.MeaningPreserved(exact,"I want to make a grocery list, and I want to put:\n\n- Apples\n- Bananas\n- Two strawberries",CancellationToken.None,true),"Polish meaning check accepts original wording with filler removal and a corrected quantity");
        check(!await models.MeaningPreserved("Please get apples for the picnic. If they are not ripe, skip them.","Please get apples for the picnic.",CancellationToken.None,true),"Polish meaning check rejects lost conditional detail");
        check(!await models.MeaningPreserved("I cannot attend because I am sick.","I cannot attend because I am sick. I will send notes tomorrow.",CancellationToken.None),"General meaning check still rejects invented promises");
        check(await models.MeaningPreserved("Our meeting is Friday, sorry, Saturday.","Our meeting is Saturday.",CancellationToken.None),"General meaning check still accepts a legitimate correction");
        string selected="I'd like to make a grocery list for the picnic: apples, bananas and two strawberries. Please keep the fruit separate from the drinks.";
        text=await models.TransformSelection(selected,"Rewrite","Smart formatting","","English",CancellationToken.None);Console.WriteLine("SELECTED POLISH "+text.Replace("\n"," | "));report["selected_polish_source"]=selected;report["selected_polish_result"]=text;check(Bullets(text)&&Regex.IsMatch(text,@"(?:like|want).*grocery list",RegexOptions.IgnoreCase|RegexOptions.Singleline)&&new[]{"picnic","separate","drinks","strawberries"}.All(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0),"Polish selected-text rewriting retains the introduction and closing instruction");check(Formatting.PolishWordsPreserved(selected,text),"Selected-text Polish adds or substitutes no words");
        string plain="i don't want two folders for Friday";
        text=await polish(plain);check(text.IndexOf("don't",StringComparison.OrdinalIgnoreCase)>=0&&text.IndexOf("two",StringComparison.OrdinalIgnoreCase)>=0,"Polish keeps contractions and spoken number words");
        text=await polish("hey Nina the printer don't work and i need it for Monday");check(text.IndexOf("don't",StringComparison.OrdinalIgnoreCase)>=0&&text.IndexOf("need",StringComparison.OrdinalIgnoreCase)>=0,"Polish does not change words to fix informal grammar");
        report["polish_word_checks_complete"]=true;
        string rewordSource="Hi Nina the printer don't work and I want to fix it before Monday.";
        text=await models.Rewrite(rewordSource,"Smart formatting","",CancellationToken.None,"","Medium",true);report["reworded_polish_source"]=rewordSource;report["reworded_polish_result"]=text;check(text.Contains("Nina")&&text.Contains("Monday")&&Regex.IsMatch(text,@"(?:(?:doesn't|does not) work|(?:isn't|is not) working)",RegexOptions.IgnoreCase)&&!text.Contains("Thanks"),"Reworded Polish can improve grammar while retaining names and timing");
        check(await models.MeaningPreserved(rewordSource,text,CancellationToken.None,true,false),"Reworded Polish still passes factual and completeness review");
        string[] broader={
            "Add the following bananas, pears, three apples. Actually two bananas.",
            "My goals for this month are to walk every day read two books and spend less on takeaway food. I don't want a strict plan just a few things to focus on.",
            "So to make this recipe you start by washing the rice then you put it in a pan with water and then bring it to the boil reduce the heat and simmer it for fifteen minutes and don't open the lid while it's cooking.",
            "Quick update on the app the recording button is working now but the model download is still failing and the history page needs a search box. Tomorrow I will fix the download and Alex will handle the search.",
            "Dear Maya I hope you're doing well I'm emailing because the invoice from last week has the wrong delivery address can you update it to 12 Oak Street please and send me a corrected copy thanks Daniel.",
            "The cafe had tables chairs and books everywhere and the music was quiet. I loved it and want to go again.",
            "I want to explain why I am changing my morning routine. I used to check emails as soon as I woke up, which made me feel rushed before work. Now I take a short walk and eat breakfast first. The walk helps me think about the day and decide what matters. I still check urgent messages before leaving, but I leave the rest until I reach the office. I am going to try this for two weeks and see whether I feel calmer."
        };
        foreach(bool reworded in new[]{false,true})for(int i=0;i<broader.Length;i++){
            Stopwatch timer=Stopwatch.StartNew();text=await models.Rewrite(broader[i],"Smart formatting","",CancellationToken.None,"","Medium",reworded);results.Add(new {source=broader[i],result=text,seconds=timer.Elapsed.TotalSeconds});
            Console.WriteLine((reworded?"REWORDED ":"MY WORDS ")+text.Replace("\n"," | "));
            if(!reworded)check(Formatting.PolishWordsPreserved(Formatting.PreparePolish(broader[i]).Text,text)&&Formatting.PolishContentComplete(Formatting.PreparePolish(broader[i]).Text,text),"Broader My words output preserves source content: "+i);
            bool valid=i==0?Bullets(text)&&Regex.Matches(text,@"\bbananas\b",RegexOptions.IgnoreCase).Count==1&&Regex.IsMatch(text,@"(?:two|2) bananas",RegexOptions.IgnoreCase):i==1?Bullets(text)&&text.Contains("month")&&text.Contains("strict plan"):i==2?Regex.Matches(text,@"(?m)^\d+\. ").Count>=3&&text.Contains("fifteen")&&Regex.IsMatch(text,@"(?:don't|do not).*open",RegexOptions.IgnoreCase):i==3?text.Contains("\n")&&!Regex.IsMatch(text,@"(?m)^\d+\. ")&&text.Contains("Tomorrow")&&text.Contains("Alex"):i==4?text.Contains("\n\n")&&text.Contains("Maya")&&text.Contains("12 Oak Street")&&text.Contains("Daniel"):i==5?!Bullets(text)&&!Regex.IsMatch(text,@"(?m)^\d+\. "):text.Contains("\n\n")&&!Bullets(text);
            check(valid,(reworded?"Reworded":"My words")+" selects and fulfills the contextual format: "+i);
        }
        report["polish_modes_checks_complete"]=true;
        report["polish_context_checks_complete"]=true;
        report["polish_checks_complete"]=true;
    }
}
