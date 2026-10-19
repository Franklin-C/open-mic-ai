import XCTest
@testable import OpenMicCore

final class StatisticsTests: XCTestCase {
    var calendar: Calendar { var value = Calendar(identifier: .gregorian); value.timeZone = TimeZone(identifier: "America/Toronto")!; return value }
    func date(_ text: String, hour: Int = 12) -> Date { calendar.date(byAdding: .hour, value: hour, to: UsageStatistics.date(text, calendar: calendar)!)! }
    func words(_ count: Int) -> String { (0..<count).map { "word\($0)" }.joined(separator: " ") }
    func testFiltersBoundariesWeightedSpeedAndTokens() throws {
        var value = UsageStatistics()
        value.record(source: words(100), seconds: 30, app: "Docs", style: "Polish", at: date("2026-10-19", hour: 9), calendar: calendar)
        value.record(source: words(20), seconds: 10, app: "Docs", style: "Clean up", at: date("2026-10-19", hour: 17), calendar: calendar)
        value.record(source: words(10), seconds: 5, app: "Mail", style: "Raw transcript", at: date("2026-10-18", hour: 9), calendar: calendar)
        value.record(source: words(200), seconds: 60, app: "Mail", style: "Custom", at: date("2026-10-12"), calendar: calendar)
        value.record(source: words(40), seconds: 20, app: "Mail", style: "Custom", at: date("2026-10-11"), calendar: calendar)
        value.record(tokens: AiTokenUsage(input: 120, output: 30), at: date("2026-10-19"), calendar: calendar)
        value.record(tokens: AiTokenUsage(input: 40, output: 10), at: date("2026-10-12"), calendar: calendar)
        let week = value.snapshot(range: 7, today: date("2026-10-19"), calendar: calendar), all = value.snapshot(range: 0, today: date("2026-10-19"), calendar: calendar)
        XCTAssertEqual(week.words, 130); XCTAssertEqual(week.sessions, 3); XCTAssertEqual(week.previousWords, 240)
        XCTAssertEqual(all.words, 370); XCTAssertEqual(all.sessions, 5); XCTAssertEqual(all.totalTokens, 200)
        XCTAssertEqual(week.input, 120); XCTAssertEqual(week.output, 30); XCTAssertEqual(week.wpm, 130 * 60 / 45.0, accuracy: 0.001)
        XCTAssertEqual(week.longest, 100); XCTAssertEqual(week.fastest, 200)
        XCTAssertEqual(week.savedSeconds(typingWpm: 40), 150); XCTAssertEqual(week.savedSeconds(typingWpm: 200), 0)
        XCTAssertEqual(week.currentStreak, 2); XCTAssertEqual(week.longestStreak, 2); XCTAssertEqual(week.activeDays, 2)
        XCTAssertEqual(week.hours[9], 110); XCTAssertEqual(week.hours[17], 20); XCTAssertEqual(week.hours.reduce(0, +), 130)
        XCTAssertEqual(week.weekdays[0], 120); XCTAssertEqual(week.weekdays[6], 10)
        XCTAssertEqual(week.apps.first?.name, "Docs"); XCTAssertEqual(week.apps.first?.words, 120)
        XCTAssertEqual(Set(week.styles.map(\.name)), ["Polish", "Clean up", "Raw transcript"])
        let data = try JSONEncoder().encode(value), restored = try JSONDecoder().decode(UsageStatistics.self, from: data)
        XCTAssertEqual(restored.snapshot(range: 7, today: date("2026-10-19"), calendar: calendar).words, 130)
        XCTAssertFalse(String(decoding: data, as: UTF8.self).contains("word0"))
    }
    func testNoFakeDurationOrSpeedFromShortBursts() {
        var value = UsageStatistics()
        value.record(source: words(100), seconds: 30, app: "", style: "Polish", at: date("2026-10-19"), calendar: calendar)
        value.record(source: words(500), seconds: .nan, app: "", style: "Polish", at: date("2026-10-19"), calendar: calendar)
        value.record(source: words(5), seconds: 1, app: "", style: "Polish", at: date("2026-10-19"), calendar: calendar)
        let result = value.snapshot(range: 7, today: date("2026-10-19"), calendar: calendar)
        XCTAssertEqual(result.words, 605); XCTAssertEqual(result.timedWords, 105); XCTAssertEqual(result.seconds, 31)
        XCTAssertEqual(result.fastest, 200); XCTAssertEqual(result.wpm, 105 * 60 / 31.0, accuracy: 0.001)
    }
    func testStreakAcrossDSTAndTokenOnlyDay() {
        var value = UsageStatistics()
        for key in ["2026-10-31", "2026-11-01", "2026-11-02"] { value.record(source: "hello", seconds: 1, app: "Mail", style: "Raw transcript", at: date(key), calendar: calendar) }
        value.record(tokens: AiTokenUsage(input: 1, output: 1), at: date("2026-11-03"), calendar: calendar)
        let snapshot = value.snapshot(range: 7, today: date("2026-11-03"), calendar: calendar)
        XCTAssertEqual(snapshot.currentStreak, 3); XCTAssertEqual(snapshot.longestStreak, 3); XCTAssertEqual(snapshot.activeDays, 3)
        XCTAssertEqual(value.snapshot(range: 7, today: date("2026-11-04"), calendar: calendar).currentStreak, 0)
    }
    func testLegacySeedDoesNotInventDatedTokens() {
        var tokens = AiTokenTotals(); tokens.add(AiTokenUsage(input: 100, output: 20))
        let history = [HistoryEntry(source: "one two three", result: "One two three.", action: "Dictate", duration: 2), HistoryEntry(source: "selected text", result: "Summary", action: "Summarize")]
        let value = UsageStatistics.seeded(history: history, tokens: tokens)
        XCTAssertTrue(value.seededFromHistory)
        XCTAssertEqual(value.snapshot(range: 0).words, 3); XCTAssertEqual(value.snapshot(range: 0).totalTokens, 120)
        XCTAssertEqual(value.snapshot(range: 7).totalTokens, 0)
        XCTAssertEqual(value.snapshot(range: 0).styles.first?.name, "Unknown")
    }
    func testEmptyMalformedFutureAndRangeLengths() {
        var value = UsageStatistics()
        XCTAssertEqual(value.snapshot(range: 30).words, 0)
        XCTAssertNil(UsageStatistics.date("2026-02-30", calendar: calendar)); XCTAssertNil(UsageStatistics.date("invalid", calendar: calendar))
        for offset in 0..<100 { value.record(source: "hello", seconds: 1, app: "Mail", style: "Polish", at: calendar.date(byAdding: .day, value: -offset, to: date("2026-10-19"))!, calendar: calendar) }
        for range in [7, 30, 90] { XCTAssertEqual(value.snapshot(range: range, today: date("2026-10-19"), calendar: calendar).words, range) }
        value.record(source: words(1000), seconds: 5, app: "Mail", style: "Polish", at: date("2026-10-20"), calendar: calendar)
        XCTAssertEqual(value.snapshot(range: 7, today: date("2026-10-19"), calendar: calendar).words, 7)
        XCTAssertEqual(UsageSnapshot.duration(0), "0 min"); XCTAssertEqual(UsageSnapshot.duration(10), "<1 min")
        var defaults = Preferences(); XCTAssertTrue(defaults.trackUsage); defaults.trackUsage = false
        XCTAssertFalse((try! JSONDecoder().decode(Preferences.self, from: JSONEncoder().encode(defaults))).trackUsage)
        XCTAssertTrue((try! JSONDecoder().decode(Preferences.self, from: Data("{}".utf8))).trackUsage)
    }
}
