# EdgeRock on Windows (experimental)

The Windows language-model selector includes **EdgeRock 4B · Experimental**.
It uses a separate, local PyTorch process; EdgeRock GGUF files do not work in
the app's existing llama.cpp runtime. The Qwen defaults are unchanged. macOS
integration is not included.

## Setup

With Python 3.10 or newer installed, select EdgeRock in **Settings → System →
Models** and choose **Download & load models**. The app installs the isolated
runtime, verifies the checkpoint, and shows progress in the Models page. Cancel
stops setup; retry resumes checkpoint transfers. Windows computers with an NVIDIA
GPU use the official PyTorch CUDA wheel index automatically. The installer does
not change the GPU driver.

The same setup can be run from the source checkout:

```powershell
python windows/edgerock/setup.py
./windows/Build.ps1
```

The installer downloads and SHA-256-verifies a pinned checkpoint, model code,
vision frontend, and matching upstream Transformers wheel. The checkpoint is
about **9.87 GB**. By default, the fast format/meaning decision model reuses the existing
**1.89 GB Qwen3 4B Q3** weights, for about **11.76 GB** total before the speech
model and Python environment. Files are stored in `windows/models/edgerock-4b` and
`windows/runtime/edgerock`. Failed checkpoint transfers can resume. No model
weights or user data are added to Git.

For a portable app, pass `--root` with its **OpenMicAI-data** directory. For a
thin source build, use its EXE directory. Rebuild the EXE with this change; the
adapter is embedded in it. The speech runtime and Parakeet model are still
required separately, as described in the [Windows guide](windows.md).

The installer uses matching PyTorch 2.11.0 and Torchvision 0.26.0 packages, including
the video-processing dependency required when Transformers loads the image processor.
It accepts `--device cpu`, `--device cuda`, or `--device auto` (default).
For GPU use, install a PyTorch build supporting the GPU; an official wheel index
can be passed with `--torch-index`. The CPU path uses FP32 and needs substantially
more RAM than the checkpoint's download size. CUDA uses BF16 where supported.
This integration uses portable eager PyTorch, not the optimized Hopper kernels.

Select EdgeRock under **Settings → System → Models**, then load models. Startup
performs the usual speech and rewrite warm-up. Setup completion means the files
and dependencies are installed; it is not an inference or quality certification.

## Output selection

Polish already has a decision pass: `ChoosePolishLayout` chooses among 19 layouts.
The **Decision model** selector defaults to **Automatic**: with a Qwen language
choice it uses that resident model, and with EdgeRock it uses the tested Qwen3
4B Q3 model for layout decisions and meaning checks. You can select Qwen3 1.7B
Q4 (compact, experimental), Qwen3 4B Q3 or Qwen3 4B Q4 independently. The enabled
**Use Qwen for faster dictation and text edits** option also sends writing to Qwen,
while EdgeRock answers image questions. Disable it to use EdgeRock for writing.
If direct EdgeRock writing exceeds eight seconds across its polishing passes,
the request is cancelled and retried once through the fast Qwen writing model.
Liquid d1 Omni 600M and Simple Jev/Qwen 0.8B are optional dedicated decision
backends. When either is selected, it handles decisions on CPU while Qwen 4B Q3
remains a separate writing process; a non-generative model is never used to write.
The retry keeps the existing meaning and word-preservation checks. User
cancellation stops the operation without starting a fallback. For consistently
shorter dictation latency, keep the faster-writing option enabled.
This avoids a verified shopping-list versus
checklist error and slow decision requests on EdgeRock. Existing Qwen weights
are reused; otherwise the app downloads them with the pinned catalog hash.
Explicit layout requests can bypass the planner. The decision path warms before
the app reports Ready. A second Vulkan GPU is used for Qwen when available;
otherwise the decision model runs on CPU to leave GPU memory for EdgeRock.

The EdgeRock adapter requests short, direct answers with an empty reasoning
prefix. LM Format Enforcer constrains schema-based tasks during generation,
and JSON Schema validation checks the result again afterward. The upstream
lightweight server rejects `response_format`, so this adapter supplies that
contract. Invalid JSON, invalid schema values,
empty answers, and truncated answers fail instead of being used as edits or
affirmative meaning verdicts. Existing content and word-preservation checks
remain active. EdgeRock's direct-answer editing quality needs model-based tests.

## Ask about an image

Choose **Chat** in the sidebar:

1. **Attach image** selects a PNG, JPEG or BMP, or **Select screen region** lets
   you drag a region. Escape cancels region selection. The app hides its window
   before a region capture.
2. Type a question and press **Ask**, or choose **Speak question**. Press
   **Stop and ask** to transcribe and send the question. Recording stops after
   60 seconds. The configured microphone and local speech engine are used.
3. Review the answer and choose **Copy answer**. Answers are not automatically
   pasted into another app.

Continue with a typed or spoken follow-up; the same image and recent conversation
are included. Text-only chats work too. Selecting a new image, removing the image
or choosing **New chat** clears the previous conversation. Navigation preserves
completed turns until the main window closes. The adapter currently resends a
bounded recent history; it does not persist or reuse model KV/recurrent states
between HTTP requests. Image selection therefore does not create permanent RMA
memory. Shared-hotkey image answers seed the Chat conversation for follow-ups.

The **same dictation shortcut** handles the three input modes. Select an image
and press the shortcut to speak an image question, select text to speak an edit,
or place the cursor in a text field to dictate normally. Press again to finish
(or use the existing hold-to-talk/automatic recording mode). Image questions show
an **Ask about image** recording card, with no text rewrite/translation actions.
Answers appear on a floating result card and are never automatically pasted.

An optional image-capture shortcut can be enabled under **Settings → Access →
Shortcuts**; it is off by default. Its first choice is Ctrl + Alt + I. If the
selected image is available through Copy it offers an **Ask about image** card;
otherwise it opens region selection. This is useful when an app does not expose
selected images. If the chosen shortcut is in use, the app tries Ctrl + Shift + F7,
then Ctrl + Alt + F7.

Text selections retain the existing text actions. There is no background
screen scanning or clipboard polling. Browser images, canvas editors and other
apps differ in what a selection exposes; a highlighted image is not universally
available as bitmap data, which is why region capture remains available. Remote
image links are never fetched automatically.

Images and questions stay in memory. The adapter listens only on loopback, uses
a per-process bearer token, disables request logging, and accepts attached PNG
data rather than remote image URLs. Images and answers are not added to history.
The vision frontend loads at startup so it does not wait until the first question.
This path does not use the upstream vision benchmark's FlashAttention kernels.
The vision tower uses native PyTorch SDPA and caps image processing at 262,144
pixels. On CUDA GPUs below 16 GiB, that tower runs on CPU and transfers its
features to the GPU language model, leaving more graphics memory for generation.
An image allocation failure retries once at 65,536 pixels. If it still fails,
the app displays a memory-specific explanation instead of a generic HTTP error.
The original attachment remains in memory; these limits apply to model input.

## Architecture and reported results

These mechanisms are inside EdgeRock's trained model and runtime:

- **Recurrent state** carries a compressed, evolving summary as tokens arrive.
- **Local attention** keeps detailed interactions over the most recent 1,024
  positions.
- **Rock Memory Anchors** write learned KV records into a fixed-capacity bank.
  Queries can retrieve selected distant information after its original tokens
  leave the local window. The bank has 1,024 records per KV group, with admission
  updates every 256 tokens. It is selective memory, not a lossless transcript.

The developer reports **82.9% mean AIME pass@1 across 2024–2026**, **72 MiB of
attention KV per sequence** (99.1% less than Qwen3.5-4B at 256K context), and up
to **6.42× batched output throughput** on a Hopper 80GB GPU, comparing each
model's best measured batch with 32K input and 32K output. These are developer
results, not Open Mic AI measurements. The 72 MiB figure excludes model weights,
recurrent state and other runtime memory. AIME measures mathematical reasoning,
not dictation accuracy. Neither figure establishes faster short edits on a
consumer GPU. The original model supports text and images, not raw audio; our
speech recognizer still converts the spoken question to text first.

Sources: [model card](https://huggingface.co/rockmatrix-ai/EdgeRock),
[runtime and setup](https://github.com/rockmatrix-ai/EdgeRock/blob/main/USAGE.md),
[upstream code and architecture](https://github.com/rockmatrix-ai/EdgeRock).

## Validation

```powershell
./windows/runtime/edgerock/venv/Scripts/python.exe -m unittest discover -s windows/edgerock -p test_server.py
./windows/validation/RunCoreChecks.ps1
./windows/validation/RunModelLoadingChecks.ps1
./windows/validation/RunEdgeRockChecks.ps1
./windows/validation/RunInferenceSmokeChecks.ps1 -LanguageModel edgerock-4b -FastWriting
```

Protocol tests use a deterministic fixture, without weights, a microphone or
screen capture. They cover schema rejection, truncated answers, reasoning removal,
image decoding, blocked remote URLs and loopback authentication. They do not
establish EdgeRock inference correctness, image-answer quality, or latency.
Before recommending this model, run actual text, all 19 layout, selected-text,
image and cancellation checks on the target CPU/GPU and compare them with Qwen.

Live checks on the development PC (RTX 5070 for EdgeRock, RTX 2060 for Qwen)
passed text edits, all 19 implied layout choices, image OCR and a synthetic spoken
image question. In the fast-writing configuration, short text edits took about
1 second, warm layout decisions about 0.2 seconds, warm image OCR about 2.3 seconds,
and the spoken number question about 1.8 seconds. The first image request took
about 3.9 seconds; a request immediately after cancellation took 2.6 seconds. These are small synthetic smoke
checks, not a general accuracy benchmark or a latency guarantee. EdgeRock writing
without the fast option took about 4–6 seconds for the tested short edits.
In a later run with the eight-second writing budget, slower direct edits
recovered through Qwen in 9.3–10.1 seconds. A forced early timeout also completed
successfully, and image questions still worked after cancellation. Runtime load,
prompt shape and current GPU activity affect these timings.
After enabling SDPA and the CPU vision tower on the 12 GiB GPU, the local
integration checks passed image reading, chart values, follow-up arithmetic and
cancellation. Small image questions took roughly 3.5–7.2 seconds; two synthetic
chart questions took about 10–11 seconds. Those are smoke checks, not a general
chart-reading accuracy benchmark.
