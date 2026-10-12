# Feature comparison: sources and scope

Checked October 12, 2026. The [README comparison](../README.md#features) uses
official product pages, documentation, and source repositories. It compares
desktop capabilities across each product's supported platforms, not identical
plans or default configurations. A check does not mean every model or platform
supports the feature. **?** means we did not verify it; it is not a claim that
the feature is missing. No competing app was benchmarked in this project.

## Open Mic AI

The Windows app uses Parakeet and Qwen; the Mac preview uses Whisper and Qwen.
Both provide independently chosen local models, one-shortcut dictation and text
actions, custom instructions, vocabulary, snippets, history, and a floating bar.
The word guard is in [Windows Formatting.cs](../windows/src/Formatting.cs) and
[Mac Core.swift](../macos/Sources/OpenMicCore/Core.swift), with automated checks.
See the [platform checklist](../README.md#features) and
[verification scope](verification.json).

Windows has native fixture and local-model integration results. Mac builds and
tests passed on Apple Silicon and Intel runners; microphone, accessibility,
audio-device changes and paste still need testing on real Macs. English speech
only, no mobile app, no meeting recorder, and one global style are the current
scope. The app source is [MIT licensed](../LICENSE).

## Wispr Flow

Its official site documents Windows/macOS, cross-app dictation, formatting,
vocabulary, snippets, per-app styles and multilingual speech. Its privacy page
says transcription takes place on servers, even when cloud storage is disabled.
Desktop Command Mode uses selected text with spoken instructions and requires
eligible plan access. Local history is configurable.

- [Platforms, dictation, formatting, vocabulary, snippets and languages](https://wisprflow.ai/)
- [Cloud transcription and local history controls](https://wisprflow.ai/privacy)
- [Command Mode and access requirements](https://docs.wisprflow.ai/articles/4816967992-how-to-use-command-mode)

We did not verify a public app-source license, user-selectable speech/LLM pair,
custom written prompt editor, or deterministic no-added-word validator.

## OpenWhispr

The official docs and MIT repository describe Windows/macOS/Linux dictation,
automatic paste, offline speech and local AI reasoning. Speech recognition and
cleanup/agent models are configured separately. Dictionary entries and snippets
are supported. The voice agent can edit highlighted text; transcription records
and notes are retained. Speech languages depend on the selected engine.

- [Platforms and stored transcriptions](https://docs.openwhispr.com/)
- [Speech-model downloads and language support](https://docs.openwhispr.com/guides/local-models)
- [Independent cleanup model, including local processing](https://docs.openwhispr.com/help/dictation/cleanup)
- [Vocabulary and snippets](https://docs.openwhispr.com/guides/custom-dictionary)
- [Selected-text voice edits](https://docs.openwhispr.com/help/agent/voice-agent)
- [Local AI reasoning and MIT source](https://github.com/OpenWhispr/openwhispr)

We did not confirm a custom written prompt editor or a deterministic word guard
from these sources. OpenWhispr also offers meetings, notes and cloud services
beyond Open Mic AI's present scope.

## Superwhisper

The docs describe Windows/macOS, offline speech, multilingual dictation, automatic
paste, independent speech/LLM selection, and model downloads. Offline AI processing
uses local S1-mini in Message and Mail only; Custom modes require online AI.
Custom modes accept written prompts and selected text. History retains results;
vocabulary replacements can expand short phrases.

- [Platforms, offline behavior and languages](https://superwhisper.com/docs/get-started/introduction)
- [Model selection, downloads and text delivery](https://superwhisper.com/docs/get-started/choose-your-model)
- [S1-mini restrictions](https://superwhisper.com/docs/models/language)
- [Custom instructions and selected-text context](https://superwhisper.com/docs/modes/custom)
- [Vocabulary and phrase expansion](https://superwhisper.com/docs/get-started/interface-vocabulary)
- [History and results](https://superwhisper.com/docs/get-started/history)

We did not verify a public app-source license or deterministic word guard.
Features can require Pro access; Windows and macOS differ in some controls.

## VoiceInk

VoiceInk's desktop app is for macOS. Its modes separate transcription, AI
enhancement and delivery, including automatic paste. Local speech, vocabulary,
word replacements, custom prompts, selected-text context and history are
documented. VoiceInk Refine performs fixed on-device cleanup on supported Apple
Silicon Macs; it cannot use custom prompts or selected-text context. The docs
recommend Ollama or a compatible provider for those workflows. The published
source is GPL-3.0; buying the packaged app supports its development.

- [Desktop scope, modes and text delivery](https://tryvoiceink.com/docs/introduction)
- [Local cleanup and Refine limitations; Ollama alternative](https://tryvoiceink.com/docs/voiceink-refine)
- [Custom prompts and selected text](https://tryvoiceink.com/docs/custom-prompts)
- [Text expansion](https://tryvoiceink.com/docs/word-replacements)
- [Saved results](https://tryvoiceink.com/docs/transcription-history)
- [Multilingual local speech models](https://tryvoiceink.com/docs/local-models)
- [Published app source and license](https://github.com/Beingpax/VoiceInk)

We did not verify a deterministic word guard. Local speech alone does not make
cloud enhancement offline, and Ollama needs a separately configured local service.

## Reading the table

Cross-app input depends on operating-system permissions, secure/read-only fields,
application accessibility, and focus. Open Mic AI offers Copy when insertion
cannot be safely completed. No product is given a universal compatibility,
accuracy, privacy-certification, or latency guarantee by this checklist. Features
and plans can change; corrections with a supporting official link are welcome.
