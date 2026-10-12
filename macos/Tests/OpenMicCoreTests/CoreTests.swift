import XCTest
@testable import OpenMicCore

final class CoreTests: XCTestCase {
    func testWritingFormatsAndTables() throws {
        XCTAssertEqual(PolishLayout.choices.count, 20)
        XCTAssertEqual(PolishLayout.selected("Table"), "table")
        XCTAssertNil(PolishLayout.selected("Auto"))
        XCTAssertEqual(PolishLayout.explicit("Travel checklist. Pack the passport."), "checklist")
        XCTAssertNil(PolishLayout.explicit("The cafe had tables and chairs."))
        let rendered = try XCTUnwrap(TableFormatting.render(#"{"before":"Comparison table. Plan price storage. Basic five dollars ten gigabytes.","headers":["Plan","price","storage"],"rows":[["Basic","five dollars","ten gigabytes"]],"after":""}"#))
        XCTAssertTrue(PolishLayout.fits("table", rendered))
        XCTAssertTrue(PolishLayout.complete(source: "Comparison table. Plan price storage. Basic five dollars ten gigabytes.", result: rendered))
        XCTAssertNil(TableFormatting.render(#"{"before":"","headers":["A","B"],"rows":[["One"]],"after":""}"#))
        let html = try XCTUnwrap(TableFormatting.html("| Name | Item |\n| --- | --- |\n| Zoë | <script>& café |"))
        XCTAssertTrue(html.contains("&lt;script&gt;&amp;")); XCTAssertFalse(html.contains("<script>"))
        XCTAssertNil(TableFormatting.html("Plain sentence."))
        let checklist = try XCTUnwrap(WordLayout.project(source: "Travel checklist pack the passport and charge the camera before Friday.", layout: "Travel checklist\n\n- [ ] Pack the passport\n- [ ] Charge the camera before Friday."))
        XCTAssertTrue(PolishLayout.fits("checklist", checklist))
        XCTAssertTrue(PolishLayout.complete(source: "Travel checklist pack the passport and charge the camera before Friday.", result: checklist))
        var preferences = Preferences(); preferences.polishFormat = "Checklist"
        XCTAssertEqual(try JSONDecoder().decode(Preferences.self, from: JSONEncoder().encode(preferences)).polishFormat, "Checklist")
    }
    func testContextualLayoutAndCorrections() {
        XCTAssertEqual(PolishLayout.prepare("Add bananas, pears, three apples. Actually two bananas."), "Add two bananas, pears, three apples.")
        XCTAssertEqual(PolishLayout.prepare("Add bananas, pears. Actually two bananas. Actually make that three bananas."), "Add three bananas, pears.")
        XCTAssertTrue(PolishLayout.prepare("We need apples. Actually two pears.").contains("Actually two pears"))
        XCTAssertTrue(PolishLayout.fits("goals", "My goals:\n- Walk\n- Read"))
        XCTAssertFalse(PolishLayout.fits("goals", "Walk and read."))
        XCTAssertTrue(PolishLayout.fits("recipe", "1. Wash the rice.\n2. Simmer for fifteen minutes."))
        XCTAssertFalse(PolishLayout.fits("sections", "1. The model is ready.\n\n2. The export fails."))
        XCTAssertTrue(PolishLayout.fits("paragraphs", "The first idea.\n\nThe next idea."))
    }
    func testSourceWordLayoutRecovery() throws {
        let source = "I want to make a grocery list with apples and two strawberries."
        let projected = try XCTUnwrap(WordLayout.project(source: source, layout: "Grocery list\n\n- Apples\n- Two strawberries\n\nEnjoy!"))
        XCTAssertTrue(WordGuard.preservesWords(source: source, result: projected))
        XCTAssertTrue(PolishLayout.complete(source: source, result: projected))
        XCTAssertTrue(projected.contains("I want")); XCTAssertFalse(projected.contains("Enjoy"))
        let recipe = "So to make this recipe you start by washing the rice then you put it in a pan with water and then bring it to the boil reduce the heat and simmer it for fifteen minutes and don't open the lid while it's cooking."
        let steps = try XCTUnwrap(WordLayout.project(source: recipe, layout: "1. Wash the rice.\n2. Put the rice in a pan with water.\n3. Bring the mixture to a boil.\n4. Reduce the heat and simmer it for fifteen minutes.\n5. Do not open the lid while it's cooking."))
        XCTAssertTrue(PolishLayout.fits("recipe", steps)); XCTAssertTrue(PolishLayout.complete(source: recipe, result: steps)); XCTAssertTrue(WordGuard.preservesWords(source: recipe, result: steps))
        XCTAssertTrue(steps.contains("fifteen")); XCTAssertTrue(steps.contains("don't"))
    }
    func testImportedTextOverlapAndLongEditing() {
        XCTAssertEqual(MediaText.join("Please send the report tomorrow.", "report tomorrow and call Sarah.", overlap: true), "Please send the report tomorrow. and call Sarah.")
        XCTAssertEqual(MediaText.join("Please call Sarah", "Sarah will reply", overlap: true), "Please call Sarah Sarah will reply")
        XCTAssertEqual(MediaText.join("Say hello", "Say hello", overlap: false), "Say hello Say hello")
        let source = String(repeating: "Please keep every sentence. ", count: 1000)
        let parts = MediaText.parts(source)
        XCTAssertTrue(parts.allSatisfy { $0.count <= 4500 })
        XCTAssertEqual(WordGuard.words(parts.joined(separator: " ")), WordGuard.words(source))
    }
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
        XCTAssertEqual(Preferences().keyModifiers, 4608)
        let value = Preferences()
        XCTAssertEqual(try JSONDecoder().decode(Preferences.self, from: JSONEncoder().encode(value)).style, .polish)
    }
}
