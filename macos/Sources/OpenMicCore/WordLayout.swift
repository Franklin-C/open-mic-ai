import Foundation

public enum WordLayout {
    private static let word = try! NSRegularExpression(pattern: #"[\p{L}\p{M}\p{N}_]+(?:['\u2019][\p{L}\p{M}\p{N}_]+)*"#)
    private static func matches(_ pattern: String, _ text: String) -> Bool { text.range(of: pattern, options: .regularExpression) != nil }
    private static func key(_ text: String) -> String { let value = text.replacingOccurrences(of: "’", with: "'").lowercased(); if value.count > 5 && value.hasSuffix("ing") { return String(value.dropLast(3)) }; if value.count > 4 && value.hasSuffix("s") { return String(value.dropLast()) }; return value }
    public static func project(source: String, layout: String) -> String? {
        let original = word.matches(in: source, range: NSRange(source.startIndex..., in: source)), value = source as NSString, editedValue = layout as NSString
        let edited = word.matches(in: layout, range: NSRange(layout.startIndex..., in: layout)).filter { match in
            let before = editedValue.substring(to: match.range.location), prefix = before.components(separatedBy: "\n").last ?? ""
            return !matches(#"^\s*$"#, prefix) || !matches(#"^\d+[.)]\s+"#, editedValue.substring(from: match.range.location))
        }
        let n = original.count, m = edited.count
        guard n > 0, m > 0, n <= 3000, m <= 3000 else { return nil }
        let a = original.map { key(value.substring(with: $0.range)) }, b = edited.map { key(editedValue.substring(with: $0.range)) }
        var lengths = Array(repeating: Array(repeating: 0, count: m + 1), count: n + 1)
        for i in stride(from: n - 1, through: 0, by: -1) { for j in stride(from: m - 1, through: 0, by: -1) { lengths[i][j] = a[i] == b[j] ? lengths[i+1][j+1] + 1 : max(lengths[i+1][j], lengths[i][j+1]) } }
        guard Double(lengths[0][0]) >= Double(min(n, m)) * 0.6 else { return nil }
        var map = [Int: Int](), x = 0, y = 0
        while x < n && y < m { if a[x] == b[y] { map[y] = x; x += 1; y += 1 } else if lengths[x+1][y] >= lengths[x][y+1] { x += 1 } else { y += 1 } }
        var breaks = [Int: String](), punctuation = [Int: String](), casing = [Int: String](), previousEnd = -1, previousOffset = -1, previousList = false
        let lineExpression = try! NSRegularExpression(pattern: #"(?m)^[^\r\n]+"#)
        for line in lineExpression.matches(in: layout, range: NSRange(layout.startIndex..., in: layout)) {
            let keys = map.keys.filter { edited[$0].range.location >= line.range.location && edited[$0].range.location < NSMaxRange(line.range) }.sorted()
            guard let firstKey = keys.first, let lastKey = keys.last else { continue }
            let markerExpression = try! NSRegularExpression(pattern: #"^[ \t]*(?:- (?:\[[ xX]\] )?|\d+[.)]\s+)"#), lineText = editedValue.substring(with: line.range)
            let marker = markerExpression.firstMatch(in: lineText, range: NSRange(lineText.startIndex..., in: lineText)).map { (lineText as NSString).substring(with: $0.range).trimmingCharacters(in: .newlines) }.map { String($0) } ?? ""
            var start = previousEnd >= 0 ? previousEnd + 1 : map[firstKey]!
            if marker.isEmpty && previousEnd < 0 { start = 0 }
            let blank = previousOffset < 0 || matches(#"\r?\n[ \t]*\r?\n"#, editedValue.substring(with: NSRange(location: max(0, previousOffset), length: line.range.location - max(0, previousOffset))))
            let separation = !marker.isEmpty ? (previousList ? "\n" : "\n\n") : (blank ? "\n\n" : "\n")
            if start > 0 { breaks[start] = separation + marker } else if !marker.isEmpty { breaks[0] = marker }
            if start > 0 && !marker.isEmpty && previousEnd < 0 { punctuation[start-1] = ":" }
            let last = map[lastKey]!, end = NSMaxRange(edited[lastKey].range), tail = editedValue.substring(with: NSRange(location: end, length: NSMaxRange(line.range) - end)).trimmingCharacters(in: .whitespaces)
            if matches(#"^[.!?,:;]*$"#, tail) { punctuation[last] = tail }
            for j in keys { let i = map[j]!; casing[i] = editedValue.substring(with: edited[j].range); let end = NSMaxRange(edited[j].range), next = j+1 < m ? edited[j+1].range.location : editedValue.length, gap = editedValue.substring(with: NSRange(location: end, length: next-end)); if !gap.contains("\n") && matches(#"^[ \t]*[.!?,:;]+[ \t]*$"#, gap) { punctuation[i] = gap.trimmingCharacters(in: .whitespaces) } }
            previousEnd = last; previousOffset = NSMaxRange(line.range); previousList = !marker.isEmpty
        }
        var output = value.substring(to: original[0].range.location)
        for i in 0..<n {
            output += breaks[i] ?? (i > 0 ? " " : "")
            var token = value.substring(with: original[i].range)
            if token.lowercased() == "i" { token = "I" } else if let model = casing[i], model.first?.isUppercase == true, i == 0 || breaks[i] != nil || matches(#"[.!?]$"#, punctuation[i-1] ?? "") || (model.count > 1 && !matches(#"^[a-z]+$"#, String(model.dropFirst()))) { token = String(token.prefix(1)).uppercased() + String(token.dropFirst()) }
            output += token
            if let punct = punctuation[i] { output += punct } else { let end = NSMaxRange(original[i].range), next = i+1 < n ? original[i+1].range.location : value.length, gap = value.substring(with: NSRange(location: end, length: next-end)).trimmingCharacters(in: .whitespaces); if !gap.contains("\n") { output += gap } }
        }
        output = output.replacingOccurrences(of: #"(?mi)^(\s*(?:- (?:\[[ xX]\] )?|\d+[.)] ))and\s+"#, with: "$1", options: .regularExpression).trimmingCharacters(in: .whitespacesAndNewlines)
        return WordGuard.preservesWords(source: source, result: output) ? output : nil
    }
}
