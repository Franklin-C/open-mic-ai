using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LocalFlow {
public static class PolishLayout {
    public static readonly string[] Names={"prose","paragraphs","list","goals","steps","recipe","sections","email","table","blog","essay","notes","checklist","outline","proposal","agenda","faq","social","script"};
    public static readonly string[] Labels={"Short message","Paragraphs","Bullet list","Goals","Instructions","Recipe","Report","Email","Table","Blog post","Essay","Meeting notes","Checklist","Outline","Proposal","Agenda","FAQ","Social post","Script"};
    public static string Explicit(string source){
        Match request=Regex.Match(source??"",@"^\s*(?:please (?:format (?:this|it) as |make (?:this|it) (?:a |an )?))?(comparison table|table|blog post|essay|meeting notes|travel checklist|checklist|outline|proposal|agenda|faq|social post|script)(?=\s*(?:[:.]|\b(?:for|about|of|on)\b))",RegexOptions.IgnoreCase);
        if(!request.Success)return null;string name=request.Groups[1].Value.ToLowerInvariant();
        return name.Contains("table")?"table":name.Contains("checklist")?"checklist":name=="blog post"?"blog":name=="meeting notes"?"notes":name=="social post"?"social":name;
    }
    public static string[] Eligible(string source){return (string[])Names.Clone();}
    public const string Planner="Choose a layout from the purpose and structure of the complete source. Return only JSON with layout. Do not edit, answer, summarize or add information. All 19 layouts are available automatically: no exact category name or formatting command is required. Ignore fillers and false starts when deciding the purpose. prose: a short message, continuous thought, question, opinion or brief story. Mentioning several things does not make a list. paragraphs: a longer narrative or explanation with distinct ideas, without a more specific document purpose. list: independent shopping or packing items, requirements or things needed. Retain surrounding context. goals: intended outcomes or personal/work targets, not steps in a procedure. steps: sequential instructions to perform an operation. recipe: cooking preparation, with ingredients, quantities, timing or cooking actions. sections: a status update or report with distinct topics such as progress, blockers and next actions. email: a message with a clear addressed greeting or sign-off, or explicitly intended as an email. A question or request involving people's names is not an email. table: compare two or more records with the same attributes, prices, quantities or measurements. Do not turn an ordinary list, story or instructions into a table. blog: material for a website article or personal blog, organized around an experience or topic. essay: a formal argument, thesis, supporting reasons and qualifications, especially for school or an assignment. notes: a record of a completed discussion, meeting decisions, unresolved issues and owners/action items. checklist: tasks explicitly intended to be checked off, completed before an event, or verified as ready. outline: main topics with their subordinate points, a hierarchy to develop later. proposal: a proposed solution with a problem, approach, scope, costs or requested approval. agenda: topics for an upcoming meeting, in discussion order; not notes from a completed meeting. faq: multiple questions paired with supplied answers, even when called questions and answers or spoken as number one and number two. An isolated unanswered question is prose. Never invent answers. social: a post or caption explicitly intended for a named social network, keeping only the supplied content. A direct request to another person is prose. script: dialogue or narration with stated speakers or scene directions. Choose the most specific supported purpose. Do not assume a blog, essay or social post merely because a message is long. Do not expand the chosen genre into new content. Prefer prose for short ordinary messages and paragraphs for long ordinary explanations. Explicit source intent takes precedence.";
    public static readonly string[][] Examples={
        new[]{"Can you ask Jo Rae and Sam whether they can meet on Tuesday?","prose"},
        new[]{"I need you to call Rowan when you get home because their phone is broken.","prose"},
        new[]{"Could you call Sam before Tuesday? Please don't send the old document.","prose"},
        new[]{"Rae asks: Is the microphone ready? Noor replies: Yes, but wait until I close the door. Rae says: I'll start after that.","script"},
        new[]{"Quick update: the website is ready but the export fails. Tomorrow Jo will fix the export and Sam will check the design.","sections"},
        new[]{"To install it, connect the cable, install the driver, then print a test page.","steps"},
        new[]{"I enjoyed the apples bananas and pears we ate after our walk.","prose"},
        new[]{"I am changing my morning routine. Checking emails when I wake makes me feel rushed. Now I walk and eat breakfast first. The walk helps me decide what matters. I still check urgent messages before leaving. I will try this for two weeks.","paragraphs"},
        new[]{"Here are questions and answers. Number one, what colour is the sky? The answer is blue. Number two, what color is water? The answer is blue.","faq"},
        new[]{"What colour is the sky? Could you explain it to me?","prose"},
        new[]{"Compare these plans by price and storage. Basic costs five dollars and has ten gigabytes. Pro costs twelve dollars and has one hundred gigabytes.","table"},
        new[]{"At our meeting we discussed the export issue. We decided to keep the date. Maya will investigate on Friday. Parking is still unresolved.","notes"},
        new[]{"For tomorrow's meeting discuss the export first, then review the design, finally decide who will test it.","agenda"},
        new[]{"Before travelling I need boxes to tick off: pack my passport, charge the camera and check the train time.","checklist"},
        new[]{"For this month I aim to walk each morning, read two books and save fifty dollars.","goals"},
        new[]{"For dinner rinse the rice, add water, boil it and simmer for fifteen minutes with the lid on.","recipe"},
        new[]{"For my class I argue that the library should open later. Working students need quiet evening study. Staffing costs need review. A one month trial would give us evidence.","essay"},
    };
    public static string Instruction(string layout){
        switch(layout){
            case "proposal":return "Separate the supplied problem, proposed approach, scope and next actions into readable sections. Preserve any stated costs, dates and limitations. Do not invent benefits, commitments, metrics, budgets or approval. My words uses existing topic labels only, otherwise paragraph breaks.";
            case "agenda":return "Use a numbered agenda, one discussion topic per line, in the stated order. Keep the meeting purpose, timing, owners and any requested preparation. Do not invent time allocations, attendees or outcomes.";
            case "faq":return "Put each supplied question and its supplied answer together, separated by blank lines between pairs. Keep questions as questions. Do not answer unanswered questions or invent missing answers. My words needs no added Q or A labels.";
            case "social":return "Make the supplied social post readable with short paragraphs. Preserve the voice, details and any supplied call to action. Do not add hashtags, emojis, claims, links or calls to action that were not supplied. Do not force a heading or expand a short post.";
            case "script":return "Separate the stated speakers, dialogue or narration into readable lines. Keep stage directions only if supplied. Do not invent scenes, speakers, dialogue or actions. My words may use existing speaker labels without duplication.";
            case "table":return "Use a Markdown pipe table with a separator row. Put each comparable record on its own row and each attribute in a column. Preserve introductions and closing text. Use only supplied data; leave missing cells blank. For My words, use provided column labels in their original order, or blank header cells if labels are absent; preserve clause wording and attribute order inside cells. Never invent labels or repeat source words. For Reworded, concise column labels are allowed but new values, assumptions and calculations are not.";
            case "blog":return "Organize the provided material as a blog post with a clear opening, readable paragraphs and a closing based on the source. Use subheadings only for distinct topics. My words keeps the original wording and uses only existing heading words. Reworded may improve transitions and phrasing without inventing experiences, advice or facts. Do not pad the post or manufacture a conclusion.";
            case "essay":return "Organize the supplied argument into an introduction, body paragraphs and a conclusion where the source supports one. Preserve the thesis, evidence, qualifications and counterarguments. Do not invent research, citations or examples. My words uses the original words and paragraph breaks; Reworded may improve coherence without expanding the claims.";
            case "notes":return "Use readable meeting notes: separate discussion, decisions and action items when present. Keep all stated people, owners, dates, qualifications and unresolved questions. Do not invent attendees, decisions or deadlines. My words uses only existing topic labels, otherwise paragraphs and bullets.";
            case "checklist":return "Put each actionable task on a separate '- [ ] ' checklist line. Keep context, due dates, quantities and conditions. Do not mark tasks completed unless the source explicitly says they are complete.";
            case "outline":return "Use an outline with indented '- ' bullets for main points and subpoints. Preserve the hierarchy described in the source. Use only existing topic words as headings in My words. Do not invent supporting details.";
            case "list":case "goals":return "Use '- ' bullet lines for each independent item, goal or task. Keep the introductory and closing prose, quantities and conditions. Do not leave the items embedded in one paragraph.";
            case "recipe":return "Use numbered cooking steps, one primary action per line. Chopping, adding ingredients, simmering, blending and tasting are separate steps, even if dictated in one sentence. Do not compress a whole recipe into one or two long steps. Keep quantities, temperatures, timing, conditions and warnings with their action. Keep an introduction and separately supplied ingredients if present. Do not invent ingredients, measurements, durations or steps.";
            case "steps":return "Use a numbered procedure: '1. ', '2. ', etc. Put each sequential action on its own line. Preserve the introduction and any warnings, timing or conditions. Do not invent actions. Do not leave the procedure embedded in one paragraph.";
            case "sections":return "Use separate sections for distinct topics and '- ' bullets for independent issues or actions. A report or status update is not a numbered procedure: do not number its facts. In My words, headings may use only existing source topic words; use paragraph breaks if no heading words are available.";
            case "email":return "Separate the greeting, message body and closing with blank lines. Keep only the supplied greeting and sign-off.";
            case "paragraphs":return "Split distinct ideas into readable paragraphs with blank lines. Use natural sentences. Preserve the narrative; do not turn it into bullets or summarize it.";
            default:return "Use natural sentences and paragraph breaks. Keep a short message or question simple; do not force a list or heading.";
        }
    }
    public static bool FitsSource(string layout,string source,string text){
        if(!Fits(layout,text))return false;
        if(layout=="recipe"){
            int actions=Regex.Matches(source??"",@"\b(?:wash|rinse|chop|put|add|bring|boil|reduce|simmer|leave|cook|drain|stir|blend|taste|bake|mix|heat|pour|serve)\b",RegexOptions.IgnoreCase).Count;
            if(actions>=4&&Regex.Matches(text??"",@"(?m)^\s*\d+[.)]\s").Count<(actions+1)/2)return false;
        }
        return true;
    }
    public static bool Fits(string layout,string text){
        if(layout=="prose"||layout=="paragraphs"){
            if(Regex.IsMatch(text??"",@"(?m)^[ \t]*(?:[-*•]\s+|\d+[.)]\s+|\|.*\||#{1,6}\s+|\*\*)"))return false;
            return layout=="prose"||Regex.IsMatch(text,@"\r?\n\s*\r?\n");
        }
        if(layout=="table")return Regex.Matches(text,@"(?m)^\s*\|[^\r\n]+\|\s*$").Count>=3&&Regex.IsMatch(text,@"(?m)^\s*\|[ :\-|]+\|\s*$");
        if(layout=="checklist")return Regex.IsMatch(text,@"(?m)^\s*- \[[ xX]\] \S");
        if(layout=="outline")return Regex.IsMatch(text,@"(?m)^\s*- \S");
        if(layout=="blog"||layout=="essay"||layout=="notes")return Regex.IsMatch(text,@"\r?\n\s*\r?\n");
        if(layout=="list"||layout=="goals")return Regex.Matches(text,@"(?m)^\s*-\s+\S").Count>=1;
        if(layout=="steps"||layout=="recipe"||layout=="agenda")return Regex.Matches(text,@"(?m)^\s*\d+[.)]\s+\S").Count>=1;
        if(layout=="sections"&&Regex.IsMatch(text,@"(?m)^\s*\d+[.)]\s+"))return false;
        if(layout=="script")return text.Contains("\n");
        if(layout=="sections"||layout=="email"||layout=="paragraphs"||layout=="proposal"||layout=="faq")return Regex.IsMatch(text,@"\r?\n\s*\r?\n");
        return true;
    }
}
}
