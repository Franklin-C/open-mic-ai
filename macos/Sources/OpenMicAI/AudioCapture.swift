import AVFoundation
import Foundation

final class AudioCapture {
    private let engine = AVAudioEngine()
    private let lock = NSLock()
    private var samples = [Float]()
    private var silent = 0.0
    private var speech = false
    private var installedTap = false
    func start() throws {
        lock.lock(); samples.removeAll(keepingCapacity: true); silent = 0; speech = false; lock.unlock()
        let input = engine.inputNode
        let format = input.outputFormat(forBus: 0)
        guard format.sampleRate > 0, let output = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: 16000, channels: 1, interleaved: false), let converter = AVAudioConverter(from: format, to: output) else { throw MicError.message("Choose a working microphone in macOS Sound settings.") }
        input.installTap(onBus: 0, bufferSize: 4096, format: format) { [weak self] buffer, _ in
            guard let self, let converted = AVAudioPCMBuffer(pcmFormat: output, frameCapacity: AVAudioFrameCount(Double(buffer.frameLength) * 16000 / format.sampleRate + 32)) else { return }
            var supplied = false; var error: NSError?
            converter.convert(to: converted, error: &error) { _, status in
                if supplied { status.pointee = .noDataNow; return nil }
                supplied = true; status.pointee = .haveData; return buffer
            }
            guard error == nil, let data = converted.floatChannelData?[0] else { return }
            let count = Int(converted.frameLength)
            let frames = Array(UnsafeBufferPointer(start: data, count: count))
            let level = frames.reduce(Float(0)) { $0 + $1 * $1 } / Float(max(1, count))
            self.lock.lock()
            self.samples.append(contentsOf: frames)
            if level > 0.00015 { self.speech = true; self.silent = 0 } else { self.silent += Double(count) / 16000 }
            self.lock.unlock()
        }
        installedTap = true
        do { engine.prepare(); try engine.start() } catch { stop(); throw error }
    }
    func stop() { engine.stop(); if installedTap { engine.inputNode.removeTap(onBus: 0); installedTap = false } }
    func state() -> (seconds: Double, speech: Bool, silence: Double) {
        lock.lock(); defer { lock.unlock() }
        return (Double(samples.count) / 16000, speech, silent)
    }
    func wav() -> Data {
        lock.lock(); let snapshot = samples; lock.unlock()
        let pcm = snapshot.map { Int16(max(-1, min(1, $0)) * 32767).littleEndian }
        var result = Data()
        func append<T>(_ value: T) { var value = value; withUnsafeBytes(of: &value) { result.append(contentsOf: $0) } }
        result.append(Data("RIFF".utf8)); append(UInt32(36 + pcm.count * 2).littleEndian)
        result.append(Data("WAVEfmt ".utf8)); append(UInt32(16).littleEndian); append(UInt16(1).littleEndian)
        append(UInt16(1).littleEndian); append(UInt32(16000).littleEndian); append(UInt32(32000).littleEndian)
        append(UInt16(2).littleEndian); append(UInt16(16).littleEndian)
        result.append(Data("data".utf8)); append(UInt32(pcm.count * 2).littleEndian)
        pcm.withUnsafeBytes { result.append(contentsOf: $0) }
        return result
    }
    deinit { stop() }
}
