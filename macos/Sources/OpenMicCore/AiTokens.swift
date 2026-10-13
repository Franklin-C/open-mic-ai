import Foundation
import CoreFoundation

public struct AiTokenUsage {
    public let input: Int64
    public let output: Int64
    public init(input: Int64, output: Int64) { self.input = input; self.output = output }
    public static func read(_ response: [String: Any]) -> AiTokenUsage? {
        guard let usage = response["usage"] as? [String: Any] else { return nil }
        func count(_ key: String) -> Int64? {
            guard let number = usage[key] as? NSNumber, CFGetTypeID(number) != CFBooleanGetTypeID(),
                  let value = Int64(number.stringValue), value >= 0 else { return nil }
            return value
        }
        guard let input = count("prompt_tokens"), let output = count("completion_tokens") else { return nil }
        return AiTokenUsage(input: input, output: output)
    }
}

public struct AiTokenTotals: Codable {
    public private(set) var input: Int64 = 0
    public private(set) var output: Int64 = 0
    public var total: Int64 { input + output }
    public init() {}
    enum CodingKeys: String, CodingKey { case input, output }
    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        input = max(0, try values.decodeIfPresent(Int64.self, forKey: .input) ?? 0)
        output = max(0, try values.decodeIfPresent(Int64.self, forKey: .output) ?? 0)
    }
    public mutating func add(_ usage: AiTokenUsage) {
        guard usage.input >= 0, usage.output >= 0 else { return }
        input += usage.input; output += usage.output
    }
}
