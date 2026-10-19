import XCTest
@testable import OpenMicCore

final class PreferencesTests: XCTestCase {
    func testSettingsGroupsKeepRelatedPagesTogether() {
        XCTAssertEqual(SettingsNavigation.groups, ["Writing", "Access", "System", "Integrations"])
        XCTAssertEqual(SettingsNavigation.pages("Writing"), ["Style", "Snippets", "Vocabulary"])
        XCTAssertEqual(SettingsNavigation.pages("Access"), ["Shortcuts", "Dictation bar"])
        XCTAssertEqual(SettingsNavigation.pages("System"), ["General", "Models"])
        for group in SettingsNavigation.groups {
            for page in SettingsNavigation.pages(group) { XCTAssertEqual(SettingsNavigation.group(for: page), group) }
        }
        XCTAssertEqual(SettingsNavigation.group(for: "Integrations"), "Integrations")
    }
    func testOldSettingsKeepPersonalChoices() throws {
        let data = Data(#"{"style":"Custom","custom":"Use short paragraphs","recordingMode":"Automatic","keyCode":29,"keyModifiers":4096,"shortcutLabel":"Control + 0","speechModel":"chosen-speech.bin","languageModel":"chosen-llm.gguf","saveHistory":false,"showBar":false,"translationLanguage":"Spanish"}"#.utf8)
        let preferences = try JSONDecoder().decode(Preferences.self, from: data)
        XCTAssertEqual(preferences.barLayout, .horizontal)
        XCTAssertFalse(preferences.showBar)
        XCTAssertFalse(preferences.saveHistory)
        XCTAssertEqual(preferences.style, .custom)
        XCTAssertEqual(preferences.custom, "Use short paragraphs")
        XCTAssertEqual(preferences.recordingMode, .automatic)
        XCTAssertEqual(preferences.keyCode, 29)
        XCTAssertEqual(preferences.keyModifiers, 4096)
        XCTAssertEqual(preferences.shortcutLabel, "Control + 0")
        XCTAssertEqual(preferences.speechModel, "chosen-speech.bin")
        XCTAssertEqual(preferences.languageModel, "chosen-llm.gguf")
        XCTAssertEqual(preferences.translationLanguage, "Spanish")
    }
    func testLayoutsRoundTrip() throws {
        for layout in BarLayout.allCases {
            var preferences = Preferences()
            preferences.barLayout = layout
            let restored = try JSONDecoder().decode(Preferences.self, from: JSONEncoder().encode(preferences))
            XCTAssertEqual(restored.barLayout, layout)
            XCTAssertEqual(restored.speechModel, preferences.speechModel)
            XCTAssertGreaterThan(layout.idleHeight, 0)
            XCTAssertGreaterThan(layout.idleWidth, 0)
        }
        XCTAssertEqual(BarLayout.vertical.idleWidth, 44)
        XCTAssertEqual(BarLayout.horizontal.idleWidth, 204)
    }
    func testMissingOrUnknownLayoutUsesDefault() throws {
        let empty = try JSONDecoder().decode(Preferences.self, from: Data("{}".utf8))
        XCTAssertEqual(empty.barLayout, .horizontal)
        XCTAssertTrue(empty.showBar)
        let unknown = try JSONDecoder().decode(Preferences.self, from: Data(#"{"barLayout":"Future layout","showBar":false}"#.utf8))
        XCTAssertEqual(unknown.barLayout, .horizontal)
        XCTAssertFalse(unknown.showBar)
    }
}
