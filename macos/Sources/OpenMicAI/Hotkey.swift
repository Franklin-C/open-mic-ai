import AppKit
import Carbon

@MainActor final class Hotkey {
    private var reference: EventHotKeyRef?
    private var handler: EventHandlerRef?
    var pressed: () -> Void = {}
    var released: () -> Void = {}
    var capturing = false
    init() {
        var events = [EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed)), EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyReleased))]
        let pointer = Unmanaged.passUnretained(self).toOpaque()
        InstallEventHandler(GetApplicationEventTarget(), { _, event, pointer -> OSStatus in
            guard let pointer, let event else { return OSStatus(eventNotHandledErr) }
            let owner = Unmanaged<Hotkey>.fromOpaque(pointer).takeUnretainedValue()
            let released = GetEventKind(event) == UInt32(kEventHotKeyReleased)
            DispatchQueue.main.async { if !owner.capturing { if released { owner.released() } else { owner.pressed() } } }
            return noErr
        }, UInt32(events.count), &events, pointer, &handler)
    }
    func register(code: UInt32, modifiers: UInt32) throws {
        let identifier = EventHotKeyID(signature: 0x4F4D4943, id: 1)
        var next: EventHotKeyRef?
        if reference != nil { UnregisterEventHotKey(reference!); reference = nil }
        guard RegisterEventHotKey(code, modifiers, identifier, GetApplicationEventTarget(), 0, &next) == noErr else { throw MicError.message("That shortcut is unavailable. Choose another combination.") }
        reference = next
    }
    deinit { if let reference { UnregisterEventHotKey(reference) }; if let handler { RemoveEventHandler(handler) } }
}
