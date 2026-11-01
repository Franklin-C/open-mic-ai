"""Install the experimental local backend: python windows/edgerock/setup.py."""
import argparse
from contextlib import contextmanager
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import threading
import urllib.request
import venv

_progress_path = None
_progress_lock = threading.Lock()


def report(message):
    print(message, flush=True)
    if _progress_path is not None:
        with _progress_lock:
            partial = _progress_path.with_suffix(".tmp")
            partial.write_text(json.dumps({"message": message}), encoding="utf-8")
            partial.replace(_progress_path)


@contextmanager
def setup_lock(root):
    folder = root.resolve() / "runtime/edgerock"
    folder.mkdir(parents=True, exist_ok=True)
    with (folder / "setup.lock").open("a+b") as lock:
        lock.seek(0, 2)
        if not lock.tell():
            lock.write(b"0")
            lock.flush()
        lock.seek(0)
        try:
            if sys.platform == "win32":
                import msvcrt
                msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as error:
            raise RuntimeError("EdgeRock setup is already running. Wait for it to finish.") from error
        try:
            yield
        finally:
            lock.seek(0)
            if sys.platform == "win32":
                msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(lock.fileno(), fcntl.LOCK_UN)


def verified(path, item):
    if not path.is_file() or path.stat().st_size != item["bytes"]:
        return False
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest() == item["sha256"]


def download(base, item, destination):
    path = destination / item["name"]
    if verified(path, item):
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    partial = path.with_name(path.name + ".partial")
    offset = partial.stat().st_size if partial.exists() else 0
    if offset > item["bytes"]:
        partial.unlink()
        offset = 0
    while offset < item["bytes"]:
        end = min(item["bytes"], offset + 64 * 1024 * 1024) - 1
        request = urllib.request.Request(base + item["name"], headers={"Range": f"bytes={offset}-{end}"})
        with urllib.request.urlopen(request, timeout=120) as response:
            append = response.status == 206
            if append and response.headers.get("Content-Range") != f"bytes {offset}-{end}/{item['bytes']}":
                raise ValueError("Unexpected download range")
            expected = end - offset + 1 if append else item["bytes"]
            received = 0
            with partial.open("ab" if append else "wb") as output:
                while received < expected:
                    chunk = response.read(min(1024 * 1024, expected - received))
                    if not chunk:
                        raise ValueError("Incomplete download; run setup again to resume.")
                    output.write(chunk)
                    received += len(chunk)
            offset = partial.stat().st_size
            if item["bytes"] > 64 * 1024 * 1024:
                report(f"{item['name']}: {offset / 1e6:.0f} / {item['bytes'] / 1e6:.0f} MB")
    if not verified(partial, item):
        partial.unlink(missing_ok=True)
        raise ValueError("SHA-256 verification failed for " + item["name"])
    partial.replace(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--device", choices=["auto", "cpu", "cuda"], default="auto")
    parser.add_argument("--torch-index", help="Optional official PyTorch wheel index for your GPU")
    parser.add_argument("--download-only", action="store_true", help="Verify/download the checkpoint without installing dependencies")
    args = parser.parse_args()
    with setup_lock(args.root):
        install(args)


def install(args):
    global _progress_path
    manifest = json.loads(Path(__file__).with_name("model-manifest.json").read_text())
    root = args.root.resolve()
    environment = root / "runtime/edgerock"
    checkpoint = root / "models/edgerock-4b"
    environment.mkdir(parents=True, exist_ok=True)
    _progress_path = environment / "setup-progress.json"
    ready = environment / "setup.json"
    ready.unlink(missing_ok=True)
    missing = sum(item["bytes"] for item in manifest["files"]
                  if not (checkpoint / item["name"]).is_file())
    if shutil.disk_usage(root).free < missing + 4 * 1024 ** 3:
        raise RuntimeError("Not enough free space for the checkpoint and Python runtime.")
    report(f"Pinned EdgeRock checkpoint: {manifest['revision']}")
    report(f"Checkpoint download: {sum(f['bytes'] for f in manifest['files']) / 1e9:.2f} GB; Python dependencies are additional.")
    base = f"https://huggingface.co/rockmatrix-ai/EdgeRock/resolve/{manifest['revision']}/"
    def install_file(item):
        report("Verify / download " + item["name"])
        download(base, item, checkpoint)
    # Independent weight shards can transfer concurrently; each keeps its own
    # resumable partial file and must pass its pinned hash before admission.
    with ThreadPoolExecutor(max_workers=4) as pool:
        list(pool.map(install_file, sorted(manifest["files"], key=lambda item: item["bytes"])))
    if args.download_only:
        report("Checkpoint verified. Run setup again without --download-only to finish installation.")
        return
    venv.EnvBuilder(with_pip=True).create(environment / "venv")
    python = environment / "venv" / ("Scripts/python.exe" if sys.platform == "win32" else "bin/python")
    if not args.torch_index and args.device != "cpu" and sys.platform == "win32" and shutil.which("nvidia-smi"):
        detected = subprocess.run(["nvidia-smi", "--query-gpu=name", "--format=csv,noheader"], capture_output=True, text=True, timeout=15)
        if detected.returncode == 0 and "NVIDIA" in detected.stdout:
            args.torch_index = "https://download.pytorch.org/whl/cu130"
    report("Installing the local Python runtime" + (" with CUDA support" if args.torch_index else ""))
    torch = [str(python), "-m", "pip", "install", "--quiet", "--disable-pip-version-check", "torch==2.11.0", "torchvision==0.26.0"]
    if args.torch_index:
        torch += ["--index-url", args.torch_index]
    subprocess.run(torch, check=True)
    subprocess.run([str(python), "-m", "pip", "install", "--quiet", "--disable-pip-version-check",
                    str(checkpoint / "wheels/transformers-5.7.0.dev0-py3-none-any.whl"),
                    "safetensors>=0.7", "accelerate>=1.13", "numpy>=2", "Pillow>=12.1.1", "jsonschema>=4.23", "lm-format-enforcer==0.11.3"], check=True)
    # Do not mark installed until imports, model metadata, and every pinned file check out.
    subprocess.run([str(python), "-c", "import torch, torchvision, transformers, PIL, jsonschema, safetensors, accelerate; from lmformatenforcer.tokenenforcer import TokenEnforcerTokenizerData"], check=True)
    ready.write_text(json.dumps({"python": str(python), "model": str(checkpoint), "device": args.device,
                                "revision": manifest["revision"], "adapter_version": "1"}), encoding="utf-8")
    report("EdgeRock setup finished. Loading models is now available.")


if __name__ == "__main__":
    main()
