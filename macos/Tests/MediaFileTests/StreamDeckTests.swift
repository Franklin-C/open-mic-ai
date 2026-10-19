import XCTest
@testable import OpenMicAI

final class StreamDeckTests: XCTestCase {
    @MainActor func testAuthenticatedLocalControls() async throws {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("OpenMicAI-deck-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: folder) }
        var commands = [String]()
        let bridge = try StreamDeckBridge(root: folder) { command in commands.append(command); return ["ok": true, "phase": "idle", "ready": true] }
        defer { bridge.stop() }
        let configURL = folder.appendingPathComponent("streamdeck.json")
        for _ in 0..<100 { if FileManager.default.fileExists(atPath: configURL.path) { break }; try await Task.sleep(nanoseconds: 20_000_000) }
        let config = try XCTUnwrap(try JSONSerialization.jsonObject(with: Data(contentsOf: configURL)) as? [String: Any])
        let port = try XCTUnwrap(config["port"] as? Int), token = try XCTUnwrap(config["token"] as? String)
        XCTAssertEqual(token.count, 64)
        let permissions = try FileManager.default.attributesOfItem(atPath: configURL.path)[.posixPermissions] as? NSNumber
        XCTAssertEqual(permissions?.intValue, 0o600)
        func send(_ command: String? = nil, authorization: String? = nil, origin: String? = nil) async throws -> Int {
            var request = URLRequest(url: URL(string: "http://127.0.0.1:\(port)/\(command == nil ? "status" : "command")")!)
            request.setValue("Bearer \(authorization ?? token)", forHTTPHeaderField: "Authorization")
            if let origin { request.setValue(origin, forHTTPHeaderField: "Origin") }
            if let command { request.httpMethod = "POST"; request.httpBody = try JSONSerialization.data(withJSONObject: ["command": command]) }
            let (_, response) = try await URLSession.shared.data(for: request)
            return (response as! HTTPURLResponse).statusCode
        }
        let status = try await send(); XCTAssertEqual(status, 200)
        let wrong = try await send(authorization: "wrong"); XCTAssertEqual(wrong, 403)
        let origin = try await send(origin: "https://example.test"); XCTAssertEqual(origin, 403)
        let unknown = try await send("shell"); XCTAssertEqual(unknown, 400)
        for command in ["toggle", "hold.down", "hold.up", "copy", "open", "cancel"] { let code = try await send(command); XCTAssertEqual(code, 200) }
        XCTAssertEqual(commands, ["status", "toggle", "hold.down", "hold.up", "copy", "open", "cancel"])
        bridge.stop()
        let closed = try XCTUnwrap(try JSONSerialization.jsonObject(with: Data(contentsOf: configURL)) as? [String: Any]); XCTAssertEqual(closed["port"] as? Int, 0)
    }
}
