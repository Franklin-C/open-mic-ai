import AppKit
import ApplicationServices

@MainActor final class FocusTarget {
    let pid: pid_t
    let element: AXUIElement
    let selected: String
    let range: CFRange?
    let password: Bool
    static func attribute(_ element: AXUIElement, _ name: String) -> CFTypeRef? {
        var value: CFTypeRef?
        return AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success ? value : nil
    }
    static func selectionRange(_ element: AXUIElement) -> CFRange? {
        guard let value = attribute(element, kAXSelectedTextRangeAttribute), CFGetTypeID(value) == AXValueGetTypeID() else { return nil }
        var range = CFRange(); return AXValueGetValue(value as! AXValue, .cfRange, &range) ? range : nil
    }
    static func capture() -> FocusTarget? {
        guard AXIsProcessTrusted(), let application = NSWorkspace.shared.frontmostApplication,
              application.processIdentifier != ProcessInfo.processInfo.processIdentifier else { return nil }
        let app = AXUIElementCreateApplication(application.processIdentifier)
        guard let value = attribute(app, kAXFocusedUIElementAttribute), CFGetTypeID(value) == AXUIElementGetTypeID() else { return nil }
        return FocusTarget(pid: application.processIdentifier, element: value as! AXUIElement)
    }
    init(pid: pid_t, element: AXUIElement) {
        self.pid = pid; self.element = element
        let role = Self.attribute(element, kAXSubroleAttribute) as? String
        password = role == kAXSecureTextFieldSubrole
        selected = password ? "" : (Self.attribute(element, kAXSelectedTextAttribute) as? String ?? "")
        range = Self.selectionRange(element)
    }
    func matches(requireSelection: Bool) -> Bool {
        guard !password, let current = Self.capture(), current.pid == pid, CFEqual(current.element, element) else { return false }
        if let range { guard let now = current.range, now.location == range.location, now.length == range.length else { return false } }
        return !requireSelection || (!selected.isEmpty && current.selected == selected)
    }
    func paste(_ text: String, replacing: Bool) async -> Bool {
        guard matches(requireSelection: replacing) else { return false }
        var writable: DarwinBoolean = false
        if AXUIElementIsAttributeSettable(element, kAXSelectedTextAttribute as CFString, &writable) == .success, writable.boolValue {
            return AXUIElementSetAttributeValue(element, kAXSelectedTextAttribute as CFString, text as CFString) == .success
        }
        // Do not send keystrokes to a read-only field or an unsupported target.
        guard let editable = Self.attribute(element, kAXEnabledAttribute) as? Bool, editable,
              let role = Self.attribute(element, kAXRoleAttribute) as? String,
              role == kAXTextFieldRole || role == kAXTextAreaRole, range != nil else { return false }
        guard AXUIElementIsAttributeSettable(element, kAXValueAttribute as CFString, &writable) == .success, writable.boolValue else { return false }
        let clipboard = NSPasteboard.general
        let backup = (clipboard.pasteboardItems ?? []).map { item -> NSPasteboardItem in
            let copy = NSPasteboardItem(); for type in item.types { if let data = item.data(forType: type) { copy.setData(data, forType: type) } }; return copy
        }
        clipboard.clearContents(); clipboard.setString(text, forType: .string); let sequence = clipboard.changeCount
        guard matches(requireSelection: replacing), let source = CGEventSource(stateID: .combinedSessionState), let down = CGEvent(keyboardEventSource: source, virtualKey: 9, keyDown: true), let up = CGEvent(keyboardEventSource: source, virtualKey: 9, keyDown: false) else {
            if clipboard.changeCount == sequence { clipboard.clearContents(); clipboard.writeObjects(backup) }; return false
        }
        down.flags = .maskCommand; up.flags = .maskCommand; down.post(tap: .cghidEventTap); up.post(tap: .cghidEventTap)
        try? await Task.sleep(nanoseconds: 350_000_000)
        if clipboard.changeCount == sequence { clipboard.clearContents(); clipboard.writeObjects(backup) }
        return true
    }
}
