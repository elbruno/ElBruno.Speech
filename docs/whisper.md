# Opt-in Whisper large-v3 CPU provider

`ElBruno.Speech.Stt.Whisper` implements MEAI `ISpeechToTextClient` with
Whisper.net 1.9.1 / whisper.cpp. It is an alternative local architecture to
Parakeet, **not a replacement for the existing ElBruno.Whisper package or its
defaults**. This new provider is available from source pending review/release.

The single supported model is the pinned multilingual `ggml-large-v3.bin` from
[`ggerganov/whisper.cpp`](https://huggingface.co/ggerganov/whisper.cpp/tree/5359861c739e955e79d9a303bcbc70fb988958b1).
It is unquantized GGML with predominantly F16 weights, not FP32 and not ONNX.
No new model conversion or Hugging Face upload is necessary.

| Property | Value |
|---|---|
| Revision | `5359861c739e955e79d9a303bcbc70fb988958b1` |
| Bytes | 3,095,033,483 |
| SHA-256 | `64d182b440b98d5203c4f9bd541544d84c605196c4f7b845dfa11fb23594d1e2` |
| Runtime | Whisper.net / Whisper.net.Runtime 1.9.1, CPU only |
| Settings | 4 threads by default, greedy, temperature 0, no temperature fallback or context between requests |
| Language | Automatic, no reference-language hints or translation exposed |

## Native requirements

Use .NET 8 or .NET 10 and a platform supported by Whisper.net.Runtime 1.9.1.
On Windows, upstream requires Windows 11 / Server 2022 or later, the Visual
C++ 2022 redistributable, and (for the standard x64 CPU runtime) AVX, AVX2, FMA,
and F16C support. Linux requires glibc 2.31+ and libstdc++6; macOS support
depends on the upstream native runtime. See the
[versioned upstream instructions](https://github.com/sandrohanea/whisper.net/blob/1.9.1/readme.md).

Unsupported native environments fail explicitly. This package does not install
system redistributables, switch global native-runtime settings, install CUDA,
or silently fall back to another model. Large-v3 needs several GiB of process
memory, beyond its 3.1 GB weight file. Do not assume low latency on CPU.

## Explicit preparation and use

Download separately, once. Constructors, DI registration, and transcription
never access the network.

```csharp
using ElBruno.Speech.Audio;
using ElBruno.Speech.Stt.Whisper;

await WhisperModel.DownloadAsync(@"C:\models\whisper\ggml-large-v3.bin");

using var client = new WhisperSpeechToTextClient(new()
{
    ModelPath = @"C:\models\whisper\ggml-large-v3.bin",
    InputFormat = Pcm16InputFormat.Wav
});
await using var audio = File.OpenRead(@"C:\audio\recording.wav");
var response = await client.GetTextAsync(audio);
var transcript = (WhisperTranscript)response.RawRepresentation!;
Console.WriteLine(transcript.Text);
```

Preparation validates size and SHA-256, including cached files. Construction
verifies the entire model again before native loading. A corrupt file fails;
delete that specific corrupt file explicitly before downloading again. The
cache defaults to `.cache\elbruno-speech\whisper-large-v3` under the user profile.
No weights are included in the NuGet package.

For a pipeline, select the provider explicitly:

```csharp
services.AddWhisperLargeV3SpeechToText(new() { ModelPath = modelPath });
services.AddSpeechPipeline(builder => builder
    .UseSpeechToText(sp => sp.GetRequiredService<ISpeechToTextClient>()));
```

Do not register multiple unkeyed STT providers and depend on registration order.
The pipeline still needs its other VAD/chat/TTS registrations, as in the root
quick start. Options are snapshotted at registration/construction.

Input defaults to **raw signed little-endian PCM16, mono, 16 kHz**, matching the
pipeline. Select `Pcm16InputFormat.Wav` explicitly for a complete RIFF/WAVE file.
Strict shared input validation rejects wrong rates, stereo, non-PCM16,
truncation, duplicate data chunks, and incomplete samples. No implicit
resampling or downmixing occurs. `MaxInputBytes` defaults to 64 MiB including
container bytes. Caller streams remain open.

## Honest capabilities and lifecycle

The provider returns final-only streaming updates, not live partial hypotheses.
`WhisperTranscript.Segments` preserves native segment text/start/end/language.
These timestamps are model predictions, **not independently reviewed timing or
forced alignment**. It does not expose word intervals. Segment ends past the
actual input remain unchanged with `ExtendsPastInput=true`; no clipping or
invented boundaries occurs. Transcript-level language is null if segments
disagree or no segments exist.

Requests on a client are serialized. Cancellation while reading or queued is
immediate; native cancellation is cooperative at encoder boundaries and can
wait for the current decoding window. The client joins native execution before
returning cancellation, admitting another request, or disposing its model.
It does not abandon a still-running native task. Dispose waits for active work,
so do not call it from a callback that is itself waiting for transcription.

Unsupported model overrides, sample rates, language hints, and translation
requests throw rather than being silently ignored. Native failures propagate
to the caller. Empty audio returns empty text without native inference; this
does not make the model immune to hallucination on silence or noise.

## Sample and independent-reference evaluation

```powershell
dotnet run --project src\samples\WhisperTranscribe -- download C:\models\whisper\ggml-large-v3.bin
dotnet run --project src\samples\WhisperTranscribe -- recording.wav C:\models\whisper\ggml-large-v3.bin output.json
dotnet test tests\ElBruno.Speech.Stt.Whisper.Tests -c Release --filter "Category!=Integration&Category!=ModelDownload"
```

Ordinary CI runs model-free contract, validation, cancellation, and disposal
tests on both frameworks. Real-model evaluation is explicitly opt-in:

```powershell
$env:WHISPER_MODEL_PATH = 'C:\models\whisper\ggml-large-v3.bin'
$env:SPEECH_FIXTURES_REPORT_DIRECTORY = 'C:\results\whisper'
dotnet test tests\ElBruno.Speech.Stt.Whisper.Tests -c Release -f net10.0 --filter "Category=WhisperFixtureEvaluation"
dotnet test tests\ElBruno.Speech.Stt.Whisper.Tests -c Release -f net8.0 --filter "Category=WhisperNativeSmoke"
```

The first command explicitly downloads/verifies the pinned model, transcribes
all twenty versioned FLEURS fixtures with the shared scorer, and saves complete
or partial JSON evidence. The smoke test requires the prepared cache and checks
native cancellation/reuse plus Spanish detection. Accuracy thresholds remain
unapproved. The read-speech bundle provides no reviewed timestamp references
and does not close the podcast evaluation requirement in issue #101.

Use the [comparison tool](../tools/speech-benchmark/README.md) for warmed,
isolated performance runs. Provider integration tests include cold inference
and should not be mixed into those timings.
The [recorded comparison](speech-benchmark.md) explains the measured quality
and CPU cost tradeoff. The manual **Whisper large-v3 fixture evaluation**
workflow runs the opt-in full evaluation on Ubuntu and preserves JSON/TRX
evidence, including partial failures. It never publishes packages or fixtures.

## Licensing and release review

The pinned GGML repository declares MIT. OpenAI Whisper and runtime dependency
attribution is preserved in the package's `THIRD_PARTY_NOTICES.txt`. Dataset
fixtures remain CC BY 4.0 with their own required attribution.

A future release must review all **eleven** packages, including both STT
providers and `ElBruno.Speech.BlazorComponents`. The existing `publish.yml`
build/test/pack/package-presence review gate remains required. No release or
provider-default change is part of this addition.
