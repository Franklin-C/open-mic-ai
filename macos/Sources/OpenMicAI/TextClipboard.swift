import AppKit
import OpenMicCore

enum TextClipboard {
    static func set(_ text: String) {
        let clipboard = NSPasteboard.general
        clipboard.clearContents(); clipboard.setString(text, forType: .string)
        if let html = TableFormatting.html(text) { clipboard.setString(html, forType: .html) }
    }
}
