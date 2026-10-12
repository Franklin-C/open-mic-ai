import Foundation

public enum VoiceCommand {
    public static func parse(_ command: String, language: String) -> (action: String, language: String) {
        let start = command.trimmingCharacters(in: .whitespacesAndNewlines).replacingOccurrences(of: #"^(?:(?:please|can you|could you|would you|I want you to)\s+)+"#, with: "", options: [.regularExpression, .caseInsensitive])
        func matches(_ pattern: String) -> Bool { start.range(of: pattern, options: [.regularExpression, .caseInsensitive]) != nil }
        if matches(#"^(?:translate|translation|give me (?:a |the )?translation)\b"#) {
            let expression = try! NSRegularExpression(pattern: #"\b(?:into|to|in)\s+([\p{L}][\p{L}\p{M} ()-]*?)(?:[,.!?;]|\s+(?:and|using|with|as|but|please)\b|$)"#, options: .caseInsensitive)
            let match = expression.firstMatch(in: start, range: NSRange(start.startIndex..., in: start))
            let selected = match.map { (start as NSString).substring(with: $0.range(at: 1)).trimmingCharacters(in: .whitespacesAndNewlines) } ?? language
            return ("Translate", selected)
        }
        if matches(#"^(?:summari[sz]e|summary|give me (?:a |the )?(?:short |brief |quick )?summary|make (?:this|it) (?:a |into a )?summary)\b"#) { return ("Summarize", language) }
        if matches(#"^(?:rewrite|reword|rephrase|edit|polish|format|improve|shorten|lengthen|simplify|fix|correct|change|turn|make|add|append|remove|replace|convert)\b"#) { return ("Rewrite", language) }
        return ("Dictate", language)
    }
}
