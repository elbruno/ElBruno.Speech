"""Prepare, verify, and package the small CC BY 4.0 FLEURS fixture bundle (Python 3.11+)."""

import argparse
import csv
import hashlib
import io
import json
import math
from pathlib import Path, PurePosixPath
import struct
import tarfile
import urllib.request
import wave
import zipfile

REVISION = "70bb2e84b976b7e960aa89f1c648e09c59f894dd"
BASE = f"https://huggingface.co/datasets/google/fleurs/resolve/{REVISION}"
LIMIT = 20 * 1024 * 1024
ROOT = Path(__file__).resolve().parents[2]
DEFAULT_BUNDLE = ROOT / "tests" / "fixtures" / "speech" / "v1.0.0"
LICENSE_URL = "https://creativecommons.org/licenses/by/4.0/legalcode.txt"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def fetch(url, limit=2 * 1024 * 1024):
    request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(request, timeout=120) as response:
        data = response.read(limit + 1)
    if len(data) > limit:
        raise ValueError(f"Download exceeds {limit} bytes: {url}")
    return data


class LimitedReader:
    def __init__(self, stream):
        self.stream = stream
        self.count = 0

    def read(self, size):
        if self.count + size > 32 * 1024 * 1024:
            raise ValueError("Archive prefix exceeds 32 MiB; do not download the full archive.")
        data = self.stream.read(size)
        self.count += len(data)
        return data


def pcm16(source):
    if source[:4] != b"RIFF" or source[8:12] != b"WAVE":
        raise ValueError("Not a RIFF/WAVE source")
    position = 12
    fmt = samples = None
    while position + 8 <= len(source):
        name, size = struct.unpack_from("<4sI", source, position)
        chunk = source[position + 8:position + 8 + size]
        if len(chunk) != size:
            raise ValueError("Truncated source chunk")
        if name == b"fmt ":
            fmt = struct.unpack_from("<HHIIHH", chunk)
        elif name == b"data":
            samples = chunk
        position += 8 + size + (size % 2)
    if fmt is None or samples is None:
        raise ValueError("Source has no format or data")
    kind, channels, rate, _, alignment, bits = fmt
    if channels != 1 or rate != 16000:
        raise ValueError("Expected mono 16 kHz source; resampling is not implicit")
    if kind == 1 and bits == 16 and alignment == 2:
        converted = samples
        transform = "Rewrapped complete PCM16 samples in canonical 44-byte WAV; no trimming/resampling."
    elif kind == 3 and bits == 32 and alignment == 4:
        values = [value for (value,) in struct.iter_unpack("<f", samples)]
        if not all(math.isfinite(value) for value in values):
            raise ValueError("Non-finite source audio")
        converted = b"".join(struct.pack("<h", max(-32768, min(32767, round(value * 32768))))
                             for value in values)
        transform = ("Complete IEEE float32 WAV converted to PCM16: round-to-even(sample * 32768), "
                     "clamp to [-32768,32767]; canonical 44-byte WAV; no trimming/resampling.")
    else:
        raise ValueError(f"Unsupported source encoding: {fmt}")
    output = io.BytesIO()
    with wave.open(output, "wb") as wav:
        wav.setparams((1, 2, 16000, 0, "NONE", "not compressed"))
        wav.writeframes(converted)
    return output.getvalue(), len(converted) // 2, transform


def prepare(bundle):
    if (bundle / "manifest.json").exists():
        raise ValueError("Refusing to replace an existing version. Reproduce into a new directory.")
    if not (bundle / "ATTRIBUTION.md").is_file() or not (bundle / "README.md").is_file():
        raise ValueError("Copy the version's README.md and ATTRIBUTION.md into the output first.")
    bundle.mkdir(parents=True, exist_ok=True)
    license_data = fetch(LICENSE_URL)
    if b"Attribution 4.0 International" not in license_data:
        raise ValueError("Unexpected license response")
    (bundle / "LICENSE.txt").write_bytes(license_data)
    entries, sources = [], []
    for language in ("en_us", "es_419"):
        tsv_path = f"data/{language}/test.tsv"
        tsv = fetch(f"{BASE}/{tsv_path}")
        rows = list(csv.reader(io.StringIO(tsv.decode("utf-8")), delimiter="\t"))
        metadata = {row[1]: row for row in rows}
        archive_path = f"data/{language}/audio/test.tar.gz"
        seen = set()
        # Only a bounded prefix is streamed. The archive's full hundreds of MiB are not downloaded.
        with urllib.request.urlopen(f"{BASE}/{archive_path}", timeout=120) as response:
            reader = LimitedReader(response)
            with tarfile.open(fileobj=reader, mode="r|gz") as archive:
                for member in archive:
                    filename = PurePosixPath(member.name).name
                    if not member.isfile() or filename not in metadata:
                        continue
                    row = metadata[filename]
                    if row[0] in seen or not 32000 <= int(row[5]) <= 240000:
                        continue
                    if member.size > 2 * 1024 * 1024:
                        raise ValueError("Unexpectedly large selected source WAV")
                    with archive.extractfile(member) as stream:
                        original = stream.read()
                    audio, count, transform = pcm16(original)
                    if count != int(row[5]):
                        raise ValueError("Source sample count differs from TSV")
                    relative = f"{language}/{filename}"
                    (bundle / language).mkdir(exist_ok=True)
                    (bundle / relative).write_bytes(audio)
                    entries.append({
                        "id": f"fleurs-{language}-test-{filename[:-4]}",
                        "language": language, "path": relative,
                        "sha256": sha(audio), "sampleRate": 16000, "channels": 1,
                        "sampleFormat": "pcm_s16le", "sampleCount": count,
                        "durationSeconds": count / 16000,
                        "rawTranscript": row[2], "transcript": row[3],
                        "sourceId": row[0], "sourceFilename": filename,
                        "sourceSha256": sha(original),
                        "sourceUrl": f"{BASE}/{archive_path}",
                        "sourceMember": member.name, "transformation": transform,
                    })
                    seen.add(row[0])
                    if len(seen) == 10:
                        break
        if len(seen) != 10:
            raise ValueError(f"Could not select ten complete eligible clips for {language}")
        sources.append({"configuration": language, "split": "test", "metadataPath": tsv_path,
                        "metadataSha256": sha(tsv), "archivePath": archive_path})
        print(f"{language}: ten unique sentences; streamed {reader.count} compressed bytes")
    manifest = {
        "schemaVersion": 1, "bundleVersion": "1.0.0", "dataset": "google/fleurs",
        "revision": REVISION, "license": "CC-BY-4.0",
        "licenseUrl": "https://creativecommons.org/licenses/by/4.0/",
        "reviewedWordTimestamps": False,
        "selection": ("First ten distinct sentence IDs in pinned test archive order per language, "
                      "with TSV sample count 32000..240000 inclusive (2..15 seconds); "
                      "complete clips, no model-based selection."),
        "sources": sources, "fixtures": entries,
        "notices": {name: sha((bundle / name).read_bytes())
                    for name in ("README.md", "ATTRIBUTION.md", "LICENSE.txt")},
    }
    (bundle / "manifest.json").write_bytes(
        (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    verify(bundle)


def safe_path(bundle, relative):
    path = PurePosixPath(relative)
    if (not relative or "\\" in relative or ":" in relative or path.is_absolute()
            or any(part in ("", ".", "..") for part in relative.split("/"))):
        raise ValueError(f"Unsafe relative path: {relative}")
    target = bundle.joinpath(*path.parts)
    if not target.resolve().is_relative_to(bundle.resolve()) or target.is_symlink():
        raise ValueError(f"Path escapes bundle: {relative}")
    return target


def verify(bundle):
    manifest = json.loads((bundle / "manifest.json").read_text(encoding="utf-8"))
    if (manifest["schemaVersion"] != 1 or manifest["bundleVersion"] != "1.0.0"
            or manifest["dataset"] != "google/fleurs" or manifest["revision"] != REVISION
            or manifest["license"] != "CC-BY-4.0" or manifest["reviewedWordTimestamps"] is not False):
        raise ValueError("Unexpected bundle contract")
    fixtures = manifest["fixtures"]
    if len(fixtures) != 20 or len({entry["id"] for entry in fixtures}) != 20:
        raise ValueError("Expected twenty unique fixtures")
    if (manifest["licenseUrl"] != "https://creativecommons.org/licenses/by/4.0/"
            or not manifest["selection"].strip()
            or len(manifest["sources"]) != 2
            or {source["configuration"] for source in manifest["sources"]} != {"en_us", "es_419"}):
        raise ValueError("Invalid source/license provenance")
    for source in manifest["sources"]:
        language = source["configuration"]
        if (source["split"] != "test" or source["metadataPath"] != f"data/{language}/test.tsv"
                or source["archivePath"] != f"data/{language}/audio/test.tar.gz"
                or len(source["metadataSha256"]) != 64
                or any(c not in "0123456789abcdef" for c in source["metadataSha256"])):
            raise ValueError("Invalid pinned source metadata")
    for language in ("en_us", "es_419"):
        group = [entry for entry in fixtures if entry["language"] == language]
        if len(group) != 10 or len({entry["sourceId"] for entry in group}) != 10:
            raise ValueError(f"Expected ten distinct sentences for {language}")
    expected = {"manifest.json", "README.md", "ATTRIBUTION.md", "LICENSE.txt"}
    if set(manifest["notices"]) != expected - {"manifest.json"}:
        raise ValueError("Missing bundle notices")
    for name, digest in manifest["notices"].items():
        if sha(safe_path(bundle, name).read_bytes()) != digest:
            raise ValueError(f"Notice hash mismatch: {name}")
    for entry in fixtures:
        filename = entry["sourceFilename"]
        if (entry["path"] != f"{entry['language']}/{filename}"
                or PurePosixPath(filename).name != filename or not filename.endswith(".wav")
                or entry["sourceMember"] != f"test/{filename}"
                or entry["sourceUrl"] != f"{BASE}/data/{entry['language']}/audio/test.tar.gz"
                or not entry["sourceId"].strip() or not entry["transformation"].strip()
                or len(entry["sourceSha256"]) != 64
                or any(c not in "0123456789abcdef" for c in entry["sourceSha256"])):
            raise ValueError("Invalid fixture source provenance")
        path = safe_path(bundle, entry["path"])
        if entry["path"] in expected:
            raise ValueError("Duplicate fixture path")
        expected.add(entry["path"])
        if sha(path.read_bytes()) != entry["sha256"]:
            raise ValueError(f"Audio hash mismatch: {path}")
        with wave.open(str(path), "rb") as wav:
            if (wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getnframes()) != (
                    1, 2, 16000, entry["sampleCount"]):
                raise ValueError(f"Audio format mismatch: {path}")
        if (entry["sampleRate"] != 16000 or entry["channels"] != 1
                or entry["sampleFormat"] != "pcm_s16le"
                or not 32000 <= entry["sampleCount"] <= 240000
                or entry["durationSeconds"] != entry["sampleCount"] / 16000
                or not entry["transcript"].strip() or not entry["rawTranscript"].strip()):
            raise ValueError(f"Invalid fixture metadata: {entry['id']}")
    actual = {path.relative_to(bundle).as_posix() for path in bundle.rglob("*") if path.is_file()}
    if actual != expected:
        raise ValueError(f"Unexpected/missing payload files: {actual ^ expected}")
    size = sum(safe_path(bundle, name).stat().st_size for name in actual)
    if size > LIMIT:
        raise ValueError(f"Bundle exceeds 20 MiB: {size}")
    print(f"Verified {len(fixtures)} fixtures, {size} bytes; manifest SHA-256 "
          f"{sha((bundle / 'manifest.json').read_bytes())}")
    return actual


def pack(bundle, output):
    files = verify(bundle)
    output.mkdir(parents=True, exist_ok=True)
    archive = output / "speech-fixtures-v1.0.0.zip"
    if archive.exists():
        raise ValueError("Refusing to overwrite an existing archive")
    # Stored ZIP avoids differences between zlib versions; these files are already small.
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_STORED) as zipped:
        for name in sorted(files):
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            zipped.writestr(info, safe_path(bundle, name).read_bytes())
    (output / (archive.name + ".sha256")).write_bytes(
        f"{sha(archive.read_bytes())}  {archive.name}\n".encode("ascii"))
    print(archive)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("prepare", "verify", "pack"))
    parser.add_argument("--bundle", type=Path, default=DEFAULT_BUNDLE)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.command == "prepare":
        prepare(args.bundle)
    elif args.command == "verify":
        verify(args.bundle)
    else:
        if args.output is None:
            parser.error("pack requires --output")
        pack(args.bundle, args.output)
