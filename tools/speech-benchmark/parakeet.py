"""Run one isolated, four-thread, warmed CPU Parakeet benchmark. Score with SpeechBenchmark."""

import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import platform
import time
import wave

import numpy as np
import onnxruntime as ort

from download_models import ASSETS, verify


def peak_memory():
    if os.name != "nt":
        import resource
        return resource.getrusage(resource.RUSAGE_SELF).ru_maxrss * (1 if platform.system() == "Darwin" else 1024)

    class Counters(ctypes.Structure):
        _fields_ = [("cb", ctypes.c_ulong), ("PageFaultCount", ctypes.c_ulong)] + [
            (name, ctypes.c_size_t) for name in (
                "PeakWorkingSetSize", "WorkingSetSize", "QuotaPeakPagedPoolUsage", "QuotaPagedPoolUsage",
                "QuotaPeakNonPagedPoolUsage", "QuotaNonPagedPoolUsage", "PagefileUsage", "PeakPagefileUsage")]
    counters = Counters()
    counters.cb = ctypes.sizeof(counters)
    ctypes.windll.kernel32.GetCurrentProcess.restype = ctypes.c_void_p
    if not ctypes.windll.psapi.GetProcessMemoryInfo(
            ctypes.c_void_p(ctypes.windll.kernel32.GetCurrentProcess()), ctypes.byref(counters), counters.cb):
        raise ctypes.WinError()
    return counters.PeakWorkingSetSize


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("precision", choices=("int8", "fp32"))
parser.add_argument("model", type=Path)
parser.add_argument("bundle", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
if ort.__version__ != "1.31.0":
    raise ValueError("Use the pinned tools/parakeet/requirements.txt runtime")
manifest = json.loads((args.bundle / "manifest.json").read_text(encoding="utf-8"))
if args.precision == "fp32":
    for group, _, _, name, size, digest in ASSETS:
        if group == "parakeet-fp32":
            verify(args.model / name, size, digest)
else:
    assets = {
        "nemo128.onnx": (139764, "a9fde1486ebfcc08f328d75ad4610c67835fea58c73ba57e3209a6f6cf019e9f"),
        "vocab.txt": (93939, "d58544679ea4bc6ac563d1f545eb7d474bd6cfa467f0a6e2c1dc1c7d37e3c35d"),
        "encoder-model.int8.onnx": (652183999, "6139d2fa7e1b086097b277c7149725edbab89cc7c7ae64b23c741be4055aff09"),
        "decoder_joint-model.int8.onnx": (18202004, "eea7483ee3d1a30375daedc8ed83e3960c91b098812127a0d99d1c8977667a70"),
    }
    for name, (size, digest) in assets.items():
        verify(args.model / name, size, digest)
options = ort.SessionOptions()
options.intra_op_num_threads = 4
options.inter_op_num_threads = 1
options.execution_mode = ort.ExecutionMode.ORT_SEQUENTIAL
suffix = ".int8" if args.precision == "int8" else ""
started = time.perf_counter()
sessions = {name: ort.InferenceSession(str(args.model / filename), options, providers=["CPUExecutionProvider"])
            for name, filename in {
                "preprocessor": "nemo128.onnx", "encoder": f"encoder-model{suffix}.onnx",
                "decoder": f"decoder_joint-model{suffix}.onnx"}.items()}
load_seconds = time.perf_counter() - started
vocab = [line.rsplit(" ", 1)[0] for line in (args.model / "vocab.txt").read_text(encoding="utf-8").splitlines()]


def recognize(samples):
    features, lengths = sessions["preprocessor"].run(
        None, {"waveforms": samples[None], "waveforms_lens": np.array([len(samples)], dtype=np.int64)})
    encoded, encoded_lengths = sessions["encoder"].run(None, {"audio_signal": features, "length": lengths})
    state1 = np.zeros((2, 1, 640), dtype=np.float32)
    state2 = state1.copy()
    blank = len(vocab) - 1
    previous, frame, emitted = blank, 0, 0
    tokens = []
    while frame < encoded_lengths[0]:
        outputs = sessions["decoder"].run(None, {
            "encoder_outputs": encoded[:, :, frame:frame + 1], "targets": np.array([[previous]], dtype=np.int32),
            "target_length": np.array([1], dtype=np.int32), "input_states_1": state1, "input_states_2": state2})
        logits = outputs[0].ravel()
        token = int(logits[:len(vocab)].argmax())
        duration = int(logits[len(vocab):].argmax())
        if token != blank:
            tokens.append(vocab[token])
            previous, state1, state2 = token, outputs[2], outputs[3]
            emitted += 1
        if duration > 0:
            frame += duration
            emitted = 0
        elif token == blank or emitted == 10:
            frame += 1
            emitted = 0
    return "".join(tokens).replace("\u2581", " ").strip()


def read(fixture):
    path = args.bundle / fixture["path"]
    if hashlib.sha256(path.read_bytes()).hexdigest() != fixture["sha256"]:
        raise ValueError("Fixture hash mismatch")
    with wave.open(str(path), "rb") as wav:
        if (wav.getnchannels(), wav.getframerate(), wav.getsampwidth()) != (1, 16000, 2):
            raise ValueError("Unexpected audio format")
        return np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(np.float32) / 32768


warmup = time.perf_counter()
recognize(read(manifest["fixtures"][0]))
warmup_seconds = time.perf_counter() - warmup
clips = []
for fixture in manifest["fixtures"]:
    samples = read(fixture)
    start = time.perf_counter()
    hypothesis = recognize(samples)
    elapsed = time.perf_counter() - start
    clips.append({"id": fixture["id"], "hypothesis": hypothesis, "inferenceSeconds": elapsed})
    print(f"{fixture['id']}: {elapsed:.2f}s", flush=True)
report = {
    "provenance": {
        "model": "parakeet-tdt-0.6b-v3", "precision": args.precision,
        "repository": "istupakov/parakeet-tdt-0.6b-v3-onnx",
        "revision": "8f23f0c03c8761650bdb5b40aaf3e40d2c15f1ce",
        "runtime": f"Python {platform.python_version()}, ONNX Runtime {ort.__version__}, NumPy {np.__version__}",
        "threads": 4, "provider": "CPUExecutionProvider", "language": "automatic, no reference hints",
        "loadSeconds": load_seconds, "warmupSeconds": warmup_seconds,
        "peakWorkingSetBytes": peak_memory(), "operatingSystem": platform.platform(),
        "modelBytes": sum((args.model / name).stat().st_size for name in (
            ["vocab.txt", "nemo128.onnx", f"encoder-model{suffix}.onnx", f"decoder_joint-model{suffix}.onnx"]
            + (["encoder-model.onnx.data"] if args.precision == "fp32" else []))),
    },
    "clips": clips,
}
args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
