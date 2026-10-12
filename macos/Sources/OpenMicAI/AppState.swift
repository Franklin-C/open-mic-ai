import AppKit
import AVFoundation
import ApplicationServices
import Carbon
import OpenMicCore

@MainActor final class AppState: ObservableObject {
    @Published var preferences: Preferences
    @Published var history: [HistoryEntry]
    @Published var snippets: [Snippet]
    @Published var vocabulary: [VocabularyWord]
    @Published var page = "Home"
    @Published var section = "Style"
    @Published var phase = "idle"
    @Published var status = "Download models in Settings"
    @Published var preview = ""
    @Published var result = ""
    @Published var selection = ""
    @Published var modelBusy = false
    @Published var capturingShortcut = false
    @Published var message = ""
    let root: URL
    let models: LocalModels
    let hotkey = Hotkey()
    private let audio = AudioCapture()
    private var target: FocusTarget?
    private var recordingPreferences = Preferences()
    private var voiceInstruction = false
    private var epoch = UUID()
    private var timer: Timer?
    private var previewTask: Task<Void, Never>?
    private var work: Task<Void, Never>?
    private var shortcutMonitor: Any?
    private var previewBusy = false
    var showMain: () -> Void = {}
    var showBar: () -> Void = {}
    var hideBar: () -> Void = {}
    init() throws {
        let dataRoot = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("Open Mic AI")
        root = dataRoot
        try FileManager.default.createDirectory(at: dataRoot, withIntermediateDirectories: true)
        func read<T: Decodable>(_ name: String, _ fallback: T) -> T { (try? JSONDecoder().decode(T.self, from: Data(contentsOf: dataRoot.appendingPathComponent(name)))) ?? fallback }
        var recommended = Preferences()
        if ProcessInfo.processInfo.physicalMemory < 12 * 1_073_741_824 { recommended.speechModel = "ggml-base.en.bin"; recommended.languageModel = "Qwen3-1.7B-Q4_K_M.gguf" }
        preferences = read("preferences.json", recommended); history = read("history.json", []); snippets = read("snippets.json", []); vocabulary = read("vocabulary.json", [])
        models = try LocalModels(root: root)
        models.status = { [weak self] in self?.status = $0 }
        hotkey.pressed = { [weak self] in self?.shortcut() }
        hotkey.released = { [weak self] in if let self, self.phase == "recording", self.recordingPreferences.recordingMode == .hold { self.finishRecording() } }
        try hotkey.register(code: preferences.keyCode, modifiers: preferences.keyModifiers)
        Task { await loadModels(download: false) }
    }
    func write<T: Encodable>(_ name: String, _ value: T) {
        do { try JSONEncoder().encode(value).write(to: root.appendingPathComponent(name), options: .atomic) }
        catch { message = "Could not save locally: \(error.localizedDescription)" }
    }
    func save() { write("preferences.json", preferences); if preferences.showBar { showBar() } else if phase == "idle" { hideBar() } }
    func saveLibrary() { write("snippets.json", snippets); write("vocabulary.json", vocabulary) }
    func addHistory(source: String, result: String, action: String, seconds: Double = 0) {
        if preferences.saveHistory { history.insert(HistoryEntry(source: source, result: result, action: action, duration: seconds), at: 0); history = Array(history.prefix(200)); write("history.json", history) }
    }
    func copy(_ text: String) { NSPasteboard.general.clearContents(); NSPasteboard.general.setString(text, forType: .string); message = "Copied" }
    func loadModels(download: Bool) async {
        guard !modelBusy, phase != "recording", phase != "ai", phase != "transcribing" else { return }
        modelBusy = true; defer { modelBusy = false }
        do { try await models.load(preferences, download: download) } catch { status = error.localizedDescription }
    }
    func shortcut() {
        if phase == "recording" { if recordingPreferences.recordingMode != .hold { finishRecording() }; return }
        if phase == "actions" { startRecording(instruction: true); return }
        guard phase != "ai", phase != "transcribing", !modelBusy else { return }
        guard models.ready else { message = "Please download models from Settings first."; page = "Settings"; section = "Models"; showMain(); return }
        target = FocusTarget.capture()
        if target?.password == true { message = "Choose a text box outside a password field."; return }
        selection = target?.selected ?? ""
        if selection.count > 9000 { message = "Select up to 9,000 characters."; return }
        if !selection.isEmpty { phase = "actions"; preview = selection; result = ""; showBar(); return }
        startRecording(instruction: false)
    }
    func startRecording(instruction: Bool) {
        guard phase != "recording" else { return }
        if instruction && target?.matches(requireSelection: true) != true { cancel(); message = "Selection changed. Select the text again."; return }
        voiceInstruction = instruction; recordingPreferences = preferences; let current = UUID(); epoch = current
        work = Task {
            let allowed = await AVCaptureDevice.requestAccess(for: .audio)
            guard !Task.isCancelled, epoch == current else { return }
            guard allowed else { message = "Allow microphone access in macOS Privacy & Security."; return }
            do {
                try audio.start(); phase = "recording"; preview = "Listening…"; result = ""; previewBusy = false; showBar()
                timer?.invalidate(); var lastPreview = 0.0
                timer = Timer.scheduledTimer(withTimeInterval: 0.2, repeats: true) { [weak self] _ in
                    Task { @MainActor in
                        guard let self, self.phase == "recording", self.epoch == current else { return }
                        let state = self.audio.state()
                        if WordGuard.shouldStop(mode: self.recordingPreferences.recordingMode, seconds: state.seconds, speech: state.speech, silence: state.silence) || state.seconds >= 120 { self.finishRecording(); return }
                        if state.speech && state.seconds - lastPreview >= 2.5 && !self.previewBusy {
                            lastPreview = state.seconds; self.previewBusy = true; let wav = self.audio.wav()
                            self.previewTask = Task {
                                defer { if self.epoch == current { self.previewBusy = false } }
                                if let text = try? await self.models.transcribe(wav, prompt: self.vocabulary.map(\.written).joined(separator: ", ")), self.epoch == current, self.phase == "recording", !Task.isCancelled { self.preview = text }
                            }
                        }
                    }
                }
            } catch { message = error.localizedDescription; phase = "idle" }
        }
    }
    func expand(_ text: String) -> String {
        var value = text
        for word in vocabulary where !word.heard.isEmpty { value = value.replacingOccurrences(of: "(?i)\\b\(NSRegularExpression.escapedPattern(for: word.heard))\\b", with: NSRegularExpression.escapedTemplate(for: word.written), options: .regularExpression) }
        for snippet in snippets.sorted(by: { $0.trigger.count > $1.trigger.count }) where !snippet.trigger.isEmpty { value = value.replacingOccurrences(of: "(?i)\\b\(NSRegularExpression.escapedPattern(for: snippet.trigger))\\b", with: NSRegularExpression.escapedTemplate(for: snippet.text), options: .regularExpression) }
        return value
    }
    func finishRecording() {
        guard phase == "recording" else { return }
        timer?.invalidate(); timer = nil; audio.stop(); previewTask?.cancel(); epoch = UUID()
        let wav = audio.wav(), seconds = audio.state().seconds, speech = audio.state().speech
        guard speech else { phase = "result"; preview = "No speech detected"; result = ""; return }
        phase = "transcribing"; let current = epoch
        work = Task {
            do {
                let transcript = try await models.transcribe(wav, prompt: vocabulary.map(\.written).joined(separator: ", "))
                try Task.checkCancellation(); guard epoch == current else { return }
                guard !transcript.isEmpty else { phase = "result"; preview = "No speech detected"; return }
                preview = transcript; phase = "ai"
                if voiceInstruction {
                    var action = "Rewrite", prefs = recordingPreferences
                    if transcript.lowercased().hasPrefix("translate") {
                        action = "Translate"
                        if let range = transcript.range(of: #"(?i)\b(?:into|to|in)\s+([A-Za-z][A-Za-z -]{0,50})"#, options: .regularExpression) {
                            let clause = String(transcript[range]); prefs.translationLanguage = clause.replacingOccurrences(of: #"(?i)^(?:into|to|in)\s+"#, with: "", options: .regularExpression).trimmingCharacters(in: .whitespacesAndNewlines)
                        }
                    } else if transcript.lowercased().hasPrefix("summar") { action = "Summarize" }
                    let output = try await models.transform(selection, action: action, preferences: prefs, spoken: transcript)
                    await deliver(source: selection, output: output, action: action, target: target, current: current)
                } else {
                    let output = try await models.transform(expand(transcript), action: "Dictate", preferences: recordingPreferences)
                    await deliver(source: transcript, output: output, action: "Dictate", target: target, current: current, seconds: seconds)
                }
            } catch { if epoch == current, !Task.isCancelled { phase = "result"; result = voiceInstruction ? selection : preview; message = error.localizedDescription } }
        }
    }
    func transformSelection(_ action: String) {
        guard phase == "actions", target?.matches(requireSelection: true) == true else { cancel(); message = "Selection changed. Select it again."; return }
        phase = "ai"; let source = selection, target = target, current = epoch, prefs = preferences
        work = Task {
            do { let output = try await models.transform(source, action: action, preferences: prefs); await deliver(source: source, output: output, action: action, target: target, current: current) }
            catch { if epoch == current, !Task.isCancelled { phase = "result"; result = source; message = error.localizedDescription } }
        }
    }
    func deliver(source: String, output: String, action: String, target: FocusTarget?, current: UUID, seconds: Double = 0) async {
        guard !Task.isCancelled, epoch == current else { return }
        result = output; addHistory(source: source, result: output, action: action, seconds: seconds)
        var pasted = false
        if action == "Dictate" || action == "Rewrite", let target { pasted = await target.paste(output, replacing: action == "Rewrite") }
        guard epoch == current else { return }
        preview = action == "Translate" || action == "Summarize" ? action : pasted ? "Pasted" : "Ready to copy"
        phase = "result"; showBar()
    }
    func cancel() { epoch = UUID(); work?.cancel(); previewTask?.cancel(); timer?.invalidate(); timer = nil; audio.stop(); phase = "idle"; preview = ""; result = ""; selection = ""; if !preferences.showBar { hideBar() } }
    func permissions() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
    }
    func recordShortcut() {
        cancelShortcut(); capturingShortcut = true; hotkey.capturing = true
        shortcutMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            guard let self else { return event }
            if event.keyCode == 53 { self.cancelShortcut(); return nil }
            let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
            guard !flags.intersection([.command, .control, .option]).isEmpty else { self.message = "Use Command, Control or Option with a key."; return nil }
            var modifiers: UInt32 = 0; var labels = [String]()
            for (flag, carbon, label) in [(NSEvent.ModifierFlags.control, UInt32(controlKey), "Control"), (.option, UInt32(optionKey), "Option"), (.shift, UInt32(shiftKey), "Shift"), (.command, UInt32(cmdKey), "Command")] {
                if flags.contains(flag) { modifiers |= carbon; labels.append(label) }
            }
            labels.append(event.keyCode == 49 ? "Space" : (event.charactersIgnoringModifiers ?? "Key \(event.keyCode)").uppercased())
            let old = self.preferences
            do { try self.hotkey.register(code: UInt32(event.keyCode), modifiers: modifiers); self.preferences.keyCode = UInt32(event.keyCode); self.preferences.keyModifiers = modifiers; self.preferences.shortcutLabel = labels.joined(separator: " + "); self.save() }
            catch { try? self.hotkey.register(code: old.keyCode, modifiers: old.keyModifiers); self.message = error.localizedDescription }
            self.cancelShortcut(); return nil
        }
    }
    func cancelShortcut() { if let shortcutMonitor { NSEvent.removeMonitor(shortcutMonitor) }; shortcutMonitor = nil; capturingShortcut = false; hotkey.capturing = false }
    func shutdown() { cancel(); cancelShortcut(); models.shutdown() }
}
