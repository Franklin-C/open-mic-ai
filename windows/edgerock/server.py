"""Private loopback adapter for the pinned EdgeRock checkpoint. No cloud calls."""
import argparse
import base64
import hmac
import io
import json
import os
from pathlib import Path
import sys
import threading
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MAX_REQUEST = 12 * 1024 * 1024


def tokenizer_data(tokenizer):
    """Use LMFE's public core API; its released Transformers adapter predates v5."""
    from lmformatenforcer.tokenenforcer import TokenEnforcerTokenizerData
    special = set(tokenizer.all_special_ids)
    prefix = tokenizer.encode("0", add_special_tokens=False)[-1]
    prefix_length = len(tokenizer.decode([prefix]))
    regular = []
    for start in range(0, len(tokenizer), 4096):
        ids = [i for i in range(start, min(start + 4096, len(tokenizer))) if i not in special]
        alone = tokenizer.batch_decode([[i] for i in ids], clean_up_tokenization_spaces=False)
        joined = tokenizer.batch_decode([[prefix, i] for i in ids], clean_up_tokenization_spaces=False)
        for token, one, two in zip(ids, alone, joined):
            suffix = two[prefix_length:]
            regular.append((token, suffix, len(suffix) > len(one)))
    return TokenEnforcerTokenizerData(regular, lambda ids: tokenizer.decode(ids, clean_up_tokenization_spaces=False).rstrip("�"), tokenizer.eos_token_id, False, len(tokenizer))


def schema_filter(data, schema):
    from lmformatenforcer import JsonSchemaParser
    from lmformatenforcer.tokenenforcer import TokenEnforcer
    enforcer = TokenEnforcer(data, JsonSchemaParser(schema))
    return lambda batch, ids: enforcer.get_allowed_tokens(ids.tolist()).allowed_tokens


def final_text(text):
    # Reasoning is never exposed as an edit or accepted as a JSON verdict.
    if "</think>" in text:
        text = text.rsplit("</think>", 1)[1]
    elif "<think>" in text:
        raise ValueError("The model did not finish its reasoning.")
    return text.strip()


def structured_text(text, schema):
    from jsonschema import Draft202012Validator
    from jsonschema.exceptions import ValidationError
    text = final_text(text)
    if text.startswith("```json\n") and text.endswith("```"):
        text = text[8:-3].strip()
    value = json.loads(text)
    try:
        Draft202012Validator(schema).validate(value)
    except ValidationError as error:
        raise ValueError("The answer does not satisfy the required schema.") from error
    return json.dumps(value, ensure_ascii=False)


def image_from_data_url(url):
    from PIL import Image
    if not isinstance(url, str) or not url.startswith("data:image/png;base64,"):
        raise ValueError("Only an attached PNG is accepted; remote images are disabled.")
    raw = base64.b64decode(url.split(",", 1)[1], validate=True)
    if len(raw) > 8 * 1024 * 1024:
        raise ValueError("The image is too large.")
    with Image.open(io.BytesIO(raw)) as image:
        if image.width * image.height > 16_000_000:
            raise ValueError("The image has too many pixels.")
        image.load()
        return image.convert("RGB")


class EdgeRock:
    def __init__(self, model, device):
        import torch
        from transformers import AutoModelForCausalLM, AutoTokenizer
        self.torch = torch
        self.path = Path(model).resolve()
        sys.path.insert(0, str(self.path))
        sys.path.insert(0, str(self.path / "runtime"))
        self.device = device if device != "auto" else "cuda" if torch.cuda.is_available() else "cpu"
        self.dtype = torch.bfloat16 if self.device == "cuda" and torch.cuda.is_bf16_supported() else torch.float32
        self.tokenizer = AutoTokenizer.from_pretrained(self.path, trust_remote_code=True, local_files_only=True)
        self.enforcer_data = tokenizer_data(self.tokenizer)
        self.model = AutoModelForCausalLM.from_pretrained(
            self.path, trust_remote_code=True, local_files_only=True,
            dtype=self.dtype, backend="torch", attn_implementation="eager",
        ).to(self.device).eval().requires_grad_(False)
        self.visual = self.processor = None
        self.vision = (self.path / "vision/model.safetensors").is_file()
        if self.vision:
            self.load_vision()  # Pay weight loading at startup, before accepting image requests.

    def load_vision(self):
        if self.visual is not None:
            return
        from safetensors.torch import load_file
        from transformers import AutoConfig, AutoProcessor
        from transformers.models.qwen3_5.modeling_qwen3_5 import Qwen3_5VisionModel
        from vision import VisualModel
        config = AutoConfig.from_pretrained(self.path / "vision", local_files_only=True)
        config.vision_config._attn_implementation = "eager"
        visual = Qwen3_5VisionModel(config.vision_config)
        visual.load_state_dict(load_file(str(self.path / "vision/model.safetensors")))
        self.visual = VisualModel(self.model, config, visual).to(self.device, dtype=self.dtype).eval().requires_grad_(False)
        self.processor = AutoProcessor.from_pretrained(self.path / "vision", local_files_only=True)
        self.processor.image_processor.size = {"longest_edge": 1048576, "shortest_edge": 65536}

    def generate(self, messages, limit, schema=None, stop=None):
        torch = self.torch
        if stop is not None and stop.is_set():
            raise InterruptedError("Cancelled")
        image = None
        normalized = []
        for message in messages:
            role, content = message["role"], message["content"]
            if role not in {"system", "user", "assistant"}:
                raise ValueError("Unsupported message role.")
            if isinstance(content, list):
                parts = []
                for part in content:
                    if part["type"] == "image_url" and role == "user" and image is None:
                        image = image_from_data_url(part["image_url"]["url"])
                        parts.append({"type": "image"})
                    elif part["type"] == "text":
                        parts.append({"type": "text", "text": part["text"]})
                    else:
                        raise ValueError("Only one attached image is supported.")
                content = parts
            elif not isinstance(content, str):
                raise ValueError("Invalid message content.")
            normalized.append({"role": role, "content": content})
        if image is None:
            prompt = "".join(f"<|im_start|>{m['role']}\n{m['content']}<|im_end|>\n" for m in normalized)
            # Short editing/planning tasks must not spend their output budget reasoning.
            prompt += "<|im_start|>assistant\n<think>\n</think>\n"
            inputs = self.tokenizer(prompt, return_tensors="pt").to(self.device)
            generation = {}
            if stop is not None:
                from transformers import StoppingCriteria
                class Cancelled(StoppingCriteria):
                    def __call__(self, input_ids, scores, **kwargs):
                        return torch.full((input_ids.shape[0],), stop.is_set(), dtype=torch.bool, device=input_ids.device)
                generation["stopping_criteria"] = [Cancelled()]
            if schema:
                generation["prefix_allowed_tokens_fn"] = schema_filter(self.enforcer_data, schema)
            with torch.inference_mode():
                output = self.model.generate(**inputs, max_new_tokens=limit, do_sample=False,
                                             pad_token_id=self.tokenizer.pad_token_id,
                                             eos_token_id=sorted(self.eos_ids), **generation)
            tokens = output[0, inputs.input_ids.shape[1]:].tolist()
            prompt_count = inputs.input_ids.shape[1]
        else:
            if not self.vision:
                raise ValueError("The vision checkpoint is not installed.")
            self.load_vision()
            prompt = self.processor.apply_chat_template(normalized, tokenize=False, add_generation_prompt=True, enable_thinking=False)
            pixels = self.processor.image_processor(images=image, return_tensors="pt")
            count = int(pixels["image_grid_thw"].prod()) // self.processor.image_processor.merge_size ** 2
            prompt = prompt.replace(self.processor.image_token, self.processor.image_token * count, 1)
            inputs = self.tokenizer(prompt, return_tensors="pt").to(self.device)
            prompt_count = inputs.input_ids.shape[1]
            tokens = []
            with torch.inference_mode():
                output = self.visual(input_ids=inputs.input_ids, attention_mask=inputs.attention_mask,
                                     pixel_values=pixels["pixel_values"].to(self.device),
                                     image_grid_thw=pixels["image_grid_thw"].to(self.device))
                positions = output["vision_decode_positions"]
                for step in range(limit):
                    if stop is not None and stop.is_set():
                        raise InterruptedError("Cancelled")
                    token = output.logits[:, -1].argmax(-1, keepdim=True)
                    tokens.append(token.item())
                    if tokens[-1] in self.eos_ids or step + 1 == limit:
                        break
                    output = self.model(input_ids=token, attention_mask=torch.ones_like(token),
                                        position_ids=positions, past_key_values=output.past_key_values,
                                        use_cache=True, logits_to_keep=1)
                    positions = positions + 1
        if stop is not None and stop.is_set():
            raise InterruptedError("Cancelled")
        return {"text": self.tokenizer.decode(tokens, skip_special_tokens=True),
                "finish_reason": "stop" if tokens and tokens[-1] in self.eos_ids else "length",
                "prompt_tokens": prompt_count, "completion_tokens": len(tokens)}

    @property
    def eos_ids(self):
        eos = self.model.generation_config.eos_token_id
        values = set(eos if isinstance(eos, list) else [eos])
        values.add(self.tokenizer.eos_token_id)
        values.add(self.tokenizer.convert_tokens_to_ids("<|im_end|>"))
        return {value for value in values if isinstance(value, int) and value >= 0}


def complete(engine, request, stop=None):
    messages = request["messages"]
    if not isinstance(messages, list) or not messages or len(messages) > 100:
        raise ValueError("Invalid chat messages.")
    limit = int(request.get("max_tokens", 1024))
    if not 1 <= limit <= 4096 or request.get("stream"):
        raise ValueError("Use non-streaming requests with 1–4096 output tokens.")
    schema = request.get("response_format", {}).get("json_schema", {}).get("schema")
    if schema:
        messages = [{"role": "system", "content": "Return only JSON satisfying this schema: " + json.dumps(schema)}] + messages
    # A failed schema is never allowed to become an affirmative meaning verdict.
    if stop is not None and stop.is_set():
        raise InterruptedError("Cancelled")
    result = engine.generate(messages, limit, schema, stop)
    if result["finish_reason"] == "length":
        raise ValueError("The local model's answer was cut short. Try a shorter request.")
    text = structured_text(result["text"], schema) if schema else final_text(result["text"])
    if not text:
        raise ValueError("The local model returned no answer.")
    return {"choices": [{"message": {"role": "assistant", "content": text}, "finish_reason": "stop"}],
            "usage": {"prompt_tokens": result["prompt_tokens"], "completion_tokens": result["completion_tokens"],
                      "total_tokens": result["prompt_tokens"] + result["completion_tokens"]}}


def handler_for(engine, secret):
    lock = threading.Lock()
    operations = {}
    cancelled = set()
    operations_lock = threading.Lock()

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass  # No transcript, image or answer logs.

        def send_json(self, value, status=200):
            body = json.dumps(value, ensure_ascii=False).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            try:
                self.wfile.write(body)
            except (BrokenPipeError, ConnectionResetError):
                pass

        def authenticated(self):
            if not hmac.compare_digest(self.headers.get("Authorization", ""), "Bearer " + secret):
                self.send_json({"error": "Unauthorized"}, 401)
                return False
            return True

        def do_GET(self):
            if not self.authenticated():
                return
            if self.path == "/health":
                self.send_json({"status": "ok", "vision": engine.vision})
            else:
                self.send_json({"error": "Not found"}, 404)

        def do_POST(self):
            if not self.authenticated():
                return
            if self.path.startswith("/cancel/"):
                identity = self.path[len("/cancel/"):]
                with operations_lock:
                    event = operations.get(identity)
                    if event is not None:
                        event.set()
                    elif len(cancelled) < 64:
                        cancelled.add(identity)
                self.send_json({"cancelled": True})
                return
            if self.path != "/v1/chat/completions":
                self.send_json({"error": "Not found"}, 404)
                return
            identity = self.headers.get("X-OpenMic-Request") or uuid.uuid4().hex
            if len(identity) != 32 or any(c not in "0123456789abcdef" for c in identity):
                self.send_json({"error": "Invalid request id"}, 400)
                return
            event = threading.Event()
            with operations_lock:
                if identity in cancelled:
                    event.set()
                    cancelled.discard(identity)
                operations[identity] = event
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if not 0 < length <= MAX_REQUEST:
                    self.send_json({"error": "Request too large"}, 413)
                    return
                request = json.loads(self.rfile.read(length))
                with lock:
                    response = complete(engine, request, event)
                self.send_json(response)
            except InterruptedError:
                self.send_json({"error": "Cancelled"}, 499)
            except (ValueError, KeyError, TypeError):
                self.send_json({"error": "Invalid, incomplete or unverified model response. Original text is unchanged."}, 422)
            except Exception:
                self.send_json({"error": "Local model inference failed. Original text is unchanged."}, 500)
            finally:
                with operations_lock:
                    operations.pop(identity, None)

    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True)
    parser.add_argument("--port", required=True, type=int)
    parser.add_argument("--device", choices=["auto", "cpu", "cuda"], default="auto")
    args = parser.parse_args()
    secret = os.environ.pop("OPENMIC_EDGEROCK_TOKEN")
    engine = EdgeRock(args.model, args.device)
    ThreadingHTTPServer(("127.0.0.1", args.port), handler_for(engine, secret)).serve_forever()


if __name__ == "__main__":
    main()
