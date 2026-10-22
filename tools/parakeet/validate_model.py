"""Create local PCM fixtures and implementation-parity hypotheses, not accuracy references."""

import argparse
import json
import time
import wave
from pathlib import Path

import numpy as np
import onnxruntime as ort
from huggingface_hub import hf_hub_download

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--model-directory", type=Path, required=True)
parser.add_argument("--output-directory", type=Path, required=True)
args = parser.parse_args()
root = args.output_directory
root.mkdir(parents=True, exist_ok=True)
model = args.model_directory
if ort.__version__ != "1.31.0":
    raise RuntimeError("Use requirements.txt: parity requires ONNX Runtime 1.31.0.")
sessions = {
    name: ort.InferenceSession(str(model / filename), providers=["CPUExecutionProvider"])
    for name, filename in {
        "preprocessor": "nemo128.onnx",
        "encoder": "encoder-model.int8.onnx",
        "decoder": "decoder_joint-model.int8.onnx",
    }.items()
}
vocab = [line.rsplit(" ", 1)[0] for line in (model / "vocab.txt").read_text(encoding="utf-8").splitlines()]
reports = []
for language in ["es", "en", "mixed"]:
    if language == "mixed":
        parts = []
        for part in ["es", "en"]:
            with wave.open(str(root / f"{part}.wav")) as wav:
                parts.append(wav.readframes(wav.getnframes()))
        pcm = parts[0] + b"\0" * 16000 + parts[1]
        path = "concatenated es.wav + 0.5s silence + en.wav; synthetic code-switch smoke test"
    else:
        path = hf_hub_download(
            "csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3",
            f"test_wavs/{language}.wav", revision="1a468a35cbba69418f126de829e75261dea4a4e4",
        )
        with wave.open(path) as wav:
            if wav.getnchannels() != 1 or wav.getsampwidth() != 2:
                raise ValueError("Expected mono PCM16 source fixture.")
            rate = wav.getframerate()
            samples = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(np.float32) / 32768
        if rate != 16000:
            samples = np.interp(np.arange(int(np.ceil(len(samples) * 16000 / rate))) * rate / 16000, np.arange(len(samples)), samples).astype(np.float32)
        pcm = (samples * 32767).astype("<i2").tobytes()
    with wave.open(str(root / f"{language}.wav"), "wb") as wav:
        wav.setparams((1, 2, 16000, 0, "NONE", "not compressed"))
        wav.writeframes(pcm)
    samples = np.frombuffer(pcm, dtype="<i2").astype(np.float32) / 32768
    started = time.perf_counter()
    features, lengths = sessions["preprocessor"].run(None, {"waveforms": samples[None], "waveforms_lens": np.array([len(samples)], dtype=np.int64)})
    encoded, encoded_lengths = sessions["encoder"].run(None, {"audio_signal": features, "length": lengths})
    state1 = np.zeros((2, 1, 640), dtype=np.float32)
    state2 = state1.copy()
    blank = len(vocab) - 1
    previous = blank
    frame = emitted = 0
    tokens = []
    while frame < encoded_lengths[0]:
        logits, _, next1, next2 = sessions["decoder"].run(None, {
            "encoder_outputs": encoded[:, :, frame:frame + 1],
            "targets": np.array([[previous]], dtype=np.int32),
            "target_length": np.array([1], dtype=np.int32),
            "input_states_1": state1,
            "input_states_2": state2,
        })
        logits = logits.ravel()
        token = int(logits[:len(vocab)].argmax())
        duration = int(logits[len(vocab):].argmax())
        if token != blank:
            tokens.append({"text": vocab[token], "start_seconds": frame * 0.08})
            previous, state1, state2 = token, next1, next2
            emitted += 1
        if duration > 0:
            frame += duration
            emitted = 0
        elif token == blank or emitted == 10:
            frame += 1
            emitted = 0
    text = "".join(token["text"] for token in tokens).replace("\u2581", " ").strip()
    reports.append({
        "fixture": path, "language_expected_from_filename": language,
        "language_id_returned": None, "text": text, "tokens": tokens,
        "word_intervals": None, "segment_intervals": None,
        "audio_seconds": len(samples) / 16000,
        "inference_seconds": time.perf_counter() - started,
        "reference_reviewed": False,
    })
    (root / f"{language}.hypothesis.txt").write_text(text, encoding="utf-8")
    (root / f"{language}.hypothesis.json").write_text(json.dumps(tokens, ensure_ascii=False), encoding="utf-8")
    print(language, text)
(root / "parakeet-validation.json").write_text(json.dumps(reports, ensure_ascii=False, indent=2), encoding="utf-8")
