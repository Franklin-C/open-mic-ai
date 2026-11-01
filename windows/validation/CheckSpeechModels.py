"""Live checks for downloaded speech GGUFs using synthetic WAV fixtures."""
import argparse
import json
from pathlib import Path
import re
import socket
import subprocess
import time
import urllib.request


def run():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("models", nargs="+")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    root = args.root.resolve()
    fixtures = [("speech-fixture.wav", ["number", "picture"]),
                ("speech-folder.wav", ["blue", "folder", "friday"]),
                ("speech-negation.wav", ["not", "old", "invoice"]),
                ("speech-quantities.wav", ["two", "apples", "three", "bananas"])]
    cases = []
    http = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    for name in args.models:
        model = root / "models" / name
        if not model.is_file():
            raise FileNotFoundError(model)
        with socket.socket() as port_picker:
            port_picker.bind(("127.0.0.1", 0))
            port = port_picker.getsockname()[1]
        started = time.perf_counter()
        engine = root / "runtime/speech/parakeet-server.exe"
        with (root / "work" / (name + ".speech.log")).open("wb") as log:
            process = subprocess.Popen([str(engine), "--model", str(model), "--host", "127.0.0.1", "--port", str(port), "--threads", "8"],
                cwd=engine.parent, stdout=subprocess.DEVNULL, stderr=log, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            row = {"model": name, "cases": []}
            try:
                while time.perf_counter() - started < 60:
                    if process.poll() is not None:
                        raise RuntimeError("Speech runtime stopped; inspect " + str(log.name))
                    try:
                        with socket.create_connection(("127.0.0.1", port), timeout=0.2):
                            break
                    except OSError:
                        time.sleep(0.05)
                else:
                    raise TimeoutError("Speech model did not load")
                row["load_seconds"] = time.perf_counter() - started
                for filename, expected in fixtures:
                    path = root / "work" / filename
                    if not path.exists():
                        continue
                    boundary = "OpenMicSpeechFixture"
                    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"fixture.wav\"\r\nContent-Type: audio/wav\r\n\r\n".encode()
                            + path.read_bytes() + f"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"response_format\"\r\n\r\njson\r\n--{boundary}--\r\n".encode())
                    request = urllib.request.Request(f"http://127.0.0.1:{port}/v1/audio/transcriptions", data=body,
                        headers={"Content-Type": "multipart/form-data; boundary=" + boundary})
                    began = time.perf_counter()
                    output = json.load(http.open(request, timeout=30))["text"]
                    words = re.findall(r"[a-z]+|\d+", output.lower())
                    words = [{"2": "two", "3": "three"}.get(word, word) for word in words]
                    result = {"fixture": filename, "seconds": time.perf_counter()-began, "output": output,
                              "pass": all(word in words for word in expected)}
                    row["cases"].append(result)
                    print(json.dumps({"model": name, **result}), flush=True)
                row["pass"] = bool(row["cases"]) and all(case["pass"] for case in row["cases"])
            except Exception as error:
                row.update(error=str(error), **{"pass": False})
                print(json.dumps(row), flush=True)
            finally:
                process.terminate()
                process.wait(timeout=10)
            cases.append(row)
    output = root / "work/speech-model-checks.json"
    output.write_text(json.dumps({"complete": all(row["pass"] for row in cases), "models": cases}, indent=2), encoding="utf-8")
    return 0 if all(row["pass"] for row in cases) else 1


if __name__ == "__main__":
    raise SystemExit(run())
