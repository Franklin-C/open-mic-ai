# Stream Deck

In Open Mic AI 3.8 or later, open **Settings → System → Stream Deck → Install plugin**.
Confirm installation in Stream Deck, then drag an **Open Mic AI** action onto a key.
The installer is included in the app; no extra download or pairing code is needed.

Requires the Stream Deck desktop app **7.1 or later**, on Windows or macOS. Keep
Open Mic AI running with models loaded for instant access. No separate Node.js
installation is needed.

| Key | Action |
| --- | --- |
| Dictate | Press to record; press again to finish and paste. |
| Hold to talk | Hold to record; release to finish and paste. |
| Copy recent | Copy the most recent result. |
| Open app | Open the main window. |
| Cancel | Cancel recording or the current AI edit. |

Click your destination text box before pressing a key. With text highlighted,
Dictate and Hold to talk accept the same spoken instructions as the shortcut:
“rewrite this more clearly”, “translate into French”, or “summarize this”.

The voice keys show **Stop**, **Release**, or **Working…** as the app processes
speech. Their recording behavior is independent of your keyboard shortcut and
recording-mode setting. If Open Mic AI is closed, pressing a key starts it;
wait for models to load, then press Dictate again. **Open app** opens it directly.

If the actions do not appear, update Stream Deck and use **Install plugin** again.
The standalone installer is also available in the GitHub release downloads.
Stream Deck manages the installation confirmation and key assignment.

## For contributors

From `streamdeck/`, run `npm ci`, `npm run pack`, then `npm test`. The official
Elgato CLI validates the manifest and creates the installer in `streamdeck/dist/`.
Copy `com.openmicai.controls.streamDeckPlugin` to
`windows/assets/OpenMicAI.streamDeckPlugin` before building the app. The Mac build
copies that same package into its resources.

The plugin connects only to `127.0.0.1`, using a random token in a file protected
for the current user. It receives status flags and sends a fixed set of commands;
audio and text are processed by Open Mic AI. Copy recent updates the clipboard
inside the app. The bridge does not expose transcripts or model prompts.

Automated checks cover real SDK key events against simulated Stream Deck/app
connections, quick hold-release presses, recording titles/icons, all commands,
authentication, browser-origin rejection, and app shutdown. Physical-device
microphone/paste behavior still depends on the destination app and permissions.
