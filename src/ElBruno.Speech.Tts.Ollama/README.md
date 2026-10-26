# ElBruno.Speech.Tts.Ollama

An opt-in `Microsoft.Extensions.AI.ITextToSpeechClient` for Ollama's Phonon model. The provider calls Ollama's OpenAI-compatible `POST /v1/audio/speech` endpoint and does not change `ElBruno.Speech.Pipeline`.

## Requirements

- Ollama 0.40.9 or later (0.40.11 or later is recommended by the speech announcement).
- An Apple Silicon Mac server with MLX support. Windows and Linux speech support is pending upstream, with no promised date. On the tested Windows host, Ollama 0.40.11 rejects Phonon with `this model requires MLX support, but the MLX runtime is not available`. A remote compatible Ollama endpoint can be used instead.
- Download the model with `ollama pull phonon`.
- Phonon is currently oriented toward English.
- Each request supports up to 4,096 characters.
- Ollama supports WAV (16-bit, mono, 24 kHz) and raw PCM (16-bit little-endian, mono, 24 kHz).

## Registration

```powershell
dotnet add package ElBruno.Speech.Tts.Ollama --version 1.6.0
```

```csharp
using ElBruno.Speech.Tts.Ollama;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddOllamaSpeech();

using var provider = services.BuildServiceProvider();
var speech = provider.GetRequiredService<ITextToSpeechClient>();
var audio = await speech.GetAudioAsync("Hello from Ollama.");
```

No options are required: `AddOllamaSpeech()` uses `http://localhost:11434/`,
model `phonon`, the model's default voice, and WAV output. The speech route is
resolved automatically. Passing `null` or `new OllamaSpeechOptions()` uses the
same defaults. Set only the values you want to override, for example:

```csharp
services.AddOllamaSpeech(new OllamaSpeechOptions
{
    Endpoint = new Uri("http://YOUR-OLLAMA-HOST:11434/"),
    Voice = "Freya"
});
```

This is an alternative registration, not an additional call. The model still
defaults to `phonon`. Set `AudioFormat` to `Pcm` for raw PCM output.
Per-request `VoiceId`, `ModelId`, and `AudioFormat` overrides are also supported.

Starting with `1.6.0-preview.4`, the library resolves the speech route automatically
for both complete and streaming requests, without changing your options object:

| Configured URL path | Request path |
|---------------------|--------------|
| `/` | `/v1/audio/speech` |
| `/v1` or `/v1/` | `/v1/audio/speech` |
| `/v1/audio` or `/v1/audio/` | `/v1/audio/speech` |
| `/v1/audio/speech` (with or without trailing slash) | `/v1/audio/speech` |
| `/proxy/v1/` | `/proxy/v1/audio/speech` |

HTTPS, ports, proxy prefixes before `/v1`, and query strings are preserved.
Other custom endpoint paths are used as provided for backward compatibility;
for a proxy base, include `/v1`, for example `https://host/proxy/v1/`.
Only absolute HTTP(S) URLs are accepted. Existing full speech URLs still work.
The published `1.6.0-preview.3` requires a complete endpoint and does not normalize base URLs.

Streaming requests read the HTTP response as it arrives and yield audio updates incrementally. Unsupported options such as language, speed, pitch, volume, and arbitrary additional properties are rejected rather than silently ignored.

Audio is exposed as MEAI `DataContent` (`audio/wav` or `audio/pcm`) and as the
same byte array in `RawRepresentation` for existing consumers. PCM is signed
little-endian 16-bit mono at 24 kHz; `audio/pcm` here is not big-endian `audio/L16`.
Streaming chunks are arbitrary transport fragments, not independently playable
WAV files or guaranteed sample-aligned PCM frames. Concatenate them in order.
Client metadata exposes the configured model ID.

The client owns its default `HttpClient`. A supplied `HttpClient` remains owned
by its caller, who may configure timeouts and authentication on it. Disposing a
streaming enumerator releases the response stream and HTTP response.

**Pipeline integration:** provider registration does not alter pipeline defaults.
The existing pipeline expects raw PCM matching `SpeechPipelineOptions.TargetFormat`;
do not feed its default 16 kHz output path a WAV container or unconverted 24 kHz
samples. Select PCM and adapt/resample to the output format in your application.
The standalone demo preserves the model's native 24 kHz audio. This release does
not add pipeline audio conversion.

**Verification:** synthetic provider and complete sample tests pass on .NET 8/10,
including WAV size finalization, incremental reading, cancellation, and resource
cleanup. Two [community reports](https://github.com/elbruno/ElBruno.Speech/issues/106)
confirm real WAV generation and successful playback using preview.5 on Apple M2
with .NET 8 and M2 Max with .NET 10, both with Ollama 0.40.11. The users reported
clear English speech; this is not a comprehensive quality, streaming, voice, or
performance benchmark. Local generation remains unavailable on the tested Windows
host because it lacks MLX. Model weights and voices are downloaded by Ollama, not bundled in
the NuGet package; their licenses are separate from this MIT client.

See [Ollama speech documentation](https://docs.ollama.com/capabilities/speech) for model and voice details.

For a runnable C# example with WAV/PCM validation, incremental streaming, and optional playback, see [OllamaSpeechDemo](https://github.com/elbruno/ElBruno.Speech/tree/v1.6.0/src/samples/OllamaSpeechDemo) in the repository.
