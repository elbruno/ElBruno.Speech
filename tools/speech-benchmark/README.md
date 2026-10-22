# Fixed-reference CPU comparison

This opt-in tool compares pinned Parakeet v3 INT8, Parakeet v3 FP32, and Whisper
large-v3 on the **same twenty complete FLEURS clips** in
`tests\fixtures\speech\v1.0.0`. It does not edit references or approve regression
thresholds. All hypotheses are scored by the existing .NET
`TranscriptionScorer`, including its accent-preserving normalization.
The [recorded comparison and recommendation](../../docs/speech-benchmark.md)
includes the complete public-reference JSON reports.

## Reproduce on Windows

Use .NET 10 and Python 3.11+. Model downloads are explicit: comparison models
require approximately 5.65 GB, in addition to the existing verified Parakeet
INT8 cache. No model weights belong in Git or the fixture archive.

```powershell
python -m pip install -r tools\parakeet\requirements.txt
python tools\speech-benchmark\download_models.py C:\models\comparison
dotnet build tools\speech-benchmark\SpeechBenchmark -c Release
```

Prepare the INT8 cache with the existing [Parakeet instructions](../../docs/parakeet.md).
Then run each command **sequentially**, with no other inference, build, or
CPU-intensive work running. Each command is a separate process. Do not compare
timings from concurrent runs.

```powershell
python tools\speech-benchmark\parakeet.py int8 C:\models\parakeet-int8 tests\fixtures\speech\v1.0.0 C:\results\int8-raw.json
python tools\speech-benchmark\parakeet.py fp32 C:\models\comparison\parakeet-fp32 tests\fixtures\speech\v1.0.0 C:\results\fp32-raw.json
dotnet tools\speech-benchmark\SpeechBenchmark\bin\Release\net10.0\SpeechBenchmark.dll whisper C:\models\comparison\whisper\ggml-large-v3.bin tests\fixtures\speech\v1.0.0 C:\results\whisper.json
dotnet tools\speech-benchmark\SpeechBenchmark\bin\Release\net10.0\SpeechBenchmark.dll score C:\results\int8-raw.json tests\fixtures\speech\v1.0.0 C:\results\int8.json
dotnet tools\speech-benchmark\SpeechBenchmark\bin\Release\net10.0\SpeechBenchmark.dll score C:\results\fp32-raw.json tests\fixtures\speech\v1.0.0 C:\results\fp32.json
python -m unittest discover -s tools\speech-benchmark -p "test_*.py"
python tools\speech-benchmark\check_report.py C:\results\int8.json
python tools\speech-benchmark\check_report.py C:\results\fp32.json
python tools\speech-benchmark\check_report.py C:\results\whisper.json
```

Create `C:\results` first. Model pins, sizes, and SHA-256 values live in
`download_models.py`. The tool rejects corrupt existing weights and verifies new
weights before renaming them. The GGML Whisper model is unquantized,
predominantly F16, **not FP32**. The FP32 Parakeet encoder uses external data,
which must remain beside the ONNX file.

## What the measurements mean

All three configurations use four CPU inference threads, automatic language
selection without reference hints, and one full-clip warm-up excluded from
inference timing. Parakeet uses ONNX Runtime 1.31.0, identical preprocessing and
greedy TDT decoding for both precisions. Whisper uses Whisper.net 1.9.1 /
whisper.cpp CPU, greedy sampling, temperature zero without temperature
fallback, and no context carried between clips. This is a comparison of these
**specific implementations/settings**, not a runtime-controlled architecture
experiment. Exporter checkpoint lineage is not independently established.

Reports include all raw hypotheses, per-language word/character edit counts,
model-load and warm-up time, summed inference time, real-time factor (RTF),
model size, and peak process working set. Lower WER/CER and RTF are better.
RTF below one is faster than real time. Peak working set is process memory,
not just model weights; it includes verification/loading/warm-up. A single run
is not a latency percentile or a statistical confidence interval.

This tiny read-speech bundle cannot establish podcast, noise, code-switching,
speaker, or timestamp accuracy. No reviewed timestamp references are present.
Do not call a lower WER a universal improvement or change default providers
based on it. See [the fixture contract](../../docs/speech-fixtures.md) for
source attribution and CC BY 4.0 obligations. The selected Whisper GGML
repository declares MIT; Parakeet weights are CC BY 4.0. Models are downloaded
from their upstream repositories, not republished here.
