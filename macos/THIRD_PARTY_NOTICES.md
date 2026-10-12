# Mac components

The application source uses the MIT license in the repository root. Inference
engines and model weights retain their upstream licenses.

- **whisper.cpp**, ggml-org and contributors: [MIT](https://github.com/ggml-org/whisper.cpp/blob/master/LICENSE). Engine source pinned to `d2a764c648779feb36d44363f212a29b00aad1af` (v1.9.5). The build bundles its full license alongside the executable.
- **llama.cpp**, ggml-org and contributors: [MIT](https://github.com/ggml-org/llama.cpp/blob/master/LICENSE). Engine source pinned to `69ecae922d2b60c1fa8c590279f4e2099af75111` (v0.6.0). The build bundles its full license alongside the executable.
- **Whisper**, OpenAI: [MIT](https://github.com/openai/whisper/blob/main/LICENSE). English Base and Small weights use the GGML conversions from [ggerganov/whisper.cpp](https://huggingface.co/ggerganov/whisper.cpp), pinned to revision `5359861c739e955e79d9a303bcbc70fb988958b1`.
- **Qwen3 / Qwen3.5**, Qwen / Alibaba Cloud: [Apache 2.0](https://www.apache.org/licenses/LICENSE-2.0). Model catalog records the pinned Unsloth conversion revision and SHA-256 for each GGUF file. The models are converted and quantized upstream; this app does not train or modify them.

App downloads do not bundle model weights. For exact download URLs, revisions,
sizes and checksums, see `Sources/OpenMicAI/Resources/models.json`.
