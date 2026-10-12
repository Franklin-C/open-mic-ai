# Feature comparison: sources and scope

Checked October 12, 2026. The [README comparison](../README.md#features) uses
official product pages, documentation, and source repositories. It compares
desktop capabilities across each product's supported platforms, not identical
plans or default configurations. A check does not mean every model or platform
supports the feature. **✓** means available, **✕** means unavailable, and **-**
means not verified in the sources; a dash is not a claim that the feature is
missing. Qualifiers stay in table cells. No competing app was benchmarked here.

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
audio-device changes and paste still need testing on real Macs. Speech recognition
currently uses English-only models. Text translation supports multiple languages:
the Windows picker offers 15 language choices and accepts other language names;
Mac has a translation-language field. See
[Windows text actions](../windows/src/TextActions.cs) and
[Mac translation prompts](../macos/Sources/OpenMicCore/Core.swift).
There is no mobile app, Linux app, meeting recorder, audio/video import or cloud
provider option, and the app uses one global style. The app source is
[MIT licensed](../LICENSE).

### What Open Mic AI offers

Open Mic AI packages unlimited local dictation, custom AI editing, translation,
and summary without an account, paid license or subscription. Its My words mode
also validates that accepted formatting adds no new words. We did not find a
documented equivalent word validator in the competitors' sources; the table uses
dashes rather than claiming those apps cannot preserve wording. OpenWhispr also
offers free local processing. Superwhisper's offline AI is limited to Message and
Mail; VoiceInk can use local custom prompts through a separate Ollama setup.

### What other apps offer that Open Mic AI does not

Wispr Flow, OpenWhispr and Superwhisper document meeting capture with speaker
labels. OpenWhispr, Superwhisper and VoiceInk import recorded media. Wispr Flow,
Superwhisper and VoiceInk can switch styles or modes by app. All four have cloud
processing, mobile apps and multilingual speech-model choices. OpenWhispr also
has a Linux app. These differences are represented as separate rows in the
README; supporting official sources appear under each product below.

## Wispr Flow

Its official site documents Windows/macOS, cross-app dictation, formatting,
vocabulary, snippets, per-app styles and multilingual speech. Its privacy page
says transcription takes place on servers, even when cloud storage is disabled.
Desktop Command Mode uses selected text with spoken instructions and requires
eligible plan access. Local history is configurable.

- [Platforms, dictation, formatting, vocabulary, snippets and languages](https://wisprflow.ai/)
- [Cloud transcription and local history controls](https://wisprflow.ai/privacy)
- [Command Mode and access requirements](https://docs.wisprflow.ai/articles/4816967992-how-to-use-command-mode)

Its unlimited dictation and Command Mode use paid access. The homepage documents
per-app styles, mobile apps and meeting notes with speaker labels. We did not
verify a public app-source license, user-selectable speech/LLM pair, custom
written prompt editor, text translation, media import or deterministic word
validator. The supported-platform list does not include Linux.

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
- [Custom translation prompt, target languages and separate translation model](https://docs.openwhispr.com/help/dictation/translation)
- [Free local processing](https://docs.openwhispr.com/guides/cloud-vs-local)
- [Local AI reasoning and MIT source](https://github.com/OpenWhispr/openwhispr)

The translation workflow has a written prompt editor, including local processing;
we have not generalized that to every cleanup or agent workflow. We did not
confirm automatic per-app styles or a deterministic word guard. OpenWhispr also
offers meetings, media import, speaker labels and an iOS app.

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
- [Automatic app activation, translation and speaker labels](https://superwhisper.com/docs/modes/modes)
- [Vocabulary and phrase expansion](https://superwhisper.com/docs/get-started/interface-vocabulary)
- [History and results](https://superwhisper.com/docs/get-started/history)
- [Free speech-only plan and paid AI/file transcription](https://superwhisper.com/docs/billing/plans)

We did not verify a public app-source license or deterministic word guard.
Features can require Pro access; Windows and macOS differ in some controls. It
offers iOS/Android apps, meeting capture and media import; Linux is listed as
planned, not available.

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
- [Automatic app and website mode switching](https://tryvoiceink.com/docs/mode-triggers)
- [Text expansion](https://tryvoiceink.com/docs/word-replacements)
- [Saved results](https://tryvoiceink.com/docs/transcription-history)
- [Multilingual local speech models](https://tryvoiceink.com/docs/local-models)
- [Recorded media import](https://tryvoiceink.com/docs/transcribe-audio-files)
- [iOS dictation](https://tryvoiceink.com/docs/quick-dictation)
- [One-time purchase for the packaged Mac app](https://tryvoiceink.com/)
- [Published app source and license](https://github.com/Beingpax/VoiceInk)

We did not verify a deterministic word guard. Local speech alone does not make
cloud enhancement offline, and Ollama needs a separately configured local service.
The translation cell describes a custom-prompt workflow with a compatible model,
not a dedicated built-in translation button. Its published desktop platforms do
not include Linux. We did not confirm live meeting recording with speaker labels.

## Reading the table

Cross-app input depends on operating-system permissions, secure/read-only fields,
application accessibility, and focus. Open Mic AI offers Copy when insertion
cannot be safely completed. No product is given a universal compatibility,
accuracy, privacy-certification, or latency guarantee by this checklist. Features
and plans can change; corrections with a supporting official link are welcome.
