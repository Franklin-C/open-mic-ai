import Foundation

public enum FaqFormatting {
    private struct Pair: Decodable { let question: String; let answer: String }
    private struct Document: Decodable { var before: String; let pairs: [Pair]; let after: String }
    public static func render(_ json: String, pairsOnly: Bool = false) -> String? {
        guard var document = try? JSONDecoder().decode(Document.self, from: Data(json.utf8)), !document.pairs.isEmpty, document.pairs.count <= 40, document.pairs.allSatisfy({ !$0.question.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && $0.question.rangeOfCharacter(from: .newlines) == nil }) else { return nil }
        if pairsOnly { document.before = "" }
        let body = document.pairs.map { pair in
            let question = pair.question.trimmingCharacters(in: .whitespacesAndNewlines).trimmingCharacters(in: CharacterSet(charactersIn: ".?:")) + "?"
            return question + (pair.answer.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? "" : "\n" + pair.answer.trimmingCharacters(in: .whitespacesAndNewlines))
        }.joined(separator: "\n\n")
        let expression = try! NSRegularExpression(pattern: #"[\p{L}\p{M}\p{N}_]+(?:['’][\p{L}\p{M}\p{N}_]+)*"#)
        let words = WordGuard.words(body), prefix = expression.matches(in: document.before, range: NSRange(document.before.startIndex..., in: document.before))
        if !words.isEmpty && prefix.count >= words.count && WordGuard.words(document.before).suffix(words.count).elementsEqual(words) { document.before = (document.before as NSString).substring(to: prefix[prefix.count-words.count].range.location).trimmingCharacters(in: .whitespacesAndNewlines) }
        return [document.before, body, document.after].map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }.filter { !$0.isEmpty }.joined(separator: "\n\n")
    }
}
