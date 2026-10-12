import Foundation

public enum OutlineFormatting {
    private struct Group: Decodable { let topic: String; let items: [String] }
    private struct Document: Decodable { let before: String; let groups: [Group]; let after: String }
    public static func render(_ json: String) -> String? {
        guard let document = try? JSONDecoder().decode(Document.self, from: Data(json.utf8)), !document.groups.isEmpty, document.groups.count <= 40 else { return nil }
        func line(_ value: String) -> Bool { !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && value.rangeOfCharacter(from: .newlines) == nil }
        guard document.groups.allSatisfy({ line($0.topic) && !$0.items.isEmpty && $0.items.allSatisfy(line) }) else { return nil }
        let body = document.groups.map { "- " + $0.topic.trimmingCharacters(in: .whitespaces) + "\n" + $0.items.map { "  - " + $0.trimmingCharacters(in: .whitespaces) }.joined(separator: "\n") }.joined(separator: "\n")
        return [document.before, body, document.after].map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }.filter { !$0.isEmpty }.joined(separator: "\n\n")
    }
}
