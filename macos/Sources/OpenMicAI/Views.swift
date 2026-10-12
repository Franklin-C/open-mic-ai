import SwiftUI
import AppKit
import OpenMicCore

@MainActor struct MainView: View {
    @ObservedObject var state: AppState
    var body: some View {
        HStack(spacing: 0) {
            VStack(alignment: .leading, spacing: 8) {
                HStack { Image(systemName: "mic.fill").font(.title).foregroundStyle(.red); Text("Open Mic AI").font(.title3.bold()) }.padding(.bottom, 28)
                ForEach([("Home", "house"), ("History", "clock"), ("Snippets", "scissors"), ("Vocabulary", "text.book.closed"), ("Settings", "gearshape")], id: \.0) { name, icon in
                    Button { state.page = name } label: {
                        Label(name, systemImage: icon).frame(maxWidth: .infinity, alignment: .leading).padding(12)
                            .foregroundStyle(state.page == name ? Color.red : Color.primary)
                            .background(state.page == name ? Color.red.opacity(0.07) : Color.clear, in: RoundedRectangle(cornerRadius: 9))
                    }.buttonStyle(.plain)
                }
                Spacer(); Text(state.status).font(.caption).foregroundStyle(.secondary)
                Text("English · Local").font(.caption).foregroundStyle(.secondary)
            }.padding(24).frame(width: 210).background(Color(white: 0.985))
            Divider()
            VStack(alignment: .leading, spacing: 18) {
                Text(state.page).font(.largeTitle.bold())
                switch state.page {
                case "Home": HomeView(state: state)
                case "History": HistoryView(state: state)
                case "Snippets": SnippetsView(state: state)
                case "Vocabulary": VocabularyView(state: state)
                default: SettingsView(state: state)
                }
                if !state.message.isEmpty { HStack { Text(state.message).font(.caption).foregroundStyle(.secondary); Spacer(); Button("Dismiss") { state.message = "" } }.padding(.top, 8) }
            }.padding(32).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        }.background(.white).tint(.red).preferredColorScheme(.light)
    }
}
@MainActor struct HomeView: View {
    @ObservedObject var state: AppState
    @State private var day: Date?
    var dictations: [HistoryEntry] { state.history.filter { $0.action == "Dictate" } }
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 24) {
                HStack(spacing: 35) {
                    metric("Words", value: String(dictations.reduce(0) { $0 + WordGuard.words($1.source).count }))
                    metric("Dictations", value: String(dictations.count))
                    let seconds = dictations.reduce(0) { $0 + $1.duration }
                    metric("Words / min", value: seconds > 0 ? String(Int(Double(dictations.reduce(0) { $0 + WordGuard.words($1.source).count }) * 60 / seconds)) : "—")
                }
                VStack(alignment: .leading, spacing: 12) {
                    Text("Activity").font(.headline)
                    ActivityView(entries: dictations, selected: $day)
                    if let day {
                        let words = dictations.filter { Calendar.current.isDate($0.date, inSameDayAs: day) }.reduce(0) { $0 + WordGuard.words($1.source).count }
                        Text("\(day.formatted(date: .abbreviated, time: .omitted)) · \(words) words").font(.caption).foregroundStyle(.secondary)
                    }
                }.padding(20).background(Color(white: 0.98), in: RoundedRectangle(cornerRadius: 14))
                Text("Recent dictations").font(.headline)
                if dictations.isEmpty { Text("Click a text box, press \(state.preferences.shortcutLabel), and speak.").foregroundStyle(.secondary) }
                ForEach(Array(dictations.prefix(5))) { row in HistoryRow(state: state, entry: row) }
            }
        }
    }
    func metric(_ title: String, value: String) -> some View { VStack(alignment: .leading, spacing: 5) { Text(value).font(.system(size: 32, weight: .semibold)); Text(title).font(.caption).foregroundStyle(.secondary) } }
}
struct ActivityView: View {
    let entries: [HistoryEntry]
    @Binding var selected: Date?
    let days = (0..<112).map { Calendar.current.date(byAdding: .day, value: $0 - 111, to: Date())! }
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 5) {
                ForEach(0..<16, id: \.self) { week in
                    VStack(spacing: 5) {
                        let first = days[week * 7]
                        Text(week == 0 || Calendar.current.component(.day, from: first) <= 7 ? first.formatted(.dateTime.month(.abbreviated)) : " ").font(.system(size: 9)).frame(width: 22)
                        ForEach(0..<7, id: \.self) { offset in
                            let date = days[week * 7 + offset]
                            let words = entries.filter { Calendar.current.isDate($0.date, inSameDayAs: date) }.reduce(0) { $0 + WordGuard.words($1.source).count }
                            RoundedRectangle(cornerRadius: 3).fill(words == 0 ? Color.gray.opacity(0.1) : Color.red.opacity(min(1, 0.25 + Double(words) / 300)))
                                .frame(width: 22, height: 18).overlay(RoundedRectangle(cornerRadius: 3).stroke(selected.map { Calendar.current.isDate($0, inSameDayAs: date) } == true ? Color.red : .clear, lineWidth: 2))
                                .help("\(date.formatted(date: .abbreviated, time: .omitted)) · \(words) words")
                                .onHover { inside in if inside { selected = date } }.onTapGesture { selected = date }
                        }
                    }
                }
            }
            HStack { Spacer(); Text("Less"); ForEach(0..<4, id: \.self) { index in RoundedRectangle(cornerRadius: 2).fill(Color.red.opacity(Double(index + 1) / 4)).frame(width: 12, height: 12) }; Text("More") }.font(.caption2).foregroundStyle(.secondary)
        }
    }
}
@MainActor struct HistoryRow: View {
    @ObservedObject var state: AppState
    let entry: HistoryEntry
    @State private var original = false
    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack { Text(entry.date.formatted(date: .abbreviated, time: .shortened)).font(.caption).foregroundStyle(.secondary); Text(entry.action).font(.caption).foregroundStyle(.red); Spacer(); Button("Copy") { state.copy(original ? entry.source : entry.result) }; Button(original ? "Result" : "Original") { original.toggle() }; Button { state.history.removeAll { $0.id == entry.id }; state.write("history.json", state.history) } label: { Image(systemName: "trash") } }
            Text(original ? entry.source : entry.result).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading)
        }.padding(16).background(.white, in: RoundedRectangle(cornerRadius: 12)).overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.gray.opacity(0.15)))
    }
}
@MainActor struct HistoryView: View {
    @ObservedObject var state: AppState
    @State private var search = ""
    @State private var date = Date()
    @State private var filterDate = false
    @State private var useStyle = true
    @State private var original = false
    var entries: [HistoryEntry] { state.history.filter { (search.isEmpty || $0.result.localizedCaseInsensitiveContains(search) || $0.source.localizedCaseInsensitiveContains(search)) && (!filterDate || Calendar.current.isDate($0.date, inSameDayAs: date)) } }
    var body: some View {
        VStack(spacing: 16) {
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Button("Import file") {
                        let panel = NSOpenPanel(); panel.title = "Import audio or video"; panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
                        panel.beginSheetModal(for: NSApp.keyWindow ?? NSApp.mainWindow!) { response in
                            if response == .OK, let url = panel.url { original = false; state.importFile(url, edit: useStyle) }
                        }
                    }.buttonStyle(.borderedProminent).disabled(state.importBusy)
                    Toggle("Use writing style", isOn: $useStyle).disabled(state.importBusy)
                    if state.importBusy { ProgressView().controlSize(.small); Button("Cancel") { state.cancelImport() } }
                    Spacer()
                    if !state.importSource.isEmpty { Button("Copy") { state.copy(original ? state.importSource : state.importResult) }; Button(original ? "Result" : "Original") { original.toggle() } }
                }
                Text(state.importName.isEmpty ? "Audio or video · Processed locally" : "\(state.importName) · \(state.importStatus)").font(.caption).foregroundStyle(.secondary)
                if state.importName.isEmpty && !state.importStatus.isEmpty { Text(state.importStatus).font(.caption).foregroundStyle(.secondary) }
                if !state.importSource.isEmpty { ScrollView { Text(original ? state.importSource : state.importResult).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }.frame(height: 90) }
            }.padding(16).background(Color(white: 0.985), in: RoundedRectangle(cornerRadius: 12))
            HStack { TextField("Search history", text: $search).textFieldStyle(.roundedBorder); Toggle("Date", isOn: $filterDate); if filterDate { DatePicker("", selection: $date, displayedComponents: .date).labelsHidden() } }
            ScrollView { LazyVStack(spacing: 12) { if entries.isEmpty { Text("No results yet").foregroundStyle(.secondary).padding(30) }; ForEach(entries) { row in HistoryRow(state: state, entry: row) } } }
        }
    }
}
@MainActor struct SnippetsView: View {
    @ObservedObject var state: AppState
    @State private var trigger = ""
    @State private var text = ""
    @State private var editing: UUID?
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text("Say a phrase to insert saved text.").foregroundStyle(.secondary)
                TextField("Spoken phrase", text: $trigger).textFieldStyle(.roundedBorder)
                TextEditor(text: $text).frame(height: 100).overlay(RoundedRectangle(cornerRadius: 6).stroke(Color.gray.opacity(0.2)))
                HStack {
                    Button(editing == nil ? "Add snippet" : "Save snippet") {
                        guard !trigger.trimmingCharacters(in: .whitespaces).isEmpty, !text.isEmpty else { state.message = "Enter a phrase and some text."; return }
                        if let editing, let index = state.snippets.firstIndex(where: { $0.id == editing }) { state.snippets[index].trigger = trigger; state.snippets[index].text = text } else { state.snippets.append(Snippet(trigger: trigger, text: text)) }
                        state.saveLibrary(); trigger = ""; text = ""; editing = nil
                    }.buttonStyle(.borderedProminent)
                    if editing != nil { Button("Cancel") { editing = nil; trigger = ""; text = "" } }
                }
                ForEach(state.snippets) { snippet in
                    VStack(alignment: .leading, spacing: 8) {
                        HStack { Text(snippet.trigger).font(.headline); Spacer(); Button("Edit") { editing = snippet.id; trigger = snippet.trigger; text = snippet.text }; Button("Copy") { state.copy(snippet.text) }; Button("Delete") { state.snippets.removeAll { $0.id == snippet.id }; state.saveLibrary() } }
                        Text(snippet.text).textSelection(.enabled).foregroundStyle(.secondary)
                    }.padding(16).background(Color(white: 0.98), in: RoundedRectangle(cornerRadius: 12))
                }
            }
        }
    }
}
@MainActor struct VocabularyView: View {
    @ObservedObject var state: AppState
    @State private var heard = ""
    @State private var written = ""
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Names and words you use often.").foregroundStyle(.secondary)
            HStack { TextField("Heard as (optional)", text: $heard); TextField("Correct spelling", text: $written); Button("Add word") { guard !written.isEmpty else { return }; state.vocabulary.append(VocabularyWord(heard: heard, written: written)); state.saveLibrary(); heard = ""; written = "" }.buttonStyle(.borderedProminent) }.textFieldStyle(.roundedBorder)
            List { ForEach(state.vocabulary) { word in HStack { Text(word.written); if !word.heard.isEmpty { Text("← \(word.heard)").foregroundStyle(.secondary) }; Spacer(); Button("Delete") { state.vocabulary.removeAll { $0.id == word.id }; state.saveLibrary() } } } }.listStyle(.plain)
        }
    }
}
@MainActor struct SettingsView: View {
    @ObservedObject var state: AppState
    var body: some View {
        VStack(alignment: .leading, spacing: 24) {
            Picker("", selection: $state.section) { ForEach(["Style", "Models", "Shortcuts", "System"], id: \.self) { Text($0) } }.pickerStyle(.segmented).labelsHidden()
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    switch state.section {
                    case "Style": style
                    case "Models": models
                    case "Shortcuts": shortcuts
                    default: system
                    }
                }.frame(maxWidth: .infinity, alignment: .leading)
            }
        }.onDisappear { state.cancelShortcut() }
    }
    var style: some View {
        VStack(alignment: .leading, spacing: 20) {
            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
                ForEach(WritingStyle.allCases, id: \.self) { style in
                    Button { state.preferences.style = style; state.save() } label: {
                        VStack(alignment: .leading, spacing: 8) {
                            HStack { Text(style.rawValue).font(.headline); Spacer(); Image(systemName: state.preferences.style == style ? "checkmark.circle.fill" : "circle").foregroundStyle(.red) }
                            Text(style == .raw ? "No AI editing" : style == .cleanup ? "Fix fillers and grammar" : style == .polish ? "Context-aware formatting" : "Use your own prompt").font(.caption).foregroundStyle(.secondary)
                        }.padding(18).frame(maxWidth: .infinity, alignment: .leading).background(state.preferences.style == style ? Color.red.opacity(0.05) : .white, in: RoundedRectangle(cornerRadius: 12)).overlay(RoundedRectangle(cornerRadius: 12).stroke(state.preferences.style == style ? Color.red : Color.gray.opacity(0.2)))
                    }.buttonStyle(.plain)
                }
            }
            if state.preferences.style == .polish {
                Picker("Wording", selection: $state.preferences.reworded) { Text("My words").tag(false); Text("Reworded").tag(true) }.pickerStyle(.segmented).onChange(of: state.preferences.reworded) { _ in state.save() }
            }
            if state.preferences.style == .custom { Text("Custom prompt").font(.headline); TextEditor(text: $state.preferences.custom).frame(height: 180); Button("Save prompt") { state.save() }.buttonStyle(.borderedProminent) }
        }
    }
    var models: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Speech-to-text").font(.headline)
            Picker("", selection: $state.preferences.speechModel) { ForEach(state.models.catalog.speech) { file in Text("\(file.title) · \(String(format: "%.2f", Double(file.bytes) / 1e9)) GB").tag(file.name) } }.labelsHidden()
            Text("Language model").font(.headline)
            Picker("", selection: $state.preferences.languageModel) { ForEach(state.models.catalog.language) { file in Text("\(file.title) · \(String(format: "%.2f", Double(file.bytes) / 1e9)) GB").tag(file.name) } }.labelsHidden()
            Text(state.status).foregroundStyle(.secondary)
            HStack { Button("Download & load models") { state.save(); Task { await state.loadModels(download: true) } }.buttonStyle(.borderedProminent); if state.modelBusy { ProgressView().controlSize(.small) } }
            Text("Local processing · Models stay ready").font(.caption).foregroundStyle(.secondary)
        }.disabled(state.modelBusy)
    }
    var shortcuts: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text("One shortcut").font(.headline)
            Button(state.capturingShortcut ? "Press your shortcut… (Esc cancels)" : state.preferences.shortcutLabel) { state.recordShortcut() }
            Text("Dictate in a text box. Select text to choose an AI action.").font(.caption).foregroundStyle(.secondary)
            Picker("Recording mode", selection: $state.preferences.recordingMode) { ForEach(RecordingMode.allCases, id: \.self) { Text($0.rawValue) } }.onChange(of: state.preferences.recordingMode) { _ in state.save() }
            TextField("Translation language", text: $state.preferences.translationLanguage).textFieldStyle(.roundedBorder)
            Button("Save") { state.save() }
            Button("Allow text-box access") { state.permissions() }
            Text("macOS Accessibility access enables selection capture and paste. Some apps expose limited text access; Copy remains available.").font(.caption).foregroundStyle(.secondary)
        }
    }
    var system: some View {
        VStack(alignment: .leading, spacing: 18) {
            Toggle("Always show dictation bar", isOn: $state.preferences.showBar).onChange(of: state.preferences.showBar) { _ in state.save() }
            Toggle("Save history locally", isOn: $state.preferences.saveHistory).onChange(of: state.preferences.saveHistory) { _ in state.save() }
            Button("Open data folder") { NSWorkspace.shared.open(state.root) }
            Text("No audio files are saved. Closing the window keeps the app in the menu bar.").font(.caption).foregroundStyle(.secondary)
        }
    }
}
@MainActor struct FloatingView: View {
    @ObservedObject var state: AppState
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Image(systemName: "mic.fill").foregroundStyle(.red)
                if state.phase == "ai" || state.phase == "transcribing" { ProgressView().controlSize(.small); Text(state.phase == "ai" ? "Adjusting with AI…" : "Transcribing…") }
                else { Text(state.phase == "recording" ? "Listening…" : "Open Mic AI").font(.headline) }
                Spacer(); Button("Open app") { state.showMain() }
                Button { state.cancel() } label: { Image(systemName: "xmark") }
            }
            if state.phase == "actions" {
                Text(state.selection).lineLimit(3).foregroundStyle(.secondary)
                HStack { ForEach(["Rewrite", "Translate", "Summarize"], id: \.self) { action in Button(action) { state.transformSelection(action) } }; Button("Speak") { state.startRecording(instruction: true) } }
                TextField("Translation language", text: $state.preferences.translationLanguage).textFieldStyle(.roundedBorder)
            } else if state.phase == "result" {
                Text(state.preview).font(.caption).foregroundStyle(.secondary)
                ScrollView { Text(state.result).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }.frame(maxHeight: 150)
                HStack { Button("Copy") { state.copy(state.result) }.disabled(state.result.isEmpty); Spacer(); Button("Dictate") { state.shortcut() } }
            } else {
                if state.phase == "recording" && !state.selection.isEmpty {
                    Text(state.selection).lineLimit(2).font(.caption).foregroundStyle(.secondary)
                    Text("Say rewrite, translate into a language, or summarize. Or dictate replacement text.").font(.caption).foregroundStyle(.secondary)
                    HStack { ForEach(["Rewrite", "Translate", "Summarize"], id: \.self) { action in Button(action) { state.transformSelection(action) } } }
                }
                if state.phase != "idle" { Text(state.preview).lineLimit(4).font(.callout) }
                if state.phase == "idle" || state.phase == "recording" { Button(state.phase == "recording" ? "Stop" : "Dictate") { if state.phase == "recording" { state.finishRecording() } else { state.shortcut() } }.buttonStyle(.borderedProminent) }
            }
            if !state.message.isEmpty { Text(state.message).font(.caption).foregroundStyle(.secondary).lineLimit(2) }
        }.padding(18).frame(width: 440).background(.white).tint(.red).preferredColorScheme(.light)
    }
}
