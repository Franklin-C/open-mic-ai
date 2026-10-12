import Foundation

public enum MediaText {
    public static func join(_ previous: String, _ next: String, overlap: Bool) -> String {
        var next = next.trimmingCharacters(in: .whitespacesAndNewlines)
        if previous.isEmpty { return next }; if next.isEmpty { return previous }
        if overlap {
            let regex = try! NSRegularExpression(pattern: "[\\p{L}\\p{N}]+(?:['’][\\p{L}\\p{N}]+)*")
            let a = regex.matches(in: previous, range: NSRange(previous.startIndex..., in: previous))
            let b = regex.matches(in: next, range: NSRange(next.startIndex..., in: next))
            let count = min(12, a.count, b.count)
            if count >= 2 {
                for n in stride(from: count, through: 2, by: -1) {
                    let matches = (0..<n).allSatisfy { index in
                        let left = (previous as NSString).substring(with: a[a.count - n + index].range)
                        let right = (next as NSString).substring(with: b[index].range)
                        return left.replacingOccurrences(of: "’", with: "'").lowercased() == right.replacingOccurrences(of: "’", with: "'").lowercased()
                    }
                    if matches {
                        let end = b[n - 1].range.location + b[n - 1].range.length
                        next = (next as NSString).substring(from: end).trimmingCharacters(in: CharacterSet(charactersIn: " .,!?;:\r\n"))
                        break
                    }
                }
            }
        }
        return next.isEmpty ? previous : previous + " " + next
    }
    public static func parts(_ source: String) -> [String] {
        var remaining = source, output = [String]()
        while remaining.count > 4500 {
            let end = remaining.index(remaining.startIndex, offsetBy: 4500)
            let middle = remaining.index(remaining.startIndex, offsetBy: 2300)
            let sentence = remaining[middle..<end].lastIndex(where: { "\n.!?".contains($0) })
            let split = sentence.map { remaining.index(after: $0) } ?? remaining[..<end].lastIndex(of: " ") ?? end
            output.append(String(remaining[..<split]).trimmingCharacters(in: .whitespacesAndNewlines))
            remaining = String(remaining[split...]).trimmingCharacters(in: .whitespacesAndNewlines)
        }
        if !remaining.isEmpty { output.append(remaining) }; return output
    }
}
