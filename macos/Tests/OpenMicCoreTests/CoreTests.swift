import XCTest
@testable import OpenMicCore

final class CoreTests: XCTestCase {
    func testFormattingAllowsPunctuationAndNumbering() {
        XCTAssertTrue(WordGuard.preservesWords(source: "first connect the cable then print a page", result: "1. First, connect the cable.\n2. Then print a page."))
        XCTAssertTrue(WordGuard.preservesWords(source: "I want a list with apples and two strawberries", result: "I want a list with:\n- Apples\n- Two strawberries"))
    }
    func testNoAddedWordsOrSubstitutions() {
        for (source, result) in [("The screen is good", "The screen is excellent"), ("I don't want folders", "I do not want folders"), ("Bring two folders", "Bring 2 folders"), ("Nina will call Alex", "Alex will call Nina"), ("Bring apples", "Grocery list\n- Apples"), ("Bring apples", "- Apples\n- Apples"), ("Use 7319", "Use 7318"), ("Bring folders", "1. Bring 2 folders")] {
            XCTAssertFalse(WordGuard.preservesWords(source: source, result: result), result)
        }
    }
    func testExistingHeadingsAndApostrophes() {
        XCTAssertTrue(WordGuard.preservesWords(source: "Project status the beta is ready", result: "Project status\n\nThe beta is ready."))
        XCTAssertTrue(WordGuard.preservesWords(source: "I'd like two folders", result: "I’d like two folders."))
        XCTAssertFalse(WordGuard.preservesWords(source: "Project status the beta is ready", result: "Project status\nProject status: the beta is ready."))
    }
    func testAutomaticRecording() {
        XCTAssertFalse(WordGuard.shouldStop(mode: .toggle, seconds: 30, speech: true, silence: 10))
        XCTAssertFalse(WordGuard.shouldStop(mode: .hold, seconds: 30, speech: true, silence: 10))
        XCTAssertTrue(WordGuard.shouldStop(mode: .automatic, seconds: 2, speech: true, silence: 1.4))
        XCTAssertTrue(WordGuard.shouldStop(mode: .automatic, seconds: 10, speech: false, silence: 10))
    }
    func testModesHaveDifferentInstructions() {
        XCTAssertTrue(Prompts.editing(style: .polish, reworded: false, custom: "").contains("No added words"))
        XCTAssertTrue(Prompts.editing(style: .polish, reworded: true, custom: "").contains("Reword for clarity"))
        XCTAssertEqual(Preferences().reworded, false)
        let value = Preferences()
        XCTAssertEqual(try JSONDecoder().decode(Preferences.self, from: JSONEncoder().encode(value)).style, .polish)
    }
}
