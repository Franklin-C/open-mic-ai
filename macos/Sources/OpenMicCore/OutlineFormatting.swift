import Foundation

public enum OutlineFormatting {
    private struct Group: Decodable { let topic: String; let items: [String] }
    private struct Document: Decodable { var before: String; let groups: [Group]; let after: String }
    public static func render(_ json: String) -> String? {
        guard var document = try? JSONDecoder().decode(Document.self, from: Data(json.utf8)), !document.groups.isEmpty, document.groups.count <= 40 else { return nil }
        func line(_ value: String) -> Bool { !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && value.rangeOfCharacter(from: .newlines) == nil }
        guard document.groups.allSatisfy({ line($0.topic) && !$0.items.isEmpty && $0.items.allSatisfy(line) }) else { return nil }
        // Remove only source topic clauses repeated by their matching group.
        func phrase(_ value: String) -> String {
            let trimmed = value.replacingOccurrences(of: "^and\\s+", with: "", options: [.regularExpression, .caseInsensitive])
            return WordGuard.words(trimmed).map { NSRegularExpression.escapedPattern(for: $0) }.joined(separator: "[\\s,.:;]+(?:(?:the|a|an)\\s+)?")
        }
        for group in document.groups {
            let topic = group.topic.trimmingCharacters(in: .whitespaces).replacingOccurrences(of: "^(?:under\\s+)|\\s+(?:include|cover|put)$", with: "", options: [.regularExpression, .caseInsensitive])
            let pattern = "\\b(?:under|first topic|second topic|final topic)\\s+" + phrase(topic) + "\\s+(?:include|cover|put|with subpoints?)\\s+(?:(?:the|a|an)\\s+)?" + group.items.map(phrase).joined(separator: "[\\s,.;]*(?:and\\s+)?(?:(?:the|a|an)\\s+)?") + "[.]?"
            document.before = document.before.replacingOccurrences(of: pattern, with: "", options: [.regularExpression, .caseInsensitive])
        }
        document.before = document.before.replacingOccurrences(of: "[ \\t]{2,}", with: " ", options: .regularExpression)
        let body = document.groups.map { "- " + $0.topic.trimmingCharacters(in: .whitespaces) + "\n" + $0.items.map { "  - " + $0.trimmingCharacters(in: .whitespaces) }.joined(separator: "\n") }.joined(separator: "\n")
        return [document.before, body, document.after].map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }.filter { !$0.isEmpty }.joined(separator: "\n\n")
    }
}
