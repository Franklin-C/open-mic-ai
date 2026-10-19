import AppKit
import OpenMicCore

final class MouseShortcutInput {
    private var tap: CFMachPort?
    private var source: CFRunLoopSource?
    private var state: MouseShortcutState
    var enabled = true
    private let pressed: @MainActor () -> Void
    private let released: @MainActor () -> Void
    private let captured: @MainActor (Int) -> Void
    init(button: Int? = nil, capture: Bool = false, pressed: @escaping @MainActor () -> Void = {}, released: @escaping @MainActor () -> Void = {}, captured: @escaping @MainActor (Int) -> Void = { _ in }) throws {
        state = MouseShortcutState(selected: button, capturing: capture)
        self.pressed = pressed; self.released = released; self.captured = captured
        let mask = CGEventMask(1) << CGEventType.otherMouseDown.rawValue | CGEventMask(1) << CGEventType.otherMouseUp.rawValue
        guard let tap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .defaultTap, eventsOfInterest: mask, callback: { _, type, event, pointer in
            guard let pointer else { return Unmanaged.passUnretained(event) }
            let owner = Unmanaged<MouseShortcutInput>.fromOpaque(pointer).takeUnretainedValue()
            if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
                if let tap = owner.tap { CGEvent.tapEnable(tap: tap, enable: true) }
                return Unmanaged.passUnretained(event)
            }
            guard type == .otherMouseDown || type == .otherMouseUp else { return Unmanaged.passUnretained(event) }
            let action = owner.state.process(button: Int(event.getIntegerValueField(.mouseEventButtonNumber)), down: type == .otherMouseDown, enabled: owner.enabled)
            switch action {
            case .pass: return Unmanaged.passUnretained(event)
            case .consumed: break
            case .pressed: DispatchQueue.main.async { [weak owner] in owner?.pressed() }
            case .released: DispatchQueue.main.async { [weak owner] in owner?.released() }
            case .captured(let button): DispatchQueue.main.async { [weak owner] in owner?.captured(button) }
            }
            return nil
        }, userInfo: Unmanaged.passUnretained(self).toOpaque()) else { throw MicError.message("Allow text-box access to use middle and side mouse shortcuts.") }
        self.tap = tap
        guard let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0) else { stop(); throw MicError.message("Could not enable mouse shortcuts.") }
        self.source = source; CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes); CGEvent.tapEnable(tap: tap, enable: true)
    }
    func stop() { if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }; source = nil; if let tap { CFMachPortInvalidate(tap) }; tap = nil }
    deinit { stop() }
}
