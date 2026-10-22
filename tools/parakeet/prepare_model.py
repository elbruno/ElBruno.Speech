"""Prepare and optionally publish the byte-identical, attributed INT8 model bundle."""

import argparse
import hashlib
import json
from pathlib import Path

from huggingface_hub import HfApi, snapshot_download

SOURCE = "istupakov/parakeet-tdt-0.6b-v3-onnx"
REVISION = "8f23f0c03c8761650bdb5b40aaf3e40d2c15f1ce"
DESTINATION = "elbruno/parakeet-tdt-0.6b-v3-onnx"
ASSETS = {
    "config.json": (97, "666903c76b9798caf2c210afd4f6cd60b08a8dbf9800ec8d7a3bc0d2148ac466"),
    "vocab.txt": (93939, "d58544679ea4bc6ac563d1f545eb7d474bd6cfa467f0a6e2c1dc1c7d37e3c35d"),
    "nemo128.onnx": (139764, "a9fde1486ebfcc08f328d75ad4610c67835fea58c73ba57e3209a6f6cf019e9f"),
    "encoder-model.int8.onnx": (652183999, "6139d2fa7e1b086097b277c7149725edbab89cc7c7ae64b23c741be4055aff09"),
    "decoder_joint-model.int8.onnx": (18202004, "eea7483ee3d1a30375daedc8ed83e3960c91b098812127a0d99d1c8977667a70"),
}
CARD = """---
license: cc-by-4.0
language: [en, es, fr, de, bg, hr, cs, da, nl, et, fi, el, hu, it, lv, lt, mt, pl, pt, ro, sk, sl, sv, ru, uk]
base_model: nvidia/parakeet-tdt-0.6b-v3
pipeline_tag: automatic-speech-recognition
tags: [onnx, speech, parakeet, tdt, int8]
---

# Parakeet TDT v3 INT8 ONNX bundle for ElBruno.Speech

An attributed, byte-identical selection of the existing multilingual v3 INT8
export, not a newly trained, fine-tuned, or newly quantized model.

## Attribution and provenance

- Original model: NVIDIA, [Parakeet TDT 0.6B v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3),
  observed upstream revision `541d1f99c6b0c3cd0b11a95167540bb8edefd82b`.
- ONNX conversion and quantization: Ilya Stupakov,
  [source bundle](https://huggingface.co/istupakov/parakeet-tdt-0.6b-v3-onnx/tree/8f23f0c03c8761650bdb5b40aaf3e40d2c15f1ce).
  The exporter does not identify its original NVIDIA source commit, so exact
  training-checkpoint lineage beyond the declared base model is not asserted.
- Preprocessing/export reference: [onnx-asr](https://github.com/istupakov/onnx-asr),
  copyright Ilya Stupakov, MIT. See THIRD_PARTY_NOTICES.txt for the full notice.
- Repackaging: Bruno Capuano. Changes: selected INT8 files and added this card,
  manifest, and attribution. Graphs, vocabulary, and configuration are unchanged.
- Weights are [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
  Retain attribution, link the license, and indicate modifications. There is no
  NVIDIA or original export author's endorsement. The .NET library's MIT
  license does not relicense the model.

## Runtime contract

Supply mono 16 kHz audio. The .NET provider accepts signed little-endian PCM16,
raw or WAV wrapped. The preprocessor graph itself takes float32 waveforms.
Use all five assets together, about 670.6 MB decimal, with the SHA-256 manifest.
Encoder and decoder graphs alone are not a complete ASR runtime.

The integration uses ONNX Runtime CPU 1.31.0 and a greedy TDT decoder.
Token starts come from native encoder frame indices at 80 ms resolution.
Native word/segment intervals, token ends, language-ID fields, translation,
and incremental acoustic streaming are not exposed by this integration.
Automatic multilingual transcription does not imply an exposed language label.
Do not manufacture fine-grained intervals from coarse chunks.

The library preserves Whisper defaults. It is an opt-in experimental provider,
not a claim of better subtitle alignment or podcast accuracy. Long recordings
are processed in bounded independent chunks; words at chunk edges may be cut.
Python is needed only for bundle preparation/validation, not .NET inference.

## Validation and limits

Windows x64 CPU smoke inference was exercised with public Spanish and English
WAV fixtures and an explicitly concatenated mixed-language fixture on .NET 8/10.
Text and token-start output matched a separate Python
implementation with the same PCM samples and ONNX Runtime 1.31.0. These are
implementation-parity checks, not human-reviewed accuracy references.
Changing ONNX Runtime versions can change INT8 token predictions and timings.
Linux, macOS, ARM, and GPU execution have not been validated for this provider.
No independent podcast WER or timestamp-accuracy measurements are claimed.
No recordings or evaluation transcripts are included in this repository.

See [ElBruno.Speech](https://github.com/elbruno/ElBruno.Speech) for provider code,
reproducible preparation/validation scripts, limitations, and measured smoke
performance. Pin this repository's immutable commit for production downloads.
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--publish", action="store_true", help="Upload only the listed model assets and documentation.")
    args = parser.parse_args()
    snapshot_download(SOURCE, revision=REVISION, allow_patterns=list(ASSETS), local_dir=args.directory)
    manifest = {}
    for name, (size, expected_hash) in ASSETS.items():
        path = args.directory / name
        with path.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        if path.stat().st_size != size or digest != expected_hash:
            raise ValueError(f"Model asset failed integrity validation: {name}")
        manifest[name] = {"size": size, "sha256": digest}
    (args.directory / "manifest.json").write_text(json.dumps({
        "source": SOURCE, "source_revision": REVISION, "files": manifest,
    }, indent=2) + "\n", encoding="utf-8")
    (args.directory / "README.md").write_text(CARD, encoding="utf-8")
    (args.directory / "THIRD_PARTY_NOTICES.txt").write_text(
        Path(__file__).with_name("THIRD_PARTY_NOTICES.txt").read_text(encoding="utf-8"),
        encoding="utf-8",
    )
    if args.publish:
        api = HfApi()
        if api.whoami()["name"] != "elbruno":
            raise RuntimeError("Publication requires the authorized elbruno account.")
        api.create_repo(DESTINATION, repo_type="model", exist_ok=True)
        result = api.upload_folder(
            repo_id=DESTINATION, folder_path=args.directory,
            allow_patterns=[*ASSETS, "README.md", "manifest.json", "THIRD_PARTY_NOTICES.txt"],
            commit_message="Add verified multilingual Parakeet v3 INT8 bundle with attribution",
        )
        print(f"Published {DESTINATION}@{result.oid}")
    else:
        print(f"Verified bundle prepared in {args.directory}; nothing uploaded.")


if __name__ == "__main__":
    main()
