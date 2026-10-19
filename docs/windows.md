# Windows

Download **OpenMicAI-Portable.exe** from Releases and keep it in a writable folder.
It extracts its inference engines into `OpenMicAI-data` beside the executable.
Models download separately inside **Settings → System → Models**. Copy both the EXE and
its data folder to keep your models and preferences when moving it.

The default model pair is **Parakeet TDT 0.6B v2 Q8** for English speech and
**Qwen3 4B Instruct Q3** for editing. The app recommends a memory-fitting pair
automatically. You can choose the two roles independently.

## One shortcut

Click a text field and press **Ctrl + Shift + Space**. Speak, then press again to
finish. With text highlighted, the same shortcut immediately records your
instruction. Say “rewrite this more clearly”, “translate into French”, or
“summarize this”. Or dictate replacement text. **Rewrite**, **Translate**, and
**Summarize** remain clickable cues on the recording card; Translate opens an
inline language picker. A rewrite or replacement replaces the exact selection when it remains
valid; translation and summary appear on the floating card for review and copying.
For editors such as Google Docs, selection capture can use Ctrl+C when Windows
accessibility does not expose the highlight. The original clipboard formats are
restored. The selection is checked again before a rewrite can replace it.

In **Settings → Access → Shortcuts**, click the shortcut button and press your preferred
keys or a side mouse button. Choose click-to-toggle, hold-to-talk, or automatic
stopping after a pause. **Escape** cancels.

Dictation pastes at the cursor automatically. After a successful paste, the bar
shrinks back to Dictate plus small **Copy recent transcription** and **Open app**
icons. Hover over either icon for its label. Copy recent also works with history
saving disabled. A result card appears when automatic paste could not finish,
or when an action such as translation or summary needs its own result.

## Style and history

Choose **Raw transcript**, **Clean up**, **Polish**, or **Custom** under
**Settings → Writing → Style**. Polish rewrites and chooses a layout automatically using
your loaded local LLM. It supports 19 purposes, including sentences, tables,
recipes, FAQs, outlines and meeting notes. Ordinary messages stay prose; no
format picker is needed. A local meaning check checks supplied facts and context.
“Actually, two bananas” updates an earlier item. Vocabulary and snippets work in
all four styles. See the [actual formatting examples](formatting-examples.md).

Home shows local usage and the five newest dictations. History has search, date
filtering, Copy original and Copy modified buttons. Snippets and vocabulary use inline editors.
Home also shows cumulative AI input, output and total tokens reported by the
local model. Counts include planning, editing and verification calls. They start
with this update; historical counts are not estimated. Disable usage saving or
clear usage in Settings to control these saved counts. All settings and model
downloads stay inside the app.

In **History → Import file**, choose a local recording, or drop one onto the import
area. Audio and video use Windows' installed codecs; WAV, MP3, M4A and MP4 are
common supported formats. The first audio track is transcribed with your selected
speech model. **Use writing style** optionally applies your current AI style.
Progress, Cancel, Copy original and Copy modified stay inside History. Long recordings run in
bounded chunks; cancelled or failed imports retain a partial transcript. Imported
files are not automatically pasted into another app or included in dictation WPM.
No audio copy is saved. Windows editions without Media Foundation need the Media
Feature Pack. Speaker labels are not generated.

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
The offline core suite also generates synthetic audio to check media decoding,
resampling, chunk boundaries, cancellation and partial-result preservation.
