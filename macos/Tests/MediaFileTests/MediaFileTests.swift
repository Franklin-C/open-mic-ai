import XCTest
import AVFoundation
@testable import OpenMicAI
import OpenMicCore

private actor Chunks {
    var values = [MediaChunk]()
    func append(_ chunk: MediaChunk) { values.append(chunk) }
    func snapshot() -> [MediaChunk] { values }
}
final class MediaFileTests: XCTestCase {
    func fixture(_ name: String, seconds: Int = 1) throws -> URL {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("OpenMicAI-media-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        addTeardownBlock { try FileManager.default.removeItem(at: folder) }
        let url = folder.appendingPathComponent(name)
        let settings: [String: Any] = [AVFormatIDKey: name.hasSuffix("m4a") ? kAudioFormatMPEG4AAC : kAudioFormatLinearPCM,
            AVSampleRateKey: 44100, AVNumberOfChannelsKey: 2]
        let file = try AVAudioFile(forWriting: url, settings: settings)
        let buffer = AVAudioPCMBuffer(pcmFormat: file.processingFormat, frameCapacity: 44100)!
        buffer.frameLength = 44100
        for channel in 0..<2 { for frame in 0..<44100 { buffer.floatChannelData![channel][frame] = Float(sin(Double(frame) * 440 * 2 * .pi / 44100)) * 0.2 } }
        for _ in 0..<seconds { try file.write(from: buffer) }; return url
    }
    func testResamplesStereoWaveAndAAC() async throws {
        for name in ["tone.wav", "tone.m4a"] {
            let url = try fixture(name), chunks = Chunks()
            try await MediaFile.read(url) { await chunks.append($0) }
            let values = await chunks.snapshot()
            XCTAssertEqual(values.count, 1)
            XCTAssertTrue(values[0].hasSound)
            XCTAssertEqual(values[0].seconds, 1, accuracy: 0.2)
            XCTAssertEqual(values[0].wav.count, values[0].pcm.count + 44)
        }
    }
    func testLongFileHasBoundedChunksAndOverlap() async throws {
        let url = try fixture("long.wav", seconds: 61), chunks = Chunks()
        try await MediaFile.read(url) { await chunks.append($0) }
        let values = await chunks.snapshot()
        XCTAssertEqual(values.count, 3)
        XCTAssertFalse(values[0].overlap); XCTAssertTrue(values[1].overlap)
        XCTAssertEqual(values[0].pcm.suffix(32000), values[1].pcm.prefix(32000))
        XCTAssertTrue(values.allSatisfy { $0.pcm.count <= 960000 })
        XCTAssertEqual(values.last!.seconds, 61, accuracy: 0.05)
    }
    func testCancellationStopsDecoding() async throws {
        let url = try fixture("cancel.wav", seconds: 61)
        do { try await MediaFile.read(url) { _ in throw CancellationError() }; XCTFail("Expected cancellation") }
        catch is CancellationError { }
    }
    func testInvalidMediaIsRejected() async throws {
        do { try await MediaFile.read(URL(string: "https://example.com/audio.wav")!) { _ in }; XCTFail("Remote URL accepted") } catch { }
        let url = try fixture("invalid.wav")
        try Data("not media".utf8).write(to: url)
        do { try await MediaFile.read(url) { _ in }; XCTFail("Invalid media accepted") } catch { }
    }
}
