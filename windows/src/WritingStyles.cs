using System;
using System.Collections.Generic;

namespace LocalFlow {
public static class StyleOptions {
    public static readonly string[] Names={"Raw transcript","Clean up","Polish","Custom"};
    public static string Selected(Settings value){return !value.Rewrite||value.Cleanup=="None"||value.Style=="Original transcript"?"Raw transcript":value.Style=="Custom"?"Custom":value.Cleanup=="Medium"||value.Style=="Smart formatting"?"Polish":"Clean up";}
    public static void Apply(Settings value,string preset){if(Array.IndexOf(Names,preset)<0)throw new ArgumentException("Choose a style.");value.Style=preset=="Raw transcript"?"Original transcript":preset=="Polish"?"Smart formatting":preset=="Custom"?"Custom":"Keep my wording";value.Cleanup=preset=="Raw transcript"?"None":preset=="Clean up"?"Light":"Medium";value.Rewrite=preset!="Raw transcript";value.AppStyles.Clear();}
}
public static class WritingStyles {
    public const string PreserveRewordedContext="Preserve all meaningful introductory and closing statements, intentions, requests, descriptions, reasons, conditions, qualifications, greetings, thanks and tone. Reword for clarity without adding claims, promises or details. Remove true fillers and resolve corrections to their final choice. A bare list or heading cannot replace the speaker's personal intention or feeling. Do not summarize away meaningful content.";
    public static string PolishInstruction(bool reworded){return reworded?"Polish the complete message for clarity and readability. "+PreserveRewordedContext+" Use prose for ordinary messages, narratives, opinions and questions. Use bullets for shopping lists, independent tasks or requirements, and numbered steps for a procedure. Use short headings for distinct topics in notes or plans; omit headings for simple sentences or a single list. Preserve meaningful prose before and after lists, including purpose, conditions, quantities and descriptions. Choose structure from context, not simply the presence of multiple nouns. Use real line breaks and plain text, with no commentary.":Instruction("Smart formatting");}
    public const string PreservePolishContext="Preserve the speaker's own words in their original order. Do not add, paraphrase, replace, repeat or expand words, even to improve grammar or tone. Keep contractions, names, numbers and quantities exactly as spoken. Preserve all meaningful introductory and closing statements, intentions, requests, descriptions, reasons, conditions, qualifications, greetings and thanks. Remove true fillers, stutters, explicit formatting directives and superseded corrections. Remove incidental 'all right' at the start and 'mm-hmm' acknowledgments. Resolve corrections: 'strawberries. Actually make that two strawberries' becomes 'two strawberries'; 'three bananas, sorry make that four bananas' becomes 'four bananas'. Do not leave correction cues or the discarded choice in the output. This deletion is required and does not count as rewriting. A bare list or a heading cannot replace an intention sentence. Formatting must preserve the full message, never summarize it.";
    public static readonly string[] Names={"Keep my wording","Smart formatting","Professional","Friendly","Casual","Concise","Email","Bullet points","Numbered list","Meeting notes","Custom","Original transcript"};
    public static string Normalize(string name){return name=="Clean"||String.IsNullOrWhiteSpace(name)?"Keep my wording":name;}
    public static string Description(string name) {
        switch(Normalize(name)) {
            case "Keep my wording":return "Keep your words and tone. Remove fillers, fix grammar and punctuation, follow corrections, and format your intent.";
            case "Smart formatting":return "AI rewriting and automatic formatting based on your intent.";
            case "Professional":return "Polished business wording with your facts and intent.";
            case "Friendly":return "Warm, natural wording without adding promises or facts.";
            case "Casual":return "Relaxed, conversational wording. Keep your meaning.";
            case "Concise":return "Shorter and more direct, while keeping the important details.";
            case "Email":return "Format as an email. Keep dictated greetings and sign-offs; do not invent recipients, names or a signature.";
            case "Bullet points":return "Put the dictated items or tasks in a plain text bullet list.";
            case "Numbered list":return "Put the dictated items or steps in a numbered list.";
            case "Meeting notes":return "Structure dictated notes by topic. Keep decisions and tasks; do not invent owners or deadlines.";
            case "Custom":return "Use your own writing instructions below. Spoken instructions can also adjust the result.";
            case "Original transcript":return "Paste the speech model's transcript directly. No AI cleanup or formatting.";
            default:return "Keep your wording and intent.";
        }
    }
    public static string Instruction(string name) {
        switch(Normalize(name)) {
            case "Professional":return "Rewrite in polished business language with a polite greeting. Use complete sentences and full words instead of informal shorthand.";
            case "Friendly":return "Use a warm, friendly, natural tone. Do not add greetings or sentiments the speaker did not express.";
            case "Casual":return "Use relaxed conversational language. Do not add slang, emojis or extra claims unless requested.";
            case "Concise":return "Be concise while keeping every important detail.";
            case "Email":return "Format this as a readable email with paragraph breaks. Use only the dictated greeting and sign-off, if any. Do not add a subject, signature or placeholders unless requested.";
            case "Bullet points":return "Use a plain text bullet list.";
            case "Numbered list":return "Use a plain text numbered list, starting at 1.";
            case "Meeting notes":return "Organize as concise meeting notes, using headings and bullets where appropriate. Preserve decisions and action items. Do not invent attendees, owners or dates.";
            case "Smart formatting":return "Format the complete dictation without rewriting it. "+PreservePolishContext+" Change only punctuation, capitalization, spacing and layout. PROSE: use sentences or paragraphs for ordinary messages, opinions, narratives and questions. Several names or nouns alone do not imply a list. LISTS: use '- ' bullets for shopping or packing needs, separate tasks, requirements or an explicit list request. Separate each item, keeping the original introductory sentence and all descriptions, quantities and conditions. A list's connecting 'and' may be removed, but do not remove meaningful words inside an item. ORDERED STEPS: number procedures with '1. ', '2. ', etc.; preserve the original step wording and qualifiers. HEADINGS: isolate existing topic words as short headings for distinct topics in notes or plans. Use only words already in the source, in the same order and without duplication. Never invent labels such as 'Overview' or 'Next steps'. If there are no suitable topic words, use paragraph breaks instead. MIXED CONTENT: combine prose and lists naturally. Keep introductory and closing sentences around lists. Use real line breaks and blank lines between sections. Do not force a heading onto a simple message or a single list. Keep written number words as words; do not change 'two' to '2'.";
            default:return "Stay as close as possible to the original words and tone. Fix fillers, grammar, capitalization and punctuation. Do not summarize or replace words with synonyms unless needed for grammar or explicitly requested. Keep existing paragraph breaks. Do not add headings or turn prose into lists unless explicitly requested.";
        }
    }
    public static bool CheckMeaning(string name) {
        name=Normalize(name);
        return name=="Professional"||name=="Friendly"||name=="Casual"||name=="Email"||name=="Meeting notes"||name=="Smart formatting"||name=="Custom";
    }
}
}
