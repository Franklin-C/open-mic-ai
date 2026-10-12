import AVFoundation
import CoreMedia
import Foundation

struct MediaChunk: Sendable {
    let pcm: Data
    let seconds: Double
    let overlap: Bool
    var hasSound: Bool {
        var sum = 0.0
        pcm.withUnsafeBytes { bytes in
            for offset in stride(from: 0, to: bytes.count - 1, by: 2) {
                let value = Double(Int16(bitPattern: UInt16(bytes[offset]) | UInt16(bytes[offset + 1]) << 8)) / 32768
                sum += value * value
            }
        }
        return !pcm.isEmpty && sum / Double(pcm.count / 2) > 0.000012
    }
    var wav: Data {
        var data = Data()
        func append<T>(_ value: T) { var value = value; withUnsafeBytes(of: &value) { data.append(contentsOf: $0) } }
        data.append(Data("RIFF".utf8)); append(UInt32(36 + pcm.count).littleEndian)
        data.append(Data("WAVEfmt ".utf8)); append(UInt32(16).littleEndian); append(UInt16(1).littleEndian); append(UInt16(1).littleEndian)
        append(UInt32(16000).littleEndian); append(UInt32(32000).littleEndian); append(UInt16(2).littleEndian); append(UInt16(16).littleEndian)
        data.append(Data("data".utf8)); append(UInt32(pcm.count).littleEndian); data.append(pcm); return data
    }
}
enum MediaFile {
    static func read(_ url: URL, consume: @escaping @Sendable (MediaChunk) async throws -> Void) async throws {
        guard url.isFileURL, FileManager.default.fileExists(atPath: url.path) else { throw MicError.message("Choose a local audio or video file.") }
        let worker = Task.detached {
            try Task.checkCancellation()
            let asset = AVURLAsset(url: url)
            guard let track = try await asset.loadTracks(withMediaType: .audio).first else { throw MicError.message("This file has no audio track.") }
            let reader = try AVAssetReader(asset: asset)
            defer { reader.cancelReading() }
            let settings: [String: Any] = [AVFormatIDKey: kAudioFormatLinearPCM, AVSampleRateKey: 16000, AVNumberOfChannelsKey: 1,
                AVLinearPCMBitDepthKey: 16, AVLinearPCMIsFloatKey: false, AVLinearPCMIsBigEndianKey: false, AVLinearPCMIsNonInterleaved: false]
            let output = AVAssetReaderTrackOutput(track: track, outputSettings: settings)
            output.alwaysCopiesSampleData = false
            guard reader.canAdd(output) else { throw MicError.message("This audio format is not supported by macOS.") }
            reader.add(output)
            guard reader.startReading() else { throw reader.error ?? MicError.message("This file could not be decoded.") }
            let chunkBytes = 30 * 32000, overlapBytes = 32000
            var pending = Data(), total = 0, overlap = false
            while let sample = output.copyNextSampleBuffer() {
                try Task.checkCancellation()
                guard let block = CMSampleBufferGetDataBuffer(sample) else { throw MicError.message("Invalid audio sample.") }
                let length = CMBlockBufferGetDataLength(block)
                guard length <= 8 * 1024 * 1024, length % 2 == 0 else { throw MicError.message("Invalid audio sample.") }
                if length == 0 { continue }
                var decoded = Data(count: length)
                let code = decoded.withUnsafeMutableBytes { CMBlockBufferCopyDataBytes(block, atOffset: 0, dataLength: length, destination: $0.baseAddress!) }
                guard code == kCMBlockBufferNoErr else { throw MicError.message("Audio could not be read.") }
                pending.append(decoded); total += length
                while pending.count >= chunkBytes {
                    try Task.checkCancellation()
                    let chunk = Data(pending.prefix(chunkBytes)), seconds = Double(total - (pending.count - chunkBytes)) / 32000
                    try await consume(MediaChunk(pcm: chunk, seconds: seconds, overlap: overlap))
                    pending.removeFirst(chunkBytes - overlapBytes); overlap = true
                }
            }
            try Task.checkCancellation()
            guard reader.status == .completed else { throw reader.error ?? MicError.message("The audio file could not finish decoding.") }
            if pending.count > (overlap ? overlapBytes : 0) { try await consume(MediaChunk(pcm: pending, seconds: Double(total) / 32000, overlap: overlap)) }
            if total == 0 { throw MicError.message("This file has no readable audio.") }
        }
        try await withTaskCancellationHandler(operation: { try await worker.value }, onCancel: { worker.cancel() })
    }
}
