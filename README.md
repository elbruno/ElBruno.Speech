# ElBruno.Speech

A reusable, local-first speech runtime for .NET 8 and .NET 10.

```text
Audio input
    ↓
Audio normalization · resampling · framing · buffering
    ↓
Voice Activity Detection (Silero VAD)
    ↓
Turn detection · utterance assembly
    ↓
ISpeechToTextClient
    ↓
IChatClient
    ↓
Streaming text segmentation
    ↓
ITextToSpeechClient
    ↓
Audio output
```

Built on [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) provider boundaries. Works with local models (Whisper, VibeVoice, Qwen-TTS, Ollama) and cloud providers (Azure OpenAI, OpenAI) without changing orchestration code.

---

## Packages

| Package | Description |
|---------|-------------|
| `ElBruno.Speech.Abstractions` | Audio types, VAD contracts, session interfaces, error types |
| `ElBruno.Speech.Audio` | WAV I/O, PCM conversion, resampling, framing, ring buffers |
| `ElBruno.Speech.Vad.Silero` | Silero VAD via ONNX Runtime — streaming voice activity detection |
| `ElBruno.Speech.Pipeline` | VAD → STT → LLM → TTS orchestration, bounded channels, barge-in |
| `ElBruno.Speech.AspNetCore` | WebSocket endpoint, session registry, health checks |
| `ElBruno.Speech.NAudio` | Windows microphone and speaker via NAudio (isolated) |
| `ElBruno.Speech.OpenTelemetry` | Activities, metrics, Aspire-compatible instrumentation |
| `ElBruno.Speech.Cli` | `elbrunospeech` CLI tool |

---

## Quick start

```csharp
// Register providers
services.AddWhisper(o => o.Model = KnownWhisperModels.WhisperBaseEn);
services.AddLocalLLMs(o => o.Model = KnownModels.Phi35MiniInstruct);
services.AddVibeVoiceTTS(o => o.SampleRate = 24_000);
services.AddSileroVad(o => o.MinimumSilenceDuration = TimeSpan.FromMilliseconds(500));

// Build the pipeline
services.AddSpeechPipeline(builder =>
{
    builder
        .UseVoiceActivityDetector<SileroVoiceActivityDetector>()
        .UseSpeechToText(sp => sp.GetRequiredService<ISpeechToTextClient>())
        .UseChatClient(sp => sp.GetRequiredService<IChatClient>())
        .UseTextToSpeech(sp => sp.GetRequiredService<ITextToSpeechClient>())
        .UseSentenceChunking(o =>
        {
            o.MinimumCharacters = 24;
            o.MaximumCharacters = 220;
            o.FlushTimeout = TimeSpan.FromMilliseconds(350);
        })
        .UseBargeIn(o => o.CancelOnSpeechStart = true);
});

// Use it
var pipeline = sp.GetRequiredService<ISpeechPipeline>();
await using var session = await pipeline.CreateSessionAsync();

await session.WriteAudioAsync(frame);
await foreach (var update in session.GetUpdatesAsync())
{
    // SpeechStartedUpdate, FinalTranscriptUpdate, AssistantAudioChunkUpdate, ...
}
```

---

## Samples

| Sample | Description |
|--------|-------------|
| `FileToSpeech` | WAV file → transcript → answer → WAV output |
| `LocalVoiceAgent` | Microphone → VAD → Whisper → LLM → VibeVoice → speaker (barge-in) |
| `WebSocketVoiceAgent` | ASP.NET Core WebSocket endpoint + browser client |
| `AspireVoiceAgent` | Full Aspire AppHost with tracing, metrics, and dashboard |

---

## Related repositories

- [ElBruno.Whisper](https://github.com/elbruno/ElBruno.Whisper) — `ISpeechToTextClient` via ONNX Whisper
- [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs) — `IChatClient` for local LLMs
- [ElBruno.VibeVoiceTTS](https://github.com/elbruno/ElBruno.VibeVoiceTTS) — `ITextToSpeechClient` via VibeVoice
- [ElBruno.QwenTTS](https://github.com/elbruno/ElBruno.QwenTTS) — `ITextToSpeechClient` via Qwen3-TTS
- [ElBruno.HuggingFace.Downloader](https://github.com/elbruno/ElBruno.HuggingFace.Downloader) — model downloads

---

## License

MIT — see [LICENSE](LICENSE).
