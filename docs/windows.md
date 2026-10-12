# Windows

Download **OpenMicAI-Portable.exe** from Releases and keep it in a writable folder.
It extracts its inference engines into `OpenMicAI-data` beside the executable.
Models download separately inside **Settings → Models**. Copy both the EXE and
its data folder to keep your models and preferences when moving it.

The default model pair is **Parakeet TDT 0.6B v2 Q8** for English speech and
**Qwen3 4B Instruct Q3** for editing. The app recommends a memory-fitting pair
automatically. You can choose the two roles independently.

## One shortcut

Click a text field and press **Ctrl + Shift + Space**. Speak, then press again to
finish. Select some text before pressing the same shortcut to open **Rewrite**,
**Translate**, and **Summarize**. Press again or click **Speak instruction** to
describe the change. A rewrite replaces the exact selection when it remains
valid; translation and summary appear on the floating card for review and copying.

In **Settings → Shortcuts**, click the shortcut button and press your preferred
keys or a side mouse button. Choose click-to-toggle, hold-to-talk, or automatic
stopping after a pause. **Escape** cancels.

## Style and history

Choose **Raw transcript**, **Clean up**, **Polish**, or **Custom** under
**Settings → Style**. Polish has **My words** and **Reworded** options. My words
uses a deterministic word check to reject added or substituted wording, plus a
meaning review to catch omissions. Formatting may remove true fillers, list
connectors and superseded corrections. It does not promise to fix transcription
mistakes without changing words.

Home shows local usage and the five newest dictations. History has search, date
filtering, Copy and Original buttons. Snippets and vocabulary use inline editors.
All settings and model downloads stay inside the app.

Closing the window keeps the tray app and models running. Double-click the tray
icon, choose **Open app**, or launch the EXE again to reopen it. **Quit** unloads
the models. Automatic paste may be blocked by elevated apps, unsupported custom
controls, password fields, or a changed selection; use Copy in those cases.

## Building

```powershell
./windows/Build.ps1
./windows/validation/RunCoreChecks.ps1
```

`Build.ps1` creates a thin EXE. For inference it needs `runtime/speech/` containing
the Parakeet server and `runtime/` containing the llama server and their runtime
libraries. The downloaded portable release includes these components. For a
portable rebuild, pass a licensed runtime ZIP with the same folder structure:

```powershell
./windows/Build.ps1 -EmbeddedRuntime path/to/runtime.zip -Output OpenMicAI-Portable.exe
```

Models are never embedded in that ZIP. Native integration tests under
`windows/validation` use isolated synthetic fixtures; tests requiring weights
expect the default model files in `windows/models` and engines in `windows/runtime`.
`RunChecks.ps1` also expects a 16 kHz mono English WAV at
`windows/validation/speech-fixture.wav`; supply a sample you are comfortable using
for a local test. Audio fixtures and personal data are excluded from Git.
