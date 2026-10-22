# Experimental local Parakeet TDT v3

`ElBruno.Speech.Stt.Parakeet` is an opt-in `ISpeechToTextClient` for local,
multilingual transcription. It does not replace Whisper, change pipeline defaults,
or require a cloud inference service. The provider was released in
[`1.6.0-preview.1`](https://github.com/elbruno/ElBruno.Speech/releases/tag/v1.6.0-preview.1).
See the [prerelease notes](releases/1.6.0-preview.1.md).

The subsequent [fixed-reference comparison](speech-benchmark.md) evaluates
INT8, FP32, and Whisper large-v3 without changing this provider's model or
pipeline defaults. The new Whisper provider remains source-only pending its
own review and release.

## Model and runtime

Use [elbruno/parakeet-tdt-0.6b-v3-onnx](https://huggingface.co/elbruno/parakeet-tdt-0.6b-v3-onnx/tree/2a5f66ac53dfc1c817e75609d9903d75b4ec5895),
commit `2a5f66ac53dfc1c817e75609d9903d75b4ec5895`.
The downloader pins this commit and verifies each file's size and SHA-256.
Inference and construction do not download files. Offline deployments can copy
all five verified assets to a local directory.

This is an attributed selection of Ilya Stupakov's existing multilingual v3 INT8
ONNX export, not new training, quantization, or a fresh conversion. Assets total
670,619,803 bytes (about 639.6 MiB). The bundle includes the 128-bin feature
preprocessor, encoder, decoder/joint, vocabulary, and configuration.

The original NVIDIA 600M-parameter model supports 25 languages including Spanish:
`bg`, `hr`, `cs`, `da`, `nl`, `en`, `et`, `fi`, `fr`, `de`, `el`, `hu`, `it`,
`lv`, `lt`, `mt`, `pl`, `pt`, `ro`, `sk`, `sl`, `es`, `sv`, `ru`, and `uk`.
The model automatically chooses its language, but this runtime does not expose
a detected-language identifier. Language hints, translation, and model overrides
are rejected rather than silently ignored.

The .NET runtime dependency is the repository's CPU ONNX Runtime 1.31.0 package.
There is no production Python/NeMo/CUDA dependency. Windows x64 CPU and .NET 8/10
have been exercised. Linux, macOS, ARM, GPU execution, trimming, and Native AOT
are not validated for this provider. Do not infer provider support from ONNX
Runtime's general platform support.

## Usage

```csharp
using ElBruno.Speech.Stt.Parakeet;
using Microsoft.Extensions.AI;

// Explicit provisioning step, skip when deploying a pre-populated local directory.
await ParakeetModel.DownloadAsync(cancellationToken: cancellationToken);

using var client = new ParakeetSpeechToTextClient(new ParakeetOptions
{
    InputFormat = ParakeetInputFormat.Wav
});
await using var audio = File.OpenRead("recording.wav");
var response = await client.GetTextAsync(audio, cancellationToken: cancellationToken);
Console.WriteLine(response.Text);
var transcript = (ParakeetTranscript)response.RawRepresentation!;
foreach (var token in transcript.Tokens)
    Console.WriteLine($"{token.Start.TotalSeconds:F2}s: {token.Text}");
```

Input must be **mono 16 kHz signed little-endian PCM16**. WAV mode validates the
container and format. The default `RawPcm16` mode matches the conversational
pipeline's raw input; it is not an auto-detect mode. Resample/downmix other audio
before invoking the provider. Empty input returns an empty transcript; malformed
input and unsupported options produce explicit exceptions.

For dependency injection, call `services.AddParakeetSpeechToText()` before
`services.AddSpeechPipeline()`. Existing application registration stays unchanged
unless explicitly replaced. The DI container owns the registered client's lifetime.
Calls on one client are serialized, and native inference observes cancellation.

The default input cap is 64 MiB. `ChunkSeconds` defaults to 20 and accepts 1-30.
Longer inputs are decoded as independent bounded chunks, preserving original
recording offsets. Chunk boundaries can cut words or alter context. This is not
overlapping/VAD-aware podcast segmentation. `GetStreamingTextAsync` returns final
response updates after offline inference, not incremental acoustic streaming.

## Timestamp contract

`RawRepresentation` contains a `ParakeetTranscript`. Its `Tokens` retain native
SentencePiece tokens and their encoder-frame starts at 80 ms resolution.
Centered preprocessing/encoder padding can place a native start at or just beyond
a chunk's audio end. Such starts are retained unchanged and explicitly marked
`IsInPadding`, not clipped to fabricated speech alignment.
There are **no native token ends, word intervals, or segment intervals** in this
integration. `HasWordIntervals` and `HasSegmentIntervals` are false;
`DetectedLanguage` is null. Response-level start/end fields are left unset.
No intervals are interpolated or synthesized from full-window spans.

The conversational pipeline consumes `response.Text` only. Subtitle consumers
must call the provider directly to retain its native metadata. Token starts are
not independently verified speech alignment and must not be presented as such.

## Reproduce smoke validation

Create an isolated Python environment and install `tools\parakeet\requirements.txt`.
Python is needed only for model preparation and reference-output generation:

```powershell
python -m venv .venv-parakeet
.\.venv-parakeet\Scripts\python -m pip install -r tools\parakeet\requirements.txt
.\.venv-parakeet\Scripts\python tools\parakeet\prepare_model.py --directory C:\models\parakeet
.\.venv-parakeet\Scripts\python tools\parakeet\validate_model.py --model-directory C:\models\parakeet --output-directory C:\audio\parakeet-smoke
$env:PARAKEET_MODEL_DIRECTORY = 'C:\models\parakeet'
$env:PARAKEET_FIXTURE_DIRECTORY = 'C:\audio\parakeet-smoke'
dotnet test tests\ElBruno.Speech.Stt.Parakeet.Tests --filter 'Category=Integration&Category!=ModelDownload'
```

The generator uses pinned public Spanish and English WAV fixtures from
`csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3` revision
`1a468a35cbba69418f126de829e75261dea4a4e4`. It also creates a clearly labeled
concatenated Spanish/silence/English code-switch fixture. The upstream fixtures'
recording provenance is not independently established here. Audio stays local
and is not included in Git or the published model.
The `.hypothesis` files test Python/.NET implementation parity, **not accuracy**.
Tests compare all token texts and starts as well as full transcript text.
Run `--filter 'Category=ModelDownload' -f net10.0` separately to exercise the
published-bundle downloader and Spanish inference.

The source sample writes native token metadata and runtime measurements to JSON:

```powershell
dotnet run --project src\samples\ParakeetTranscribe -f net10.0 -- recording.wav C:\models\parakeet recording.json
```

On Windows x64, AMD EPYC 74F3 CPU, .NET 8/10, CPU ONNX Runtime 1.31.0:

| Fixture | Duration | Inference time | Model load including checksums |
|---|---:|---:|---:|
| Spanish | 5.33 s | 0.50-0.56 s | 4.28-4.37 s |
| English | 3.85 s | 0.41 s | 3.97-4.04 s |
| Concatenated Spanish/English | 9.67 s | 0.55-0.58 s | 3.90-3.98 s |

These single-pass smoke measurements ran both target-framework test processes
concurrently, not a controlled benchmark. Peak test-process working set reached
about 850-853 MiB, including test-host overhead. Provision additional headroom;
these short-input measurements are not a maximum-memory bound. The model card's
2 GB minimum load guidance is not a guarantee for arbitrary recordings.

INT8 output changed when the Python reference used ONNX Runtime 1.28.0.
Matching 1.31.0 produced exact text and token-start parity. Re-evaluate both
text and timing before upgrading the runtime or changing execution providers.

### Independent text references

The [reusable FLEURS fixtures](speech-fixtures.md#scoring-policy-and-baseline)
provide a separate small independent-reference regression test. Initial WER was
5.66% English (12 word edits over 212 reference words) and 3.37% Spanish
(seven over 208). **WER is an error rate: lower is better and 0% is perfect.**
These are not recognition-success percentages or podcast accuracy measurements.
See the fixture documentation for scoring limitations and
[candidate quality improvements](speech-fixtures.md#improving-quality-without-confusing-the-measurements).

### User-provided podcast recordings

Both complete Spanish recordings supplied by Bruno Capuano were processed locally:
`ntn459.mp3` is the edited episode with music intro/background music;
`Recording.m4a` is the voice-only recording. They belong to the
[No Tiene Nombre podcast](https://www.notienenombre.com).
The original recordings, normalized audio, and generated transcripts were not
committed to Git or uploaded to Hugging Face. Only aggregate observations are
included here.

FFmpeg 7.1 (isolated `imageio-ffmpeg` 0.6.0 binary) decoded each complete input
to mono 16 kHz PCM16 WAV. The source sample then processed all samples, without
trimming, on Windows x64 CPU, .NET 10, ONNX Runtime 1.31.0, default 20-second
chunks, and the pinned published model:

| Input | Duration | Inference | Load/checksums | Peak process working set | Native tokens |
|---|---:|---:|---:|---:|---:|
| Edited podcast | 24:07.29 | 44.22 s | 3.48 s | 1,341 MiB | 8,247 |
| Voice-only | 23:28.92 | 43.59 s | 3.45 s | 1,334 MiB | 8,297 |

Real-time factors were 0.0306 and 0.0309 respectively (inference time divided by
audio duration, excluding audio conversion/model load). These are single-run
measurements, not a controlled benchmark or accuracy comparison.
All token starts were monotonic on the original recording timeline.
The voice-only run exposed four native starts on chunk padding; these are
retained and explicitly flagged, not clipped, dropped, or turned into intervals.
The edited run had no flagged padding starts.

Reference input SHA-256:

- `ntn459.mp3`: `9f14a9764e45bcfc8363d59c95230412e559cc90c58aed5e3caecd9014c58db7`
- `Recording.m4a`: `8799ba4d6c18f918ba59c97380563ba34dba0aaca59e2c8258d52858fef2cf04`

**Still required for issue #101:** Whisper baseline artifacts and independently
human-reviewed text and word/segment alignment references for these recordings.
None were supplied or found alongside the audio. No podcast WER/CER,
boundary-error distributions, comparative accuracy, or production
subtitle-alignment claim is made. The two model hypotheses must not be used
as each other's ground truth.

## Alternative runtime and licensing

[NVIDIA NeMo-Speech.cpp](https://github.com/NVIDIA/NeMo-Speech.cpp) supports v3 GGUF,
structured word-timestamp output, and CPU/accelerator backends. It was compared
at the documented integration/dependency level, not benchmarked here. It needs a
separate native distribution/toolchain or executable; its CLI rejects streaming
for offline-only Parakeet TDT. Prefer investigating it if native word intervals
are essential. This implementation chooses ONNX for in-process .NET integration,
existing repository dependency reuse, and verified local multilingual inference,
while explicitly declaring its timing limitations.

NVIDIA's weights and the selected converted weights are **CC BY 4.0**, separate
from this library's MIT license. Preserve attribution, license links, and
modification notices when redistributing. The bundle identifies NVIDIA as model
author and Ilya Stupakov as exporter. Its original NVIDIA checkpoint commit is
not documented by the exporter. The observed current upstream NVIDIA revision
is `541d1f99c6b0c3cd0b11a95167540bb8edefd82b`, not a proven export lineage.
See `tools\parakeet\THIRD_PARTY_NOTICES.txt`, included in the provider package.

`prepare_model.py --publish` explicitly uploads only the verified assets, card,
manifest, and notices to the authorized `elbruno` repository. No fresh export is
needed for this integration. For future graph changes, start from the pinned
NeMo checkpoint using the export instructions in the source ONNX model card,
validate FP32 against NeMo, then independently validate any quantization.
Do not claim that INT8 preserves FP32 accuracy based on the smoke tests here.

No NuGet publication was performed. A future release must review the new package
alongside every existing package, including BlazorComponents, and use the normal
`publish.yml` review gate.
