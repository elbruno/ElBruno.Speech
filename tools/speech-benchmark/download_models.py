"""Explicit, bounded, SHA-256-verified downloads for the comparison (about 5.7 GB)."""

import argparse
import hashlib
from pathlib import Path
import urllib.request

PARAKEET_REVISION = "8f23f0c03c8761650bdb5b40aaf3e40d2c15f1ce"
WHISPER_REVISION = "5359861c739e955e79d9a303bcbc70fb988958b1"
ASSETS = [
    ("parakeet-fp32", "istupakov/parakeet-tdt-0.6b-v3-onnx", PARAKEET_REVISION,
     "encoder-model.onnx", 41770866, "98a74b21b4cc0017c1e7030319a4a96f4a9506e50f0708f3a516d02a77c96bb1"),
    ("parakeet-fp32", "istupakov/parakeet-tdt-0.6b-v3-onnx", PARAKEET_REVISION,
     "encoder-model.onnx.data", 2435420160, "9a22d372c51455c34f13405da2520baefb7125bd16981397561423ed32d24f36"),
    ("parakeet-fp32", "istupakov/parakeet-tdt-0.6b-v3-onnx", PARAKEET_REVISION,
     "decoder_joint-model.onnx", 72520893, "e978ddf6688527182c10fde2eb4b83068421648985ef23f7a86be732be8706c1"),
    ("parakeet-fp32", "istupakov/parakeet-tdt-0.6b-v3-onnx", PARAKEET_REVISION,
     "nemo128.onnx", 139764, "a9fde1486ebfcc08f328d75ad4610c67835fea58c73ba57e3209a6f6cf019e9f"),
    ("parakeet-fp32", "istupakov/parakeet-tdt-0.6b-v3-onnx", PARAKEET_REVISION,
     "vocab.txt", 93939, "d58544679ea4bc6ac563d1f545eb7d474bd6cfa467f0a6e2c1dc1c7d37e3c35d"),
    ("whisper", "ggerganov/whisper.cpp", WHISPER_REVISION,
     "ggml-large-v3.bin", 3095033483, "64d182b440b98d5203c4f9bd541544d84c605196c4f7b845dfa11fb23594d1e2"),
]


def verify(path, size, digest):
    with path.open("rb") as stream:
        if path.stat().st_size != size or hashlib.file_digest(stream, "sha256").hexdigest() != digest:
            raise ValueError(f"Size/SHA-256 mismatch: {path}")


def download(root):
    for group, repo, revision, name, size, digest in ASSETS:
        path = root / group / name
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists():
            verify(path, size, digest)
            print(f"Verified cached {name}", flush=True)
            continue
        temporary = path.with_suffix(path.suffix + ".download")
        if temporary.exists():
            raise ValueError(f"Remove interrupted download explicitly before retrying: {temporary}")
        print(f"Downloading {name}: {size} bytes", flush=True)
        try:
            with urllib.request.urlopen(
                    f"https://huggingface.co/{repo}/resolve/{revision}/{name}", timeout=120) as response:
                with temporary.open("xb") as output:
                    total = 0
                    while block := response.read(1024 * 1024):
                        total += len(block)
                        if total > size:
                            raise ValueError("Download exceeds pinned size")
                        output.write(block)
            verify(temporary, size, digest)
            temporary.rename(path)
        finally:
            temporary.unlink(missing_ok=True)
        print(f"Verified {name}", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    download(parser.parse_args().directory)
