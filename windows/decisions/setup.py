"""Install a pinned, local decision backend independently of EdgeRock."""
import argparse
from concurrent.futures import ThreadPoolExecutor
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import urllib.request
import venv


def verified(path, item):
    if not path.is_file() or path.stat().st_size != item['bytes']:
        return False
    with path.open('rb') as source:
        digest=hashlib.sha256()
        for chunk in iter(lambda:source.read(1024*1024),b''):digest.update(chunk)
        return digest.hexdigest() == item['sha256']


def download(item, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    if verified(target, item):
        return
    parts = target.with_name(target.name + '.parts')
    parts.mkdir(exist_ok=True)
    blocks = [(start, min(start + 8*1024*1024, item['bytes'])-1)
              for start in range(0, item['bytes'], 8*1024*1024)]
    def fetch(block):
        start, end = block
        part = parts / str(start)
        if part.is_file() and part.stat().st_size == end-start+1:
            return
        for attempt in range(5):
            try:
                request = urllib.request.Request(item['url'], headers={'Range': f'bytes={start}-{end}'})
                with urllib.request.urlopen(request, timeout=30) as response:
                    if response.status == 206:
                        if response.headers.get('Content-Range') != f"bytes {start}-{end}/{item['bytes']}":
                            raise ValueError('Unexpected download range')
                    elif start or end+1 != item['bytes']:
                        raise ValueError('Server did not support resumable transfer')
                    data = response.read(end-start+2)
                if len(data) != end-start+1:
                    raise ValueError('Incomplete download block')
                part.write_bytes(data)
                return
            except Exception:
                if attempt == 4:
                    raise
                time.sleep(1)
    with ThreadPoolExecutor(max_workers=4) as pool:
        for index, _ in enumerate(pool.map(fetch, blocks)):
            if len(blocks) > 1:
                print(f"Downloading {target.name}: {index+1}/{len(blocks)} blocks", flush=True)
    partial = target.with_name(target.name + '.partial')
    with partial.open('wb') as output:
        for start, _ in blocks:
            with (parts/str(start)).open('rb') as source:
                shutil.copyfileobj(source, output)
    if not verified(partial, item):
        partial.unlink(missing_ok=True)
        for start, _ in blocks:(parts/str(start)).unlink(missing_ok=True)
        parts.rmdir()
        raise ValueError('SHA-256 mismatch: ' + target.name)
    partial.replace(target)
    # Delete only individually named transfer blocks, inside this model's parts folder.
    for start, _ in blocks:
        (parts/str(start)).unlink()
    parts.rmdir()


@contextmanager
def setup_lock(folder):
    folder.mkdir(parents=True, exist_ok=True)
    with (folder/'setup.lock').open('a+b') as lock:
        lock.seek(0, 2)
        if not lock.tell():
            lock.write(b'0'); lock.flush()
        lock.seek(0)
        if sys.platform == 'win32':
            import msvcrt
            msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        yield


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--backend', required=True)
    parser.add_argument('--download-only', action='store_true')
    args = parser.parse_args()
    manifest = json.loads(Path(__file__).with_name('model-manifest.json').read_text(encoding='utf-8'))
    backend = manifest['backends'][args.backend]
    root = args.root.resolve()
    environment = root/'runtime/decisions'
    checkpoint = root/'models/decisions'/args.backend
    with setup_lock(environment):
        ready = environment/(args.backend+'.json')
        ready.unlink(missing_ok=True)
        missing = sum(f['bytes'] for f in backend['files'] if not (checkpoint/f['name']).exists())
        if shutil.disk_usage(root).free < missing*2 + 2*1024**3:
            raise RuntimeError('Not enough free space for the models, resumable transfers and runtime.')
        for item in backend['files']:
            print('Verify / download '+item['name'], flush=True)
            download(item, checkpoint/item['name'])
        if args.download_only:
            return
        # Keep Python's many small dependency files on the local user drive even
        # when the checkout/model library is on a network drive.
        runtime = Path.home()/'Applications/OpenMicAI/runtime/decisions/venv' if sys.platform=='win32' else environment/'venv'
        python = runtime/('Scripts/python.exe' if sys.platform=='win32' else 'bin/python')
        if not python.exists():
            venv.EnvBuilder(with_pip=True).create(runtime)
        print('Installing isolated CPU decision runtime (keeps GPU memory available for writing and images)', flush=True)
        subprocess.run([str(python),'-m','pip','install','--quiet','--disable-pip-version-check',
                        'torch==2.11.0','torchvision==0.26.0','--index-url','https://download.pytorch.org/whl/cpu'],check=True)
        subprocess.run([str(python),'-m','pip','install','--quiet','--disable-pip-version-check',
                        'transformers==5.15.1','accelerate==1.13.0','Pillow>=12.1.1','soundfile>=0.13',
                        'fastapi>=0.115,<1','pydantic>=2.10,<3','numpy>=2'],check=True)
        subprocess.run([str(python),'-c','import torch,torchvision,transformers,soundfile,fastapi; assert transformers.__version__ == "5.15.1"'],check=True)
        ready.write_text(json.dumps({'python':str(python),'model':str(checkpoint),
                                    'revision':backend['revision'],'adapter_version':manifest['version']}),encoding='utf-8')
        print('Decision backend installed. Loading is now available.',flush=True)


if __name__ == '__main__':
    main()
