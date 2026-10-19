import XCTest
@testable import OpenMicCore

final class MouseShortcutTests: XCTestCase {
    func testCaptureConsumesPressAndReleaseForEachButton() {
        for choice in MouseShortcut.allCases {
            var state = MouseShortcutState(capturing: true)
            XCTAssertEqual(state.process(button: 0, down: true), .pass)
            XCTAssertEqual(state.process(button: 1, down: true), .pass)
            XCTAssertEqual(state.process(button: choice.rawValue, down: true), .consumed)
            XCTAssertEqual(state.process(button: choice.rawValue, down: true), .consumed)
            XCTAssertEqual(state.process(button: choice.rawValue, down: false), .captured(choice.rawValue))
            XCTAssertFalse(state.capturing)
            XCTAssertEqual(state.process(button: choice.rawValue, down: false), .pass)
        }
    }
    func testRuntimeHoldAndToggleUseExactlyOnePressAndRelease() {
        for choice in MouseShortcut.allCases {
            var state = MouseShortcutState(selected: choice.rawValue)
            XCTAssertEqual(state.process(button: choice.rawValue, down: false), .pass)
            XCTAssertEqual(state.process(button: choice.rawValue, down: true), .pressed)
            XCTAssertEqual(state.process(button: choice.rawValue, down: true), .consumed)
            XCTAssertEqual(state.process(button: 8, down: true), .pass)
            XCTAssertEqual(state.process(button: choice.rawValue, down: false), .released)
            XCTAssertEqual(state.process(button: choice.rawValue, down: false), .pass)
            XCTAssertEqual(state.process(button: choice.rawValue, down: true, enabled: false), .pass)
        }
    }
    func testCaptureCanChangeThePreviouslySelectedButton() {
        var state = MouseShortcutState(selected: 3, capturing: true)
        XCTAssertEqual(state.process(button: 2, down: true), .consumed)
        XCTAssertEqual(state.process(button: 2, down: false), .captured(2))
    }
    func testMousePreferenceMigrationAndRoundTrip() throws {
        let old = try JSONDecoder().decode(Preferences.self, from: Data(#"{"keyCode":29,"shortcutLabel":"Control + 0"}"#.utf8))
        XCTAssertNil(old.mouseButton)
        XCTAssertEqual(old.keyCode, 29)
        for choice in MouseShortcut.allCases {
            var preferences = old
            preferences.mouseButton = choice.rawValue; preferences.shortcutLabel = choice.label
            let restored = try JSONDecoder().decode(Preferences.self, from: JSONEncoder().encode(preferences))
            XCTAssertEqual(restored.mouseButton, choice.rawValue)
            XCTAssertEqual(restored.shortcutLabel, choice.label)
            XCTAssertEqual(restored.keyCode, 29)
        }
        let unknown = try JSONDecoder().decode(Preferences.self, from: Data(#"{"mouseButton":0}"#.utf8))
        XCTAssertNil(unknown.mouseButton)
    }
}
