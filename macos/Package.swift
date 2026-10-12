// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "OpenMicAI", platforms: [.macOS(.v13)],
    products: [.executable(name: "OpenMicAI", targets: ["OpenMicAI"])],
    targets: [
        .target(name: "OpenMicCore"),
        .executableTarget(name: "OpenMicAI", dependencies: ["OpenMicCore"], resources: [.process("Resources")]),
        .testTarget(name: "OpenMicCoreTests", dependencies: ["OpenMicCore"])
    ]
)
