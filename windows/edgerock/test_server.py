import base64
import io
import json
import threading
import tempfile
from pathlib import Path
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer

from server import complete, handler_for, image_from_data_url, tokenizer_data, schema_filter
from setup import setup_lock, verified


class EngineFixture:
    vision = True

    def __init__(self, text='{"layout":"prose"}', reason="stop"):
        self.text, self.reason = text, reason

    def generate(self, messages, limit, schema=None, stop=None):
        return {"text": self.text, "finish_reason": self.reason,
                "prompt_tokens": 10, "completion_tokens": 3}


class AdapterChecks(unittest.TestCase):
    def test_cancel_releases_the_generation_slot(self):
        entered = threading.Event()
        class BlockingEngine(EngineFixture):
            def generate(self, messages, limit, schema=None, stop=None):
                if not entered.is_set():
                    entered.set()
                    if not stop.wait(5):
                        raise AssertionError("Generation never received cancellation")
                    raise InterruptedError("Cancelled")
                return super().generate(messages, limit, schema, stop)
        with ThreadingHTTPServer(("127.0.0.1", 0), handler_for(BlockingEngine(), "fixture-secret")) as server:
            worker = threading.Thread(target=server.serve_forever, daemon=True); worker.start()
            base = f"http://127.0.0.1:{server.server_port}"
            http = urllib.request.build_opener(urllib.request.ProxyHandler({}))
            errors = []
            def generate():
                request = urllib.request.Request(base + "/v1/chat/completions", data=json.dumps(self.request()).encode(),
                    headers={"Authorization": "Bearer fixture-secret", "X-OpenMic-Request": "a" * 32})
                try:
                    http.open(request, timeout=6)
                except urllib.error.HTTPError as error:
                    errors.append(error.code)
            first = threading.Thread(target=generate); first.start()
            try:
                self.assertTrue(entered.wait(2))
                request = urllib.request.Request(base + "/cancel/" + "a" * 32, data=b"", headers={"Authorization": "Bearer fixture-secret"})
                self.assertTrue(json.load(http.open(request, timeout=2))["cancelled"])
                first.join(2)
                self.assertFalse(first.is_alive())
                self.assertEqual(errors, [499])
                request = urllib.request.Request(base + "/v1/chat/completions", data=json.dumps(self.request()).encode(), headers={"Authorization": "Bearer fixture-secret"})
                self.assertEqual(json.load(http.open(request, timeout=2))["choices"][0]["finish_reason"], "stop")
            finally:
                server.shutdown(); worker.join(); first.join()

    def test_schema_filter_rejects_non_layout_values(self):
        class Tokenizer:
            alphabet = list(dict.fromkeys('0{}[]":,abcdefghijklmnopqrstuvwxyz \n'))
            eos_token_id = len(alphabet)
            all_special_ids = [eos_token_id]
            def __len__(self): return len(self.alphabet) + 1
            def encode(self, text, **kwargs): return [self.alphabet.index(c) for c in text]
            def decode(self, ids, **kwargs): return ''.join(self.alphabet[i] for i in ids)
            def batch_decode(self, rows, **kwargs): return [self.decode(row) for row in rows]
        class Tokens(list):
            def tolist(self): return list(self)
        tokenizer = Tokenizer()
        schema = {"type": "object", "properties": {"layout": {"enum": ["prose", "list"]}},
                  "required": ["layout"], "additionalProperties": False}
        allowed = schema_filter(tokenizer_data(tokenizer), schema)
        output = Tokens(tokenizer.encode('0'))  # Initial sequence is prompt, not generated JSON.
        for character in '{"layout":"':
            token = tokenizer.encode(character)[0]
            self.assertIn(token, allowed(0, output))
            output.append(token)
        candidates = allowed(0, output)
        self.assertIn(tokenizer.encode('l')[0], candidates)
        self.assertIn(tokenizer.encode('p')[0], candidates)
        self.assertNotIn(tokenizer.encode('x')[0], candidates)

    def test_setup_serializes_installers(self):
        with tempfile.TemporaryDirectory() as folder:
            with setup_lock(Path(folder)):
                with self.assertRaises(RuntimeError):
                    with setup_lock(Path(folder)):
                        self.fail("A second installer acquired the checkpoint lock")

    def test_checkpoint_admission_requires_size_and_hash(self):
        import hashlib
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "weights"
            path.write_bytes(b"verified fixture")
            item = {"bytes": 16, "sha256": hashlib.sha256(b"verified fixture").hexdigest()}
            self.assertTrue(verified(path, item))
            path.write_bytes(b"modified fixture")
            self.assertFalse(verified(path, item))

    def request(self, **changes):
        request = {"messages": [{"role": "user", "content": "A short message."}], "max_tokens": 32,
                   "response_format": {"json_schema": {"schema": {
                       "type": "object", "properties": {"layout": {"enum": ["prose", "list"]}},
                       "required": ["layout"], "additionalProperties": False}}}}
        request.update(changes)
        return request

    def test_planner_schema_and_usage(self):
        result = complete(EngineFixture(), self.request())
        self.assertEqual(json.loads(result["choices"][0]["message"]["content"]), {"layout": "prose"})
        self.assertEqual(result["usage"]["total_tokens"], 13)

    def test_reasoning_is_not_an_edit(self):
        result = complete(EngineFixture('<think>private reasoning</think>{"layout":"list"}'), self.request())
        self.assertNotIn("reasoning", result["choices"][0]["message"]["content"])
        with self.assertRaises(ValueError):
            complete(EngineFixture('<think>unfinished'), self.request())

    def test_invalid_or_unknown_layout_rejected(self):
        for text in ['not json', '{"layout":"invented"}', '{"layout":"prose","safe":true}']:
            with self.subTest(text=text), self.assertRaises(Exception):
                complete(EngineFixture(text), self.request())

    def test_truncated_affirmative_verdict_rejected(self):
        with self.assertRaises(ValueError):
            complete(EngineFixture('{"safe":true}', "length"), self.request())

    def test_verdict_requires_boolean(self):
        request = self.request(response_format={"json_schema": {"schema": {
            "type": "object", "properties": {"safe": {"type": "boolean"}},
            "required": ["safe"], "additionalProperties": False}}})
        with self.assertRaises(Exception):
            complete(EngineFixture('{"safe":"true"}'), request)

    def test_empty_answer_rejected(self):
        with self.assertRaises(ValueError):
            complete(EngineFixture(" "), self.request(response_format={}))

    def test_no_remote_image_fetch(self):
        for url in ["https://example.com/private.png", "file:///private.png", "data:image/png;base64,!!!"]:
            with self.subTest(url=url), self.assertRaises(ValueError):
                image_from_data_url(url)

    def test_png_round_trip(self):
        from PIL import Image
        with Image.new("RGB", (16, 12), "red") as source, io.BytesIO() as output:
            source.save(output, format="PNG")
            decoded = image_from_data_url("data:image/png;base64," + base64.b64encode(output.getvalue()).decode())
            self.assertEqual(decoded.size, (16, 12))
            self.assertEqual(decoded.getpixel((0, 0)), (255, 0, 0))

    def test_authenticated_loopback_protocol(self):
        with ThreadingHTTPServer(("127.0.0.1", 0), handler_for(EngineFixture(), "fixture-secret")) as server:
            worker = threading.Thread(target=server.serve_forever, daemon=True)
            worker.start()
            base = f"http://127.0.0.1:{server.server_port}"
            # Disable proxies just as the desktop client does.
            http = urllib.request.build_opener(urllib.request.ProxyHandler({}))
            try:
                with self.assertRaises(urllib.error.HTTPError) as error:
                    http.open(base + "/health")
                self.assertEqual(error.exception.code, 401)
                request = urllib.request.Request(base + "/health", headers={"Authorization": "Bearer fixture-secret"})
                self.assertTrue(json.load(http.open(request))["vision"])
                request = urllib.request.Request(base + "/v1/chat/completions", data=json.dumps(self.request()).encode(),
                                                 headers={"Authorization": "Bearer fixture-secret", "Content-Type": "application/json"})
                self.assertEqual(json.load(http.open(request))["choices"][0]["finish_reason"], "stop")
            finally:
                server.shutdown()
                worker.join()


if __name__ == "__main__":
    unittest.main()
