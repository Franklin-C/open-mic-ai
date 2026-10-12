<div align="center">
  <img src="windows/assets/open-mic-ai-ui.png" width="96" alt="Open Mic AI microphone logo">
  <h1>Open Mic AI</h1>
  <p>Speak. Format. Paste. On your computer.</p>
  <p>
    <a href="https://github.com/Franklin-C/open-mic-ai/actions/workflows/build.yml"><img src="https://github.com/Franklin-C/open-mic-ai/actions/workflows/build.yml/badge.svg" alt="Build and test"></a>
    <img src="https://img.shields.io/badge/Windows-native-e53945" alt="Native Windows">
    <img src="https://img.shields.io/badge/macOS-preview-e53945" alt="macOS preview">
    <img src="https://img.shields.io/badge/inference-local-26834a" alt="Local inference">
    <img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT license">
  </p>
  <p><a href="https://github.com/Franklin-C/open-mic-ai/releases">Downloads</a> · <a href="docs/windows.md">Windows guide</a> · <a href="docs/macos.md">Mac guide</a> · <a href="#features">Features</a></p>
</div>

![Open Mic AI home and usage dashboard](docs/images/hub-home.png)

Open Mic AI is a background dictation app with local speech recognition and local
AI editing. Click a text box, press your shortcut, and speak. It formats the result
and pastes it at your cursor. If the destination cannot accept a paste, Copy is
always available.

**Windows** is the existing native app. **macOS** is a new native Swift preview
for Apple Silicon and Intel. The Mac port needs real-device testing of microphone
permissions, audio devices and accessibility across third-party apps; it does not
yet have every Windows feature.

## Start speaking

1. Download your platform's app from [Releases](https://github.com/Franklin-C/open-mic-ai/releases).
2. Open **Settings → Models**, choose speech and language models, and click **Download & load models**.
3. Wait for **Ready**, click a text box, and press **Ctrl + Shift + Space** (Mac: **Control + Shift + Space**).
4. Speak. Press again to finish, or choose hold-to-talk or automatic stopping.

Both engines remain loaded while the app runs. Internet is needed for the initial
model downloads; dictation and editing run locally afterward. The app downloads
weights separately so the executable stays small. Download sizes are shown in the
model selectors.

## Choose how your words appear

| Style | What it does |
| --- | --- |
| **Raw transcript** | Speech-model text without AI editing. |
| **Clean up** | Removes fillers and fixes grammar and punctuation. |
| **Polish → My words** | Formatting, punctuation and capitalization with your own wording. No added words or invented heading labels. |
| **Polish → Reworded** | Clearer phrasing plus context-aware formatting, while preserving meaning. |
| **Custom** | Your own instructions in an inline prompt box. |

Polish chooses prose, bullets, numbered steps, or headings from context. It keeps
meaningful introductions, descriptions, quantities, conditions and closing
requests. “My words” checks the output's words against the source before accepting
it. Reworded output still receives a local meaning check. Failed dictation edits
get one retry, then retain the transcript; rejected selected-text edits leave the
original unchanged. Model checks reduce errors, but are not a guarantee of perfect
meaning or speech recognition.

![Polish and its wording options](docs/images/hub-style.png)

## Features

| Feature | Windows | Mac preview |
| --- | :---: | :---: |
| Background tray / menu-bar app | ✓ | ✓ |
| Local English speech-to-text and local language model | ✓ | ✓ |
| Independent model selectors and in-app downloads | ✓ | ✓ |
| Models stay loaded and ready | ✓ | ✓ |
| Custom keyboard shortcut capture | ✓ | ✓ |
| Click, hold, or automatic recording | ✓ | ✓ |
| Mouse 4 / Mouse 5 shortcut | ✓ | — |
| Draggable floating bar with provisional transcript | ✓ | ✓ |
| AI processing indicator and Copy fallback | ✓ | ✓ |
| Selected-text rewrite, translate and summarize | ✓ | ✓ |
| Spoken instructions for selected text | ✓ | ✓ |
| Guarded automatic paste / replacement | ✓ | ✓ |
| Local history, search and date filter | ✓ | ✓ |
| Home statistics and five recent dictations | ✓ | ✓ |
| Activity dates, word counts and month labels | ✓ | ✓ |
| Spoken snippets and personal vocabulary | ✓ | ✓ |
| Start at Windows sign-in, sounds and playback mute | ✓ | — |
| Resumable model downloads | ✓ | — |

The checkmarks describe implemented features; Mac runtime compatibility still
needs testing. Selection capture depends on the destination app exposing its text
through the operating system's accessibility APIs. No screenshot contains actual
user dictation; the examples are synthetic test fixtures.

<table><tr>
<td><img src="docs/images/hub-history.png" alt="Searchable dictation history"></td>
<td><img src="docs/images/hub-models.png" alt="Independent local model selectors"></td>
</tr><tr>
<td><img src="docs/images/hub-snippets.png" alt="Inline snippets editor"></td>
<td><img src="docs/images/selected-text-card-preview.png" alt="Selected-text AI action card"></td>
</tr></table>

## Privacy

There is no hosted inference service, account requirement, subscription, or
telemetry integration. Speech and editing requests go to local engines. Audio is
processed in memory and not saved. Optional history and preferences stay in your
app data folder. Model files come from pinned upstream revisions and are checked
against SHA-256 hashes. See [Security and privacy](SECURITY.md).

## Build and contribute

- **Windows:** .NET Framework 4.8; run `windows/Build.ps1`. No NuGet packages are required. Run `windows/validation/RunCoreChecks.ps1` for offline word-guard and retry checks. Native integration tests require locally installed models.
- **Mac:** macOS 13+, Xcode command-line tools, CMake and Git; run `bash macos/build.sh`. The script tests Swift code, builds the pinned speech and language engines, and packages an app for your architecture.

CI builds Windows and both Mac architectures. Mac packages use a development
signature and are not notarized. The downloadable Windows portable app includes
its engines; the thin Windows source-build artifact requires those engines
separately. See the platform guides for details.

The app source is [MIT licensed](LICENSE). Models and inference engines have their
own licenses: [Windows notices](windows/THIRD_PARTY_NOTICES.txt),
[Mac notices](macos/THIRD_PARTY_NOTICES.md).
