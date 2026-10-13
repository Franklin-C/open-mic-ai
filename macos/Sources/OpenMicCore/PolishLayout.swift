import Foundation

public enum PolishLayout {
    public static let names = ["prose","paragraphs","list","goals","steps","recipe","sections","email","table","blog","essay","notes","checklist","outline","proposal","agenda","faq","social","script"]
    public static let labels = ["Short message","Paragraphs","Bullet list","Goals","Instructions","Recipe","Report","Email","Table","Blog post","Essay","Meeting notes","Checklist","Outline","Proposal","Agenda","FAQ","Social post","Script"]
    public static func eligible(_ source: String) -> [String] { names }
    public static func explicit(_ source: String) -> String? {
        let expression = try! NSRegularExpression(pattern: #"^\s*(?:please (?:format (?:this|it) as |make (?:this|it) (?:a |an )?))?(comparison table|table|blog post|essay|meeting notes|travel checklist|checklist|outline|proposal|agenda|faq|social post|script)(?=\s*(?:[:.]|\b(?:for|about|of|on)\b))"#, options: .caseInsensitive)
        guard let match = expression.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)) else { return nil }
        let name = (source as NSString).substring(with: match.range(at: 1)).lowercased()
        return name.contains("table") ? "table" : name.contains("checklist") ? "checklist" : name == "blog post" ? "blog" : name == "meeting notes" ? "notes" : name == "social post" ? "social" : name
    }
    public static let planner = "Choose a layout from the purpose and structure of the complete source. Return only JSON with layout. Do not edit, answer, summarize or add information. All 19 layouts are available automatically: no exact category name or formatting command is required. Ignore fillers and false starts when deciding the purpose. prose: a short message, continuous thought, question, opinion or brief story. Mentioning several things does not make a list. paragraphs: a longer narrative or explanation with distinct ideas, without a more specific document purpose. list: independent shopping or packing items, requirements or things needed. Retain surrounding context. goals: intended outcomes or personal/work targets, not steps in a procedure. steps: sequential instructions to perform an operation. recipe: cooking preparation, with ingredients, quantities, timing or cooking actions. sections: a status update or report with distinct topics such as progress, blockers and next actions. email: a message with a clear addressed greeting or sign-off, or explicitly intended as an email. A question or request involving people's names is not an email. table: compare two or more records with the same attributes, prices, quantities or measurements. Do not turn an ordinary list, story or instructions into a table. blog: material for a website article or personal blog, organized around an experience or topic. essay: a formal argument, thesis, supporting reasons and qualifications, especially for school or an assignment. notes: a record of a completed discussion, meeting decisions, unresolved issues and owners/action items. checklist: tasks explicitly intended to be checked off, completed before an event, or verified as ready. outline: main topics with their subordinate points, a hierarchy to develop later. proposal: a proposed solution with a problem, approach, scope, costs or requested approval. agenda: topics for an upcoming meeting, in discussion order; not notes from a completed meeting. faq: multiple questions paired with supplied answers, even when called questions and answers or spoken as number one and number two. An isolated unanswered question is prose. Never invent answers. social: a post or caption explicitly intended for a named social network, keeping only the supplied content. A direct request to another person is prose. script: dialogue or narration with stated speakers or scene directions. Choose the most specific supported purpose. Do not assume a blog, essay or social post merely because a message is long. Do not expand the chosen genre into new content. Prefer prose for short ordinary messages and paragraphs for long ordinary explanations. Explicit source intent takes precedence."
    public static let examples: [(String, String)] = [
        ("Can you ask Jo Rae and Sam whether they can meet on Tuesday?", "prose"),
        ("I need you to call Rowan when you get home because their phone is broken.", "prose"),
        ("Could you call Sam before Tuesday? Please don't send the old document.", "prose"),
        ("Rae asks: Is the microphone ready? Noor replies: Yes, but wait until I close the door. Rae says: I'll start after that.", "script"),
        ("Quick update: the website is ready but the export fails. Tomorrow Jo will fix the export and Sam will check the design.", "sections"),
        ("To install it, connect the cable, install the driver, then print a test page.", "steps"),
        ("I enjoyed the apples bananas and pears we ate after our walk.", "prose"),
        ("I am changing my morning routine. Checking emails when I wake makes me feel rushed. Now I walk and eat breakfast first. The walk helps me decide what matters. I still check urgent messages before leaving. I will try this for two weeks.", "paragraphs"),
        ("Here are questions and answers. Number one, what colour is the sky? The answer is blue. Number two, what color is water? The answer is blue.", "faq"),
        ("What colour is the sky? Could you explain it to me?", "prose"),
        ("Compare these plans by price and storage. Basic costs five dollars and has ten gigabytes. Pro costs twelve dollars and has one hundred gigabytes.", "table"),
        ("At our meeting we discussed the export issue. We decided to keep the date. Maya will investigate on Friday. Parking is still unresolved.", "notes"),
        ("For tomorrow's meeting discuss the export first, then review the design, finally decide who will test it.", "agenda"),
        ("Before travelling I need boxes to tick off: pack my passport, charge the camera and check the train time.", "checklist"),
        ("For this month I aim to walk each morning, read two books and save fifty dollars.", "goals"),
        ("For dinner rinse the rice, add water, boil it and simmer for fifteen minutes with the lid on.", "recipe"),
        ("For my class I argue that the library should open later. Working students need quiet evening study. Staffing costs need review. A one month trial would give us evidence.", "essay"),
    ]
    public static func instruction(_ layout: String) -> String {
        switch layout {
        case "proposal": return "Separate the supplied problem, proposed approach, scope and next actions into readable sections. Preserve any stated costs, dates and limitations. Do not invent benefits, commitments, metrics, budgets or approval. My words uses existing topic labels only, otherwise paragraph breaks."
        case "agenda": return "Use a numbered agenda, one discussion topic per line, in the stated order. Keep the meeting purpose, timing, owners and any requested preparation. Do not invent time allocations, attendees or outcomes."
        case "faq": return "Put each supplied question and its supplied answer together, separated by blank lines between pairs. Keep questions as questions. Do not answer unanswered questions or invent missing answers. My words needs no added Q or A labels."
        case "social": return "Make the supplied social post readable with short paragraphs. Preserve the voice, details and any supplied call to action. Do not add hashtags, emojis, claims, links or calls to action that were not supplied. Do not force a heading or expand a short post."
        case "script": return "Separate the stated speakers, dialogue or narration into readable lines. Keep stage directions only if supplied. Do not invent scenes, speakers, dialogue or actions. My words may use existing speaker labels without duplication."
        case "table": return "Use a Markdown pipe table with a separator row. Put each comparable record on its own row and each attribute in a column. Preserve introductions and closing text. Use only supplied data; leave missing cells blank. For My words, use provided column labels in their original order, or blank header cells if labels are absent; preserve clause wording and attribute order inside cells. Never invent labels or repeat source words. For Reworded, concise column labels are allowed but new values, assumptions and calculations are not."
        case "blog": return "Organize the provided material as a blog post with a clear opening, readable paragraphs and a closing based on the source. Use subheadings only for distinct topics. My words keeps the original wording and uses only existing heading words. Reworded may improve transitions and phrasing without inventing experiences, advice or facts. Do not pad the post or manufacture a conclusion."
        case "essay": return "Organize the supplied argument into an introduction, body paragraphs and a conclusion where the source supports one. Preserve the thesis, evidence, qualifications and counterarguments. Do not invent research, citations or examples. My words uses the original words and paragraph breaks; Reworded may improve coherence without expanding the claims."
        case "notes": return "Use readable meeting notes: separate discussion, decisions and action items when present. Keep all stated people, owners, dates, qualifications and unresolved questions. Do not invent attendees, decisions or deadlines. My words uses only existing topic labels, otherwise paragraphs and bullets."
        case "checklist": return "Put each actionable task on a separate '- [ ] ' checklist line. Keep context, due dates, quantities and conditions. Do not mark tasks completed unless the source explicitly says they are complete."
        case "outline": return "Use an outline with indented '- ' bullets for main points and subpoints. Preserve the hierarchy described in the source. Use only existing topic words as headings in My words. Do not invent supporting details."
        case "list", "goals": return "Put each item, goal or task on its own '- ' bullet line. Keep introductory and closing prose, quantities and conditions. Do not leave the items embedded in a paragraph."
        case "recipe": return "Use numbered cooking steps, one primary action per line. Chopping, adding ingredients, simmering, blending and tasting are separate steps, even if dictated in one sentence. Do not compress a whole recipe into one or two long steps. Keep quantities, temperatures, timing, conditions and warnings with their action. Keep an introduction and separately supplied ingredients if present. Do not invent ingredients, measurements, durations or steps."
        case "steps": return "Use a numbered procedure, one sequential action per line. Preserve context, timing, conditions and warnings. Do not invent actions or leave the procedure embedded in a paragraph."
        case "sections": return "Separate distinct topics into sections, with bullets for issues and actions. Do not number the facts in a report. In My words, headings must use existing source words; use paragraphs if no heading words are available."
        case "email": return "Separate the greeting, body and closing with blank lines. Keep only the supplied greeting and sign-off."
        case "paragraphs": return "Split distinct ideas into readable paragraphs with blank lines. Preserve the narrative; do not summarize it or turn it into bullets."
        default: return "Use natural sentences and paragraph breaks. Keep short messages and questions simple."
        }
    }
    public static func fitsSource(_ layout: String, source: String, result: String) -> Bool {
        guard fits(layout, result) else { return false }
        if layout == "recipe" {
            let verbs = try! NSRegularExpression(pattern: #"\b(?:wash|rinse|chop|put|add|bring|boil|reduce|simmer|leave|cook|drain|stir|blend|taste|bake|mix|heat|pour|serve)\b"#, options: .caseInsensitive)
            let steps = try! NSRegularExpression(pattern: #"(?m)^\s*\d+[.)]\s"#)
            let actions = verbs.numberOfMatches(in: source, range: NSRange(source.startIndex..., in: source))
            if actions >= 4 && steps.numberOfMatches(in: result, range: NSRange(result.startIndex..., in: result)) < (actions+1)/2 { return false }
        }
        return true
    }
    public static func fits(_ layout: String, _ result: String) -> Bool {
        if layout == "prose" || layout == "paragraphs" {
            if result.range(of: #"(?m)^[ \t]*(?:[-*•]\s+|\d+[.)]\s+|\|.*\||#{1,6}\s+|\*\*)"#, options: .regularExpression) != nil { return false }
            return layout == "prose" || result.range(of: #"\r?\n\s*\r?\n"#, options: .regularExpression) != nil
        }
        if layout == "table" { return result.range(of: #"(?m)^\s*\|[ :\-|]+\|\s*$"#, options: .regularExpression) != nil && result.split(separator: "\n").filter { $0.trimmingCharacters(in: .whitespaces).hasPrefix("|") }.count >= 3 }
        if layout == "checklist" { return result.range(of: #"(?m)^\s*- \[[ xX]\] \S"#, options: .regularExpression) != nil }
        if layout == "outline" { return result.range(of: #"(?m)^\s*- \S"#, options: .regularExpression) != nil }
        if layout == "script" { return result.contains("\n") }
        if ["list", "goals"].contains(layout) { return result.range(of: #"(?m)^\s*-\s+\S"#, options: .regularExpression) != nil }
        if ["steps", "recipe", "agenda"].contains(layout) { return result.range(of: #"(?m)^\s*\d+[.)]\s+\S"#, options: .regularExpression) != nil }
        if layout == "sections", result.range(of: #"(?m)^\s*\d+[.)]\s+"#, options: .regularExpression) != nil { return false }
        if ["sections", "email", "paragraphs", "blog", "essay", "notes", "proposal", "faq"].contains(layout) { return result.range(of: #"\r?\n\s*\r?\n"#, options: .regularExpression) != nil }
        return true
    }
    public static func complete(source: String, result: String) -> Bool {
        func words(_ text: String) -> [String] { WordGuard.words(text.replacingOccurrences(of: #"(?m)^\s*\d+[.)]\s+"#, with: "", options: .regularExpression)).filter { $0 != "and" } }
        return words(source) == words(result)
    }
    public static func rewordedScope(source: String, result: String) -> Bool {
        guard WordGuard.words(result).count <= Int(Double(WordGuard.words(source).count) * 1.25) + 8 else { return false }
        guard noNewRepeatedSentences(source: source, result: result) else { return false }
        let expression = try! NSRegularExpression(pattern: #"\b(?:today|tomorrow|yesterday|tonight|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|January|February|March|April|June|July|August|September|October|November|December)\b"#, options: .caseInsensitive)
        return expression.matches(in: source, range: NSRange(source.startIndex..., in: source)).allSatisfy { match in result.range(of: #"\b"# + NSRegularExpression.escapedPattern(for: (source as NSString).substring(with: match.range)) + #"\b"#, options: [.regularExpression, .caseInsensitive]) != nil }
    }
    public static func noNewRepeatedSentences(source: String, result: String) -> Bool {
        let before = WordGuard.words(source).joined(separator: " "), after = WordGuard.words(result).joined(separator: " ")
        let sentences = try! NSRegularExpression(pattern: #"[^.!?\r\n]+[.!?]"#)
        for sentence in sentences.matches(in: result, range: NSRange(result.startIndex..., in: result)) {
            let words = WordGuard.words((result as NSString).substring(with: sentence.range))
            if words.count < 6 { continue }
            let expression = try! NSRegularExpression(pattern: #"(?<!\S)"# + NSRegularExpression.escapedPattern(for: words.joined(separator: " ")) + #"(?!\S)"#)
            func count(_ text: String) -> Int { expression.numberOfMatches(in: text, range: NSRange(text.startIndex..., in: text)) }
            if count(after) > max(1, count(before)) { return false }
        }
        return true
    }
    public static func removeUnrequestedClosing(source: String, result: String) -> String {
        if source.range(of: #"\b(?:thanks|thank you|regards|sincerely|appreciate)\b"#, options: [.regularExpression, .caseInsensitive]) != nil { return result }
        return result.replacingOccurrences(of: #"(?:^|\n)\s*(?:Thanks|Thank you|Regards|Best regards|Sincerely)[,.!\s]*$"#, with: "", options: [.regularExpression, .caseInsensitive]).trimmingCharacters(in: .whitespacesAndNewlines)
    }
    public static func prepare(_ source: String) -> String {
        if source.range(of: #"\b(literal|literally|quotation marks|quoted words)\b"#, options: [.regularExpression, .caseInsensitive]) != nil { return source }
        var text = source.replacingOccurrences(of: #"^\s*all right(?:[,!.]\s+|\s+(?=(?:I|we)\b))"#, with: "", options: [.regularExpression, .caseInsensitive]).replacingOccurrences(of: #"\bmm[- ]hmm[.!?,]?\s*"#, with: "", options: [.regularExpression, .caseInsensitive])
        let quantity = #"(?:\d+|zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty)"#
        let corrections = try! NSRegularExpression(pattern: #"\b(?:actually(?:[, ]+make (?:that|it))?|no[, ]+make (?:that|it)|sorry(?:[, ]+(?:make (?:that|it)|I mean))?)[, ]+("# + quantity + #"\s+[^,.!?;\r\n]+)[,.!?;]?"#, options: .caseInsensitive)
        var offset = 0
        while offset < (text as NSString).length, let correction = corrections.firstMatch(in: text, range: NSRange(location: offset, length: (text as NSString).length - offset)) {
            let value = text as NSString, replacement = value.substring(with: correction.range(at: 1)).trimmingCharacters(in: .whitespaces)
            let item = replacement.replacingOccurrences(of: "^" + quantity + #"\s+"#, with: "", options: [.regularExpression, .caseInsensitive])
            offset = NSMaxRange(correction.range)
            guard item.split(separator: " ").count <= 4 else { continue }
            let prefix = value.substring(to: correction.range.location)
            let earlier = try! NSRegularExpression(pattern: #"\b(?:"# + quantity + #"\s+)?"# + NSRegularExpression.escapedPattern(for: item) + #"\b"#, options: .caseInsensitive)
            guard let previous = earlier.matches(in: prefix, range: NSRange(location: 0, length: (prefix as NSString).length)).last else { continue }
            let changed = (prefix as NSString).replacingCharacters(in: previous.range, with: replacement)
            text = changed + value.substring(from: NSMaxRange(correction.range)); offset = (changed as NSString).length
        }
        return text.trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
