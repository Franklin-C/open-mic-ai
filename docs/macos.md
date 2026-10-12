# macOS preview

The Mac app is a new native SwiftUI implementation. It uses
[whisper.cpp](https://github.com/ggml-org/whisper.cpp) for local English speech and
[llama.cpp](https://github.com/ggml-org/llama.cpp) for local AI editing. Apple
Silicon and Intel have separate packages. This preview targets **macOS 13+**.

## Install

1. Download the Mac ZIP matching your architecture: `arm64` for Apple Silicon,
   `x86_64` for Intel.
2. Unzip and move **Open Mic AI.app** to Applications.
3. Open it and download both models in **Settings → Models**.
4. Allow microphone access when prompted. Use **Settings → Shortcuts → Allow
   text-box access** to grant macOS Accessibility access for selection capture
   and automatic paste.

The preview has a development signature and is not notarized. macOS may require
you to approve opening it in **System Settings → Privacy & Security**. A stable,
signed/notarized distribution is still future work.

The default shortcut is **Control + Shift + Space**. Click the shortcut button
to record another combination. The recording modes are click-to-toggle,
hold-to-talk, and automatic pause detection. The floating panel can be dragged
freely. Close the main window to keep the app in the menu bar; use **Open app** to
reopen it and **Quit Open Mic AI** to unload the models.

The Mac model catalog offers **Whisper Base English / Small English** and six
Qwen language-model choices independently. The engines remain loaded. Windows
uses a different speech engine, so Windows accuracy and timing results should not
be treated as Mac measurements.

Selected-text actions use macOS accessibility. A changed selection, password
field, read-only control, or unsupported app is not automatically replaced.
Translation and summary stay on the card. **Copy** is the fallback.

In **History → Import file**, choose a local audio or video recording. The first
audio track is decoded with macOS' installed codecs and transcribed locally.
**Use writing style** optionally applies your current AI style. Use Original or
Copy to retrieve either text, or Cancel to stop and keep a partial transcript.
Long recordings run in chunks. No audio copy is saved, no automatic paste occurs,
and imported audio does not affect dictation WPM. No speaker labels are generated.

## Build

Install Xcode command-line tools, CMake, and Git, then run:

```sh
bash macos/build.sh
```

This runs Swift word-guard and synthetic media-decoder tests, compiles the app and pinned local engines, and creates
`macos/dist/OpenMicAI-macOS-<architecture>.zip`. Weights are downloaded through
the app. Build once on Apple Silicon and once on Intel to produce both packages;
the GitHub Actions workflow does this on separate native runners.

Data stays under `~/Library/Application Support/Open Mic AI/`. History is capped
at 200 entries and can be disabled. No audio is retained. The initial preview
still needs hands-on validation of microphone permissions, device switching,
live transcription latency, accessibility and paste across common Mac apps.
Windows-specific startup, side mouse buttons, sounds, playback mute and resumable
downloads are not part of this initial Mac implementation.
