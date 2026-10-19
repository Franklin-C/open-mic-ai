import Foundation

public struct UsageCount: Codable, Identifiable {
    public var id: String { name }
    public var name: String
    public var words = 0
    public var sessions = 0
    public init(name: String, words: Int = 0, sessions: Int = 0) { self.name = name; self.words = words; self.sessions = sessions }
}
public struct UsageDay: Codable {
    public var date: String
    public var words = 0, timedWords = 0, sessions = 0, longest = 0
    public var seconds = 0.0, fastest = 0.0
    public var input: Int64 = 0, output: Int64 = 0
    public var apps: [UsageCount] = [], styles: [UsageCount] = []
    public var hours = [Int](repeating: 0, count: 24)
    public init(date: String) { self.date = date }
}
public struct UsageStatistics: Codable {
    public var days: [String: UsageDay] = [:]
    public var typingWpm = 40
    public var detailsSince = ""
    public var legacyInput: Int64 = 0, legacyOutput: Int64 = 0
    public var seededFromHistory = false
    public init() {}
    public static func key(_ date: Date, calendar: Calendar = .current) -> String {
        let parts = calendar.dateComponents([.year, .month, .day], from: date)
        return String(format: "%04d-%02d-%02d", parts.year!, parts.month!, parts.day!)
    }
    public static func date(_ key: String, calendar: Calendar = .current) -> Date? {
        let parts = key.split(separator: "-").compactMap { Int($0) }
        guard parts.count == 3 else { return nil }
        let result = calendar.date(from: DateComponents(year: parts[0], month: parts[1], day: parts[2]))
        return result.flatMap { self.key($0, calendar: calendar) == key ? $0 : nil }
    }
    public mutating func record(source: String, seconds: Double, app: String, style: String, at date: Date = Date(), calendar: Calendar = .current) {
        let key = Self.key(date, calendar: calendar), count = WordGuard.words(source).count
        var day = days[key] ?? UsageDay(date: key)
        let duration = seconds.isFinite ? max(0, seconds) : 0
        day.words += count; day.sessions += 1; day.seconds += duration; day.timedWords += duration > 0 ? count : 0
        day.longest = max(day.longest, count)
        if duration >= 10 && count >= 10 { day.fastest = max(day.fastest, Double(count) * 60 / duration) }
        func add(_ list: inout [UsageCount], _ name: String) {
            if let index = list.firstIndex(where: { $0.name.lowercased() == name.lowercased() }) { list[index].words += count; list[index].sessions += 1 }
            else { list.append(UsageCount(name: name, words: count, sessions: 1)) }
        }
        add(&day.apps, app.isEmpty ? "Other" : app); add(&day.styles, style.isEmpty ? "Unknown" : style)
        day.hours[calendar.component(.hour, from: date)] += count; days[key] = day
        if detailsSince.isEmpty { detailsSince = key }
    }
    public mutating func record(tokens: AiTokenUsage, at date: Date = Date(), calendar: Calendar = .current) {
        guard tokens.input >= 0 && tokens.output >= 0 else { return }
        let key = Self.key(date, calendar: calendar); var day = days[key] ?? UsageDay(date: key)
        day.input += tokens.input; day.output += tokens.output; days[key] = day
    }
    public static func seeded(history: [HistoryEntry], tokens: AiTokenTotals, today: Date = Date()) -> UsageStatistics {
        var value = UsageStatistics(); value.legacyInput = tokens.input; value.legacyOutput = tokens.output
        // Earlier capped history cannot supply all-time usage; disclose the seed's scope.
        for entry in history where entry.action == "Dictate" { value.record(source: entry.source, seconds: entry.duration, app: "Unknown", style: "Unknown", at: entry.date) }
        value.seededFromHistory = !history.isEmpty; value.detailsSince = key(today); return value
    }
    public func snapshot(range: Int, today: Date = Date(), calendar: Calendar = .current) -> UsageSnapshot {
        UsageSnapshot(self, range: range, today: today, calendar: calendar)
    }
}
public struct UsageSnapshot {
    public let days: [UsageDay]
    public let start: Date, end: Date
    public let words: Int, sessions: Int, timedWords: Int, activeDays: Int, longest: Int
    public let seconds: Double, fastest: Double, previousWords: Int
    public let input: Int64, output: Int64
    public let currentStreak: Int, longestStreak: Int
    public let apps: [UsageCount], styles: [UsageCount], hours: [Int], weekdays: [Int]
    public var wpm: Double { seconds > 0 ? Double(timedWords) * 60 / seconds : 0 }
    public var totalTokens: Int64 { input + output }
    public func savedSeconds(typingWpm: Int) -> Double { seconds > 0 ? max(0, Double(timedWords) * 60 / Double(max(10, typingWpm)) - seconds) : 0 }
    init(_ data: UsageStatistics, range: Int, today: Date, calendar: Calendar) {
        end = calendar.startOfDay(for: today)
        let valid = data.days.values.filter { day in UsageStatistics.date(day.date, calendar: calendar).map { $0 <= calendar.startOfDay(for: today) } ?? false }.sorted { $0.date < $1.date }
        start = range > 0 ? calendar.date(byAdding: .day, value: 1 - range, to: end)! : valid.first.flatMap { UsageStatistics.date($0.date, calendar: calendar) } ?? end
        let startKey = UsageStatistics.key(start, calendar: calendar)
        days = valid.filter { $0.date >= startKey }
        words = days.reduce(0) { $0 + $1.words }; sessions = days.reduce(0) { $0 + $1.sessions }; timedWords = days.reduce(0) { $0 + $1.timedWords }; seconds = days.reduce(0) { $0 + $1.seconds }
        activeDays = days.filter { $0.sessions > 0 }.count
        longest = days.map(\.longest).max() ?? 0; fastest = days.map(\.fastest).max() ?? 0
        input = days.reduce(0) { $0 + $1.input } + (range == 0 ? data.legacyInput : 0)
        output = days.reduce(0) { $0 + $1.output } + (range == 0 ? data.legacyOutput : 0)
        let previousKey = UsageStatistics.key(calendar.date(byAdding: .day, value: -max(1, range), to: start)!, calendar: calendar)
        previousWords = range > 0 ? valid.filter { $0.date >= previousKey && $0.date < startKey }.reduce(0) { $0 + $1.words } : 0
        func merge(_ values: [UsageCount]) -> [UsageCount] {
            Dictionary(grouping: values, by: { $0.name }).map { UsageCount(name: $0.key, words: $0.value.reduce(0) { $0 + $1.words }, sessions: $0.value.reduce(0) { $0 + $1.sessions }) }.sorted { $0.words == $1.words ? $0.name < $1.name : $0.words > $1.words }
        }
        apps = merge(days.flatMap(\.apps)); styles = merge(days.flatMap(\.styles))
        var hourValues = [Int](repeating: 0, count: 24), weekdayValues = [Int](repeating: 0, count: 7)
        for day in days {
            for (index, count) in day.hours.prefix(24).enumerated() { hourValues[index] += count }
            if let date = UsageStatistics.date(day.date, calendar: calendar) { weekdayValues[(calendar.component(.weekday, from: date) + 5) % 7] += day.words }
        }
        hours = hourValues; weekdays = weekdayValues
        let active = Set(valid.filter { $0.sessions > 0 }.map(\.date))
        var current = active.contains(UsageStatistics.key(end, calendar: calendar)) ? end : calendar.date(byAdding: .day, value: -1, to: end)!
        var streak = 0
        while active.contains(UsageStatistics.key(current, calendar: calendar)) { streak += 1; current = calendar.date(byAdding: .day, value: -1, to: current)! }
        currentStreak = streak
        var longestRun = 0, run = 0, previous: Date?
        for key in active.sorted() {
            guard let day = UsageStatistics.date(key, calendar: calendar) else { continue }
            run = previous.map { calendar.date(byAdding: .day, value: 1, to: $0) == day } == true ? run + 1 : 1
            longestRun = max(longestRun, run); previous = day
        }
        longestStreak = longestRun
    }
    public static func duration(_ seconds: Double) -> String { seconds < 60 ? (seconds > 0 ? "<1 min" : "0 min") : seconds < 3600 ? "\(Int((seconds / 60).rounded())) min" : String(format: "%.1f hr", seconds / 3600) }
}
