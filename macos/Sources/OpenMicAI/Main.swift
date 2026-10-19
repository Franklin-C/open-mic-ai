import AppKit
import SwiftUI

@MainActor final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    var state: AppState!
    var window: NSWindow!
    var panel: NSPanel!
    var item: NSStatusItem!
    func applicationDidFinishLaunching(_ notification: Notification) {
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1060, height: 720), styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = "Open Mic AI"; window.minSize = NSSize(width: 960, height: 650); window.isReleasedWhenClosed = false; window.delegate = self
        do { state = try AppState() }
        catch {
            window.contentView = NSHostingView(rootView: VStack(alignment: .leading, spacing: 16) { Text("Open Mic AI could not start").font(.title.bold()); Text(error.localizedDescription).textSelection(.enabled); Text("Close the app, fix access to its data folder, and reopen it.").foregroundStyle(.secondary) }.padding(32).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading).background(.white).preferredColorScheme(.light))
            window.center(); openApp(); return
        }
        window.contentView = NSHostingView(rootView: MainView(state: state)); window.center(); window.setFrameAutosaveName("OpenMicAI-main")
        panel = NSPanel(contentRect: NSRect(x: 100, y: 100, width: 476, height: 260), styleMask: [.nonactivatingPanel, .titled, .fullSizeContentView], backing: .buffered, defer: false)
        panel.titleVisibility = .hidden; panel.titlebarAppearsTransparent = true; panel.isMovableByWindowBackground = true; panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]; panel.hidesOnDeactivate = false; panel.isReleasedWhenClosed = false
        panel.contentView = NSHostingView(rootView: FloatingView(state: state)); panel.setFrameAutosaveName("OpenMicAI-bar")
        state.showMain = { [weak self] in self?.openApp() }; state.showBar = { [weak self] in self?.presentBar() }; state.hideBar = { [weak self] in self?.panel.orderOut(nil) }
        for kind in [NSWindow.ButtonType.closeButton, .miniaturizeButton, .zoomButton] { panel.standardWindowButton(kind)?.isHidden = true }
        item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength); item.button?.image = NSImage(systemSymbolName: "mic.fill", accessibilityDescription: "Open Mic AI")
        let menu = NSMenu(); menu.addItem(withTitle: "Open app", action: #selector(openApp), keyEquivalent: ""); menu.addItem(withTitle: "Dictate", action: #selector(dictate), keyEquivalent: ""); menu.addItem(.separator()); menu.addItem(withTitle: "Quit Open Mic AI", action: #selector(quit), keyEquivalent: "q"); item.menu = menu
        let appMenu = NSMenu(); let appItem = NSMenuItem(); appMenu.addItem(appItem); appItem.submenu = menu.copy() as? NSMenu
        let editItem = NSMenuItem(title: "Edit", action: nil, keyEquivalent: ""); let edit = NSMenu(title: "Edit")
        edit.addItem(withTitle: "Copy", action: #selector(NSText.copy(_:)), keyEquivalent: "c"); edit.addItem(withTitle: "Paste", action: #selector(NSText.paste(_:)), keyEquivalent: "v"); edit.addItem(withTitle: "Select All", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a"); editItem.submenu = edit; appMenu.addItem(editItem); NSApplication.shared.mainMenu = appMenu
        if !CommandLine.arguments.contains("--background") { openApp() }; if state.preferences.showBar { presentBar() }
    }
    @objc func openApp() { window.makeKeyAndOrderFront(nil); NSApplication.shared.activate(ignoringOtherApps: true) }
    func presentBar() {
        let old = panel.frame
        let compact = state.phase == "idle"
        panel.setContentSize(NSSize(width: compact ? 204 : 440, height: compact ? 46 : 310))
        let screen = panel.screen?.visibleFrame ?? NSScreen.main?.visibleFrame ?? old
        let x = max(screen.minX, min(old.midX-panel.frame.width/2, screen.maxX-panel.frame.width))
        let y = max(screen.minY, min(old.minY, screen.maxY-panel.frame.height))
        panel.setFrameOrigin(NSPoint(x: x, y: y)); panel.orderFrontRegardless()
    }
    @objc func dictate() { state.shortcut() }
    @objc func quit() { NSApplication.shared.terminate(nil) }
    func windowShouldClose(_ sender: NSWindow) -> Bool { if state == nil { return true }; sender.orderOut(nil); return false }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { state == nil }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool { openApp(); return true }
    func applicationWillTerminate(_ notification: Notification) { state?.shutdown() }
}
@main enum Launcher {
    @MainActor static func main() {
        if CommandLine.arguments.contains("--self-test") {
            do {
                let catalog = try ModelCatalog.load()
                guard catalog.speech.count == 2, catalog.language.count == 6 else { throw MicError.message("The bundled model catalog is incomplete.") }
                let runtime = Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/runtime")
                for name in ["whisper-server", "llama-server"] { guard FileManager.default.isExecutableFile(atPath: runtime.appendingPathComponent(name).path) else { throw MicError.message("Missing bundled engine: \(name)") } }
                print("PASS packaged app resources and both local engines")
            } catch { fputs("Package check failed: \(error.localizedDescription)\n", stderr); exit(1) }
            return
        }
        let application = NSApplication.shared
        let delegate = AppDelegate(); application.delegate = delegate
        application.setActivationPolicy(.regular)
        application.run()
        withExtendedLifetime(delegate) {}
    }
}
