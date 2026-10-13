import Foundation

public struct PersonalizedText {
    public let text: String
    private let inserts: [String: String]
    private let vocabulary: [VocabularyWord]
    private let literal: Bool
    private static func phrase(_ value: String) -> String {
        let words = value.split(whereSeparator: { $0.isWhitespace }).map { NSRegularExpression.escapedPattern(for: String($0)) }
        return #"(?<![\p{L}\p{N}_])"# + words.joined(separator: #"\s+"#) + #"(?![\p{L}\p{N}_])"#
    }
    private static func correct(_ text: String, _ words: [VocabularyWord]) -> String {
        words.filter { !$0.heard.isEmpty }.sorted { $0.heard.count > $1.heard.count }.reduce(text) { value, word in
            value.replacingOccurrences(of: phrase(word.heard), with: NSRegularExpression.escapedTemplate(for: word.written), options: [.regularExpression, .caseInsensitive])
        }
    }
    public init(_ source: String, vocabulary: [VocabularyWord], snippets: [Snippet]) {
        self.vocabulary = vocabulary
        literal = source.range(of: #"\b(literal|literally|quotation marks|quoted words)\b"#, options: [.regularExpression, .caseInsensitive]) != nil
        if literal { text = source; inserts = [:]; return }
        var value = source, replacements: [String: String] = [:]
        let usable = snippets.filter { !$0.trigger.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }.sorted { $0.trigger.count > $1.trigger.count }
        if !usable.isEmpty {
            let expression = try! NSRegularExpression(pattern: usable.map { "(?:" + Self.phrase($0.trigger) + ")" }.joined(separator: "|"), options: .caseInsensitive)
            for match in expression.matches(in: source, range: NSRange(source.startIndex..., in: source)).reversed() {
                let trigger = (source as NSString).substring(with: match.range)
                guard let snippet = usable.first(where: { trigger.range(of: "^" + Self.phrase($0.trigger) + "$", options: [.regularExpression, .caseInsensitive]) != nil }), let range = Range(match.range, in: value) else { continue }
                let marker = "LF_SNIP_" + UUID().uuidString.replacingOccurrences(of: "-", with: "")
                replacements[marker] = snippet.text; value.replaceSubrange(range, with: marker)
            }
        }
        text = Self.correct(value, vocabulary); inserts = replacements
    }
    public func restore(_ result: String) -> String {
        var value = literal ? result : Self.correct(result, vocabulary)
        if inserts.keys.contains(where: { marker in
            let expression = try! NSRegularExpression(pattern: NSRegularExpression.escapedPattern(for: marker), options: .caseInsensitive)
            return expression.numberOfMatches(in: value, range: NSRange(value.startIndex..., in: value)) != 1
        }) { value = text }
        for (marker, content) in inserts { value = value.replacingOccurrences(of: marker, with: content, options: .caseInsensitive) }
        return value
    }
}
