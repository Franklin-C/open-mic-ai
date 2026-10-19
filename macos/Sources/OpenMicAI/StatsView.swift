import SwiftUI
import OpenMicCore

struct StatsPoint: Identifiable {
    let id: Int
    let label: String
    let detail: String
    let value: Int64
}
struct StatsChart: View {
    let title: String
    let points: [StatsPoint]
    var unit = "words"
    @State private var selected: Int?
    var maximum: Double { max(1, Double(points.map(\.value).max() ?? 0)) }
    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text(title).font(.headline)
            if points.allSatisfy({ $0.value == 0 }) {
                Text("No data in this period").foregroundStyle(.secondary).frame(maxWidth: .infinity, minHeight: 120, alignment: .leading)
            } else {
                HStack(alignment: .bottom, spacing: 3) {
                    ForEach(points) { point in
                        Button { selected = point.id } label: {
                            VStack(spacing: 5) {
                                Spacer(minLength: 0)
                                RoundedRectangle(cornerRadius: 2).fill(selected == point.id ? Color.red : Color.red.opacity(0.32)).frame(height: max(1, 105 * Double(point.value) / maximum))
                                Text(point.label).font(.system(size: 9)).foregroundStyle(.secondary).frame(height: 13)
                            }.frame(maxWidth: .infinity).contentShape(Rectangle())
                        }.buttonStyle(.plain).help("\(point.detail) · \(point.value.formatted()) \(unit)").onHover { inside in if inside { selected = point.id } }
                        .accessibilityLabel("\(point.detail), \(point.value) \(unit)")
                    }
                }.frame(height: 123)
            }
            if let point = points.first(where: { $0.id == selected }) { Text("\(point.detail) · \(point.value.formatted()) \(unit)").font(.caption).foregroundStyle(.secondary) }
            else { Text("Hover or select a bar").font(.caption).foregroundStyle(.secondary) }
        }.padding(18).frame(maxWidth: .infinity, alignment: .leading).background(.white, in: RoundedRectangle(cornerRadius: 12)).overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.gray.opacity(0.15)))
    }
}
struct StatsRanking: View {
    let title: String
    let rows: [UsageCount]
    var body: some View {
        VStack(alignment: .leading, spacing: 15) {
            Text(title).font(.headline)
            if rows.isEmpty { Text("No data in this period").foregroundStyle(.secondary) }
            ForEach(Array(rows.prefix(5))) { row in
                HStack { Text(row.name).lineLimit(1).frame(width: 100, alignment: .leading); ProgressView(value: Double(row.words), total: Double(max(1, rows.first?.words ?? 1))).tint(.red); Text(row.words.formatted()).font(.caption).foregroundStyle(.secondary).frame(width: 60, alignment: .trailing) }.help("\(row.sessions) dictations · \(row.words) words")
            }
            Spacer(minLength: 0)
        }.padding(18).frame(maxWidth: .infinity, minHeight: 188, alignment: .topLeading).background(.white, in: RoundedRectangle(cornerRadius: 12)).overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.gray.opacity(0.15)))
    }
}
@MainActor struct StatsView: View {
    @ObservedObject var state: AppState
    @State private var range = 30
    @State private var day: Date?
    var stats: UsageSnapshot { state.statistics.snapshot(range: range) }
    var all: UsageSnapshot { state.statistics.snapshot(range: 0) }
    var trend: String {
        guard range > 0 else { return "Lifetime total" }
        return stats.previousWords > 0 ? String(format: "%+.0f%% vs previous %d days", Double(stats.words - stats.previousWords) * 100 / Double(stats.previousWords), range) : "No earlier period to compare"
    }
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Picker("Period", selection: $range) { Text("7 days").tag(7); Text("30 days").tag(30); Text("90 days").tag(90); Text("All time").tag(0) }.pickerStyle(.segmented)
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    Text("\(state.preferences.trackUsage ? "" : "Tracking paused · ")\(stats.activeDays) active days · \(stats.currentStreak) day streak").font(.caption).foregroundStyle(.secondary)
                    LazyVGrid(columns: Array(repeating: GridItem(.flexible()), count: 4), spacing: 12) {
                        metric("Words dictated", stats.words.formatted(), trend)
                        metric("Dictations", stats.sessions.formatted(), stats.sessions > 0 ? "\(stats.words / stats.sessions) words on average" : "Start your first dictation")
                        metric("Time saved · estimate", UsageSnapshot.duration(stats.savedSeconds(typingWpm: state.statistics.typingWpm)), "Compared with \(state.statistics.typingWpm) wpm")
                        metric("Words / minute", stats.seconds > 0 ? String(Int(stats.wpm.rounded())) : "—", "\(UsageSnapshot.duration(stats.seconds)) recorded")
                    }
                    StatsChart(title: range == 0 ? "Words over time · last 90 days" : "Words over time", points: dailyPoints(tokens: false)).id("words-\(range)")
                    Text("Local AI").font(.title3.bold())
                    LazyVGrid(columns: Array(repeating: GridItem(.flexible()), count: 3), spacing: 12) { metric("AI input tokens", stats.input.formatted()); metric("AI output tokens", stats.output.formatted()); metric("Total AI tokens", stats.totalTokens.formatted(), "Routing + editing + checks") }
                    StatsChart(title: "AI tokens over time", points: dailyPoints(tokens: true), unit: "tokens").id("tokens-\(range)")
                    HStack(alignment: .top, spacing: 12) { StatsRanking(title: "Your apps", rows: stats.apps); StatsRanking(title: "Writing styles", rows: stats.styles) }
                    Text("Habits").font(.title3.bold())
                    HStack(alignment: .top, spacing: 12) { StatsChart(title: "Words by weekday", points: weekdayPoints); StatsChart(title: "Your rhythm · hour of day", points: hourPoints) }
                    Text("Activity · last four months").font(.headline)
                    ActivityView(usageDays: state.statistics.days, selected: $day)
                    if let day { Text("\(day.formatted(date: .abbreviated, time: .omitted)) · \(state.statistics.days[UsageStatistics.key(day)]?.words ?? 0) words").font(.caption).foregroundStyle(.secondary) }
                    records
                    milestones
                    HStack { Text("Your typing speed"); Stepper("\(state.statistics.typingWpm) words / min", value: $state.statistics.typingWpm, in: 10...200).onChange(of: state.statistics.typingWpm) { _ in state.saveStats() } }.font(.caption)
                    Text("Time saved and pages are estimates (250 words/page). New breakdowns start \(state.statistics.detailsSince). Stats stay on this computer. Manage tracking and reset in System → General.").font(.caption).foregroundStyle(.secondary)
                    if state.statistics.seededFromHistory { Text("Earlier word totals include only the saved history available when Stats was added.").font(.caption).foregroundStyle(.secondary) }
                }.padding(.trailing, 5)
            }
        }
    }
    func metric(_ label: String, _ number: String, _ detail: String = "") -> some View {
        VStack(alignment: .leading, spacing: 6) { Text(number).font(.system(size: 26, weight: .bold)).minimumScaleFactor(0.7).lineLimit(1); Text(label).font(.caption).foregroundStyle(.secondary); Text(detail.isEmpty ? " " : detail).font(.system(size: 10)).foregroundStyle(.secondary).lineLimit(2) }.padding(16).frame(maxWidth: .infinity, minHeight: 92, alignment: .leading).background(.white, in: RoundedRectangle(cornerRadius: 12)).overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.gray.opacity(0.15)))
    }
    func dailyPoints(tokens: Bool) -> [StatsPoint] {
        let calendar = Calendar.current, end = calendar.startOfDay(for: Date())
        let start = range == 0 ? calendar.date(byAdding: .day, value: -89, to: end)! : stats.start
        let count = (calendar.dateComponents([.day], from: start, to: end).day ?? 0) + 1, step = count > 31 ? 7 : 1
        return stride(from: 0, to: count, by: step).enumerated().map { index, offset in
            let date = calendar.date(byAdding: .day, value: offset, to: start)!, last = calendar.date(byAdding: .day, value: min(step, count - offset) - 1, to: date)!
            let firstKey = UsageStatistics.key(date), lastKey = UsageStatistics.key(last)
            let value = stats.days.filter { $0.date >= firstKey && $0.date <= lastKey }.reduce(Int64(0)) { $0 + (tokens ? $1.input + $1.output : Int64($1.words)) }
            let label = index % max(1, (count / step + 7) / 8) == 0 ? date.formatted(.dateTime.month(.abbreviated).day()) : " "
            let detail = date.formatted(date: .abbreviated, time: .omitted) + (step > 1 ? " – " + last.formatted(date: .abbreviated, time: .omitted) : "")
            return StatsPoint(id: index, label: label, detail: detail, value: value)
        }
    }
    var weekdayPoints: [StatsPoint] { stats.weekdays.enumerated().map { StatsPoint(id: $0.offset, label: ["M", "T", "W", "T", "F", "S", "S"][$0.offset], detail: ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"][$0.offset], value: Int64($0.element)) } }
    var hourPoints: [StatsPoint] { stats.hours.enumerated().map { StatsPoint(id: $0.offset, label: $0.offset % 6 == 0 ? "\($0.offset)" : " ", detail: String(format: "%02d:00 – %02d:59", $0.offset, $0.offset), value: Int64($0.element)) } }
    var records: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Personal records").font(.headline)
            let best = stats.days.filter { $0.words > 0 }.max { $0.words < $1.words }
            Text("Best day   \(best.map { "\($0.date) · \($0.words.formatted()) words" } ?? "—")")
            Text("Longest dictation   \(stats.longest > 0 ? "\(stats.longest.formatted()) words" : "—")   ·   Fastest   \(stats.fastest > 0 ? "\(Int(stats.fastest.rounded())) wpm" : "—")")
            Text("Longest streak · all time   \(stats.longestStreak) days")
        }.font(.callout).padding(18).frame(maxWidth: .infinity, alignment: .leading).background(Color(white: 0.98), in: RoundedRectangle(cornerRadius: 12))
    }
    var milestones: some View {
        let targets = [100, 1000, 10000, 50000, 100000, 1000000]
        return VStack(alignment: .leading, spacing: 12) {
            Text("Milestones · all time").font(.headline)
            HStack(spacing: 15) { ForEach(targets, id: \.self) { target in Label(target >= 1000 ? "\(target / 1000)k" : "\(target)", systemImage: all.words >= target ? "checkmark.circle.fill" : "circle").foregroundStyle(all.words >= target ? Color.red : Color.secondary).font(.caption) } }
            if let next = targets.first(where: { $0 > all.words }) { Text("\((next - all.words).formatted()) words to your next milestone · About \(Int(Double(all.words) / 250).formatted()) pages spoken").font(.caption).foregroundStyle(.secondary) }
            else { Text("One million words. Keep going!").font(.caption) }
        }.padding(18).frame(maxWidth: .infinity, alignment: .leading).background(Color(white: 0.98), in: RoundedRectangle(cornerRadius: 12))
    }
}
