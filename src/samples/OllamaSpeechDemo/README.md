# Ollama speech in C#

A runnable example for generating speech with `ITextToSpeechClient`, saving a complete WAV, and writing raw PCM chunks as they arrive. The same provider works locally or against a remote Ollama host, without changing the speech pipeline.

## Prerequisites

- .NET 8 or .NET 10 SDK.
- Ollama 0.40.9 or later **on a host with the MLX runtime available**.
- Run `ollama pull phonon` on the Ollama host. Use `ollama show phonon` to list voices.
- Phonon currently uses an English checkpoint. Requests are limited to 4,096 characters.

**Windows caveat:** on the Windows machine used for this sample's initial verification, Ollama 0.40.11 rejects `ollama pull phonon` with `this model requires MLX support, but the MLX runtime is not available`. Having a new enough Ollama version alone is not sufficient. Point `--endpoint` at a compatible Ollama host, for example a supported Apple Silicon installation. Do not assume WSL or a Windows GPU resolves the missing MLX runtime.

Ollama's [speech announcement](https://ollama.com/blog/speech-models) explicitly says speech models currently run on Apple Silicon Macs, with Windows and Linux support coming soon. No availability date is promised. This sample remains Ollama-only: no standalone Phonon runtime or fallback is installed or included.

The provider and this sample do not download models or change the Ollama installation.

## Release and verification status

Updated for stable `1.6.0`. The provider is installed with
`dotnet add package ElBruno.Speech.Tts.Ollama --version 1.6.0` in your own
project; this repository sample references the provider source project.

Forty-nine focused synthetic provider/sample test cases pass on each of .NET 8 and
.NET 10. The console help and the failure path against the real Windows Ollama
0.40.11 server were exercised. Live synthesis returned HTTP 404 because Phonon
could not be pulled without MLX. Separately, two
[community reports](https://github.com/elbruno/ElBruno.Speech/issues/106) confirm
that the blog's HelloPhonon sample generated playable WAV audio using preview.5
on Apple M2/.NET 8 and M2 Max/.NET 10 with Ollama 0.40.11. Both users reported
clear English speech. Those reports do not verify every streaming mode, voice,
or platform, and no new real-audio run was performed on this Windows host.

## Run

From the repository root:

```powershell
dotnet run --project src\samples\OllamaSpeechDemo -f net10.0 -- --help

dotnet run --project src\samples\OllamaSpeechDemo -f net10.0 -- --voice Freya --text "Hello! This speech was generated from C sharp with Ollama Phonon." --play
```

Use `-f net8.0` instead if running .NET 8.

To use a remote server:

```powershell
dotnet run --project src\samples\OllamaSpeechDemo -f net10.0 -- --endpoint http://YOUR-OLLAMA-HOST:11434/ --voice Freya --output artifacts\ollama-speech
```

The remote server must be reachable from this machine and configured to listen on the appropriate network interface. Keep it on a trusted network; this sample does not configure authentication or expose a server.

The endpoint defaults to `http://localhost:11434/`. In `preview.4` and later,
the library completes host roots, `/v1` bases, and `/v1/audio` paths to the
speech route for both synthesis modes. Full `/v1/audio/speech` URLs remain
supported without duplication. Proxy prefixes before `/v1` and query strings
are preserved; other custom endpoint paths remain unchanged. See the
[provider URL rules](../../ElBruno.Speech.Tts.Ollama/README.md#registration).
The original `preview.3` package requires the complete speech endpoint.

If the installed .NET 11 preview SDK triggers the repository's existing `System.Version.Parse` error in `Directory.Build.props`, add `-p:NETCoreSdkVersion=11.0.100` before `--`. This is a local workaround, not a required setting on .NET 8/10.

## What the sample does

For local Ollama, register with `services.AddOllamaSpeech()`: defaults are
`http://localhost:11434/`, model `phonon`, the model's default voice, and WAV.
The CLI starts from those same library defaults and applies only supplied
`--endpoint` and `--voice` overrides. You do not need to specify either.
`Program.cs` resolves `ITextToSpeechClient`. `SpeechDemo.cs` demonstrates both APIs:

```csharp
var response = await speech.GetAudioAsync(
    text, new TextToSpeechOptions { AudioFormat = "wav" }, cancellationToken);

await foreach (var update in speech.GetStreamingAudioAsync(
    text, new TextToSpeechOptions { AudioFormat = "pcm" }, cancellationToken))
{
    foreach (var audio in update.Contents.OfType<DataContent>())
        await output.WriteAsync(audio.Data, cancellationToken);
}
```

The output directory defaults to `artifacts\ollama-speech`:

| File | Contents |
|------|----------|
| `complete.wav` | Complete WAV synthesis, checked and rewritten with final WAV sizes |
| `streamed.pcm` | Incrementally written raw 16-bit little-endian PCM, mono, 24 kHz |
| `streamed.wav` | The streamed PCM wrapped in a playable WAV container |

Ollama's streamed WAV header has placeholder sizes because the final duration is unknown. After completion, the sample writes proper WAV sizes for compatibility with file players. PCM streaming avoids needing to interpret a partially received WAV header.

The sample reports time to first PCM chunk, chunk count, byte count, and duration. These are local observations, not performance guarantees. It rejects empty, truncated, silent, or incorrectly formatted audio. The two synthesis calls are independent; their audio does not need to be byte-identical.

`--play` opens `streamed.wav` in your system's default player after synthesis, not while streaming. Without `--play`, the sample only saves files. Press Ctrl+C to cancel. An interrupted `streamed.pcm` is partial output, not a successful result.

## Verify before a blog demo

Run the focused automated tests:

```powershell
dotnet test tests\ElBruno.Speech.Tts.Ollama.Tests
```

These include synthetic audio tests for the complete sample flow, WAV headers, PCM integrity, and error handling. They do **not** prove that Phonon works on a particular machine.

On a compatible Ollama host, run the sample, then listen to **both** WAV files. Optionally check with FFmpeg:

```powershell
ffprobe -v error -show_entries stream=codec_name,sample_rate,channels,bits_per_sample -of json artifacts\ollama-speech\complete.wav
ffprobe -v error -show_entries stream=codec_name,sample_rate,channels,bits_per_sample -of json artifacts\ollama-speech\streamed.wav
ffplay -nodisp -autoexit artifacts\ollama-speech\complete.wav
ffplay -nodisp -autoexit artifacts\ollama-speech\streamed.wav
```

Expected format: `pcm_s16le`, `24000`, one channel, 16 bits. Automated file checks cannot establish pronunciation, intelligibility, or voice quality. Do not describe real Phonon output as verified until those checks and listening succeed.

Reference: [Ollama speech documentation](https://docs.ollama.com/capabilities/speech).
