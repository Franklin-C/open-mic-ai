import AppKit
import Network
import Darwin

@MainActor final class StreamDeckBridge {
    private var listener: NWListener?
    private let root: URL
    private let application: String
    private let token = UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased() + UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
    private let command: (String) -> [String: Any]
    init(root: URL, command: @escaping (String) -> [String: Any]) throws {
        self.root = root; self.command = command; application = Bundle.main.bundleURL.path
        let parameters = NWParameters.tcp
        parameters.requiredLocalEndpoint = .hostPort(host: "127.0.0.1", port: .any)
        let listener = try NWListener(using: parameters)
        self.listener = listener
        listener.stateUpdateHandler = { [weak self, weak listener] state in
            Task { @MainActor in if case .ready = state, let port = listener?.port { try? self?.publish(Int(port.rawValue)) } }
        }
        listener.newConnectionHandler = { [weak self] connection in Task { @MainActor in self?.serve(connection) } }
        listener.start(queue: .main)
    }
    private func publish(_ port: Int) throws {
        let data = try JSONSerialization.data(withJSONObject: ["port": port, "token": token, "application": application, "version": 1])
        let temporary = root.appendingPathComponent("streamdeck-\(UUID().uuidString).tmp")
        let descriptor = Darwin.open(temporary.path, O_WRONLY | O_CREAT | O_EXCL, S_IRUSR | S_IWUSR)
        guard descriptor >= 0 else { throw CocoaError(.fileWriteNoPermission) }
        let file = FileHandle(fileDescriptor: descriptor, closeOnDealloc: true)
        defer { try? file.close(); try? FileManager.default.removeItem(at: temporary) }
        try file.write(contentsOf: data)
        guard Darwin.rename(temporary.path, root.appendingPathComponent("streamdeck.json").path) == 0 else { throw CocoaError(.fileWriteUnknown) }
    }
    private func serve(_ connection: NWConnection) {
        connection.start(queue: .main)
        DispatchQueue.main.asyncAfter(deadline: .now() + 2) { connection.cancel() }
        receive(connection, data: Data())
    }
    private func receive(_ connection: NWConnection, data: Data) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 4096) { [weak self] chunk, _, complete, error in
            Task { @MainActor in
                guard let self, error == nil else { connection.cancel(); return }
                var data = data; if let chunk { data.append(chunk) }
                guard data.count <= 4608 else { connection.cancel(); return }
                guard let boundary = data.range(of: Data("\r\n\r\n".utf8)) else { if complete { connection.cancel() } else { self.receive(connection, data: data) }; return }
                guard boundary.upperBound <= 4096, let header = String(data: data[..<boundary.lowerBound], encoding: .utf8) else { connection.cancel(); return }
                let lines = header.components(separatedBy: "\r\n")
                var headers: [String: String] = [:]
                for line in lines.dropFirst() { if let colon = line.firstIndex(of: ":") { headers[String(line[..<colon]).lowercased()] = line[line.index(after: colon)...].trimmingCharacters(in: .whitespaces) } }
                guard headers["origin"] == nil, headers["authorization"] == "Bearer \(self.token)" else { self.reply(connection, code: 403, value: ["ok": false]); return }
                let action: String
                if lines.first == "GET /status HTTP/1.1" { action = "status" }
                else if lines.first == "POST /command HTTP/1.1", let length = Int(headers["content-length"] ?? ""), length > 0, length <= 256 {
                    let body = data[boundary.upperBound...]
                    guard body.count >= length else { if complete { connection.cancel() } else { self.receive(connection, data: data) }; return }
                    let object = try? JSONSerialization.jsonObject(with: Data(body.prefix(length))) as? [String: String]
                    action = object?["command"] ?? ""
                } else { self.reply(connection, code: 400, value: ["ok": false]); return }
                guard ["status", "toggle", "hold.down", "hold.up", "copy", "open", "cancel"].contains(action) else { self.reply(connection, code: 400, value: ["ok": false]); return }
                self.reply(connection, code: 200, value: self.command(action))
            }
        }
    }
    private func reply(_ connection: NWConnection, code: Int, value: [String: Any]) {
        let body = (try? JSONSerialization.data(withJSONObject: value)) ?? Data("{}".utf8)
        var response = Data("HTTP/1.1 \(code) Result\r\nContent-Type: application/json\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: \(body.count)\r\n\r\n".utf8)
        response.append(body); connection.send(content: response, completion: .contentProcessed { _ in connection.cancel() })
    }
    func stop() { listener?.cancel(); listener = nil; try? publish(0) }
}
@MainActor enum StreamDeckIntegration {
    static func install() throws -> String {
        guard let source = Bundle.module.url(forResource: "OpenMicAI", withExtension: "streamDeckPlugin") else { throw MicError.message("The installer is missing. Download the latest app.") }
        guard NSWorkspace.shared.open(source) else { throw MicError.message("Install the Stream Deck app first, then try again.") }
        return "Confirm in Stream Deck, then drag Dictate onto a key."
    }
}
