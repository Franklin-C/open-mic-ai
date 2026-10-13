import Foundation

public enum TableFormatting {
    private struct Plan: Decodable { var before: String; var headers: [String]; let rows: [[String]]; var after: String }
    public static func render(_ json: String, nameEmptyHeaders: Bool = false) -> String? {
        guard var plan = try? JSONDecoder().decode(Plan.self, from: Data(json.utf8)), (2...12).contains(plan.headers.count), !plan.rows.isEmpty, plan.rows.allSatisfy({ $0.count == plan.headers.count }), (plan.headers + plan.rows.flatMap { $0 }).allSatisfy({ !$0.contains("|") && !$0.contains("\n") && !$0.contains("\r") }) else { return nil }
        let expression = try! NSRegularExpression(pattern: #"[\p{L}\p{M}\p{N}_]+(?:['’][\p{L}\p{M}\p{N}_]+)*"#)
        let body = WordGuard.words((plan.headers + plan.rows.flatMap { $0 }).joined(separator: " "))
        let prefix = expression.matches(in: plan.before, range: NSRange(plan.before.startIndex..., in: plan.before))
        let words = WordGuard.words(plan.before)
        if !body.isEmpty && prefix.count >= body.count {
            for start in 0...(prefix.count-body.count) where Array(words[start..<(start+body.count)]) == body {
                let closing = (plan.before as NSString).substring(from: NSMaxRange(prefix[start+body.count-1].range)).trimmingCharacters(in: CharacterSet(charactersIn: " .,:;\r\n"))
                plan.before = (plan.before as NSString).substring(to: prefix[start].range.location).trimmingCharacters(in: .whitespacesAndNewlines)
                if !closing.isEmpty && closing.caseInsensitiveCompare(plan.after.trimmingCharacters(in: .whitespacesAndNewlines)) != .orderedSame { plan.after = [closing, plan.after].filter { !$0.isEmpty }.joined(separator: "\n\n") }
                break
            }
        }
        if nameEmptyHeaders { for i in plan.headers.indices where plan.headers[i].trimmingCharacters(in: .whitespaces).isEmpty { plan.headers[i] = i == 0 ? "Name" : "Value \(i+1)" } }
        func row(_ cells: [String]) -> String { "| " + cells.map { $0.trimmingCharacters(in: .whitespaces) }.joined(separator: " | ") + " |" }
        var output = [String]()
        if !plan.before.isEmpty { output.append(plan.before + "\n") }
        output.append(row(plan.headers)); output.append(row(plan.headers.map { _ in "---" })); output += plan.rows.map(row)
        if !plan.after.isEmpty { output.append("\n" + plan.after) }
        return output.joined(separator: "\n")
    }
    private static func escape(_ text: String) -> String { text.replacingOccurrences(of: "&", with: "&amp;").replacingOccurrences(of: "<", with: "&lt;").replacingOccurrences(of: ">", with: "&gt;").replacingOccurrences(of: "\"", with: "&quot;") }
    public static func html(_ text: String) -> String? {
        let lines = text.replacingOccurrences(of: "\r", with: "").components(separatedBy: "\n")
        func cells(_ line: String) -> [String]? { let value = line.trimmingCharacters(in: .whitespaces); guard value.hasPrefix("|"), value.hasSuffix("|"), value.count >= 2 else { return nil }; return String(value.dropFirst().dropLast()).components(separatedBy: "|").map { $0.trimmingCharacters(in: .whitespaces) } }
        func row(_ values: [String], _ header: Bool) -> String { let tag = header ? "th" : "td"; return "<tr>" + values.map { "<\(tag) style=\"border:1px solid #cccccc;padding:6px;text-align:left\">\(escape($0))</\(tag)>" }.joined() + "</tr>" }
        var html = "", found = false, i = 0
        while i < lines.count {
            if let header = cells(lines[i]), header.count >= 2, i+1 < lines.count, let separator = cells(lines[i+1]), separator.count == header.count, separator.allSatisfy({ $0.range(of: #"^:?-{3,}:?$"#, options: .regularExpression) != nil }) {
                var rows = [[String]](), next = i+2
                while next < lines.count, let values = cells(lines[next]) { guard values.count == header.count else { return nil }; rows.append(values); next += 1 }
                guard !rows.isEmpty else { return nil }
                html += "<table style=\"border-collapse:collapse\"><thead>" + row(header, true) + "</thead><tbody>" + rows.map { row($0, false) }.joined() + "</tbody></table>"; found = true; i = next
            } else { if !lines[i].trimmingCharacters(in: .whitespaces).isEmpty { html += "<p>\(escape(lines[i]))</p>" }; i += 1 }
        }
        return found ? html : nil
    }
}
