# ElBruno.Speech — Product Requirements Document

**Repository:** `ElBruno.Speech`  
**Proposed URL:** https://github.com/elbruno/ElBruno.Speech  
**Status:** Ready for repository creation  
**Target frameworks:** .NET 8 and .NET 10  
**Language:** C#  
**License:** MIT  
**Date:** July 3, 2026

---

## 1. Executive summary

`ElBruno.Speech` will be a reusable, local-first speech runtime for .NET.

It will provide the missing infrastructure required to build a C# equivalent of the practical features in https://github.com/huggingface/speech-to-speech without duplicating model implementations that already exist in other ElBruno repositories.

The target pipeline is:

```text
Audio input
    ↓
Audio normalization, resampling, framing, buffering
    ↓
Voice Activity Detection
    ↓
Turn detection and utterance assembly
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

The new repository owns:

- Audio primitives and format conversion
- Streaming buffers and bounded channels
- Voice Activity Detection
- Turn detection
- Session state
- Speech pipeline orchestration
- Barge-in and cancellation
- Text coalescing for low-latency TTS
- Realtime events
- Audio transport abstractions
- ASP.NET Core and WebSocket integration
- OpenTelemetry instrumentation
- Benchmarks and provider conformance tests

Existing repositories remain responsible for model-specific inference:

- https://github.com/elbruno/ElBruno.Whisper provides Whisper speech-to-text.
- https://github.com/elbruno/ElBruno.LocalLLMs provides local `IChatClient` implementations.
- https://github.com/elbruno/ElBruno.QwenTTS provides Qwen3-TTS inference.
- https://github.com/elbruno/ElBruno.VibeVoiceTTS provides VibeVoice inference.
- https://github.com/elbruno/ElBruno.HuggingFace.Downloader provides Hugging Face asset downloads.

`ElBruno.Speech` must not become a copied collection of model runtimes. It should be the stable orchestration layer that composes providers through Microsoft.Extensions.AI.

---

## 2. Product decision

A C# implementation is feasible.

The recommended approach is not a line-by-line Python port. The implementation should preserve the useful behavior while using idiomatic .NET:

| Python concept | .NET implementation |
|---|---|
| Queue-based handlers | `System.Threading.Channels` |
| Handler loops | Async tasks, `BackgroundService`, and async iterators |
| Per-session state | Scoped `SpeechSession` |
| Model backend selection | Dependency injection and Microsoft.Extensions.AI |
| VAD iterator | Stateful `IVoiceActivitySession` |
| STT backend | `ISpeechToTextClient` |
| Language model backend | `IChatClient` |
| TTS backend | `ITextToSpeechClient` |
| WebSocket transport | ASP.NET Core WebSockets |
| Realtime session | `IRealtimeClient` or an ElBruno session abstraction |
| Python dataclasses | C# records and options classes |
| Timing logs | OpenTelemetry activities and metrics |

The Hugging Face project is primarily an inference and orchestration project. It loads pretrained components and wires them together. The useful behaviors to preserve are:

- Modular providers
- Streaming queues
- Per-session state
- Stale-input filtering
- Speech-start and speech-end detection
- Cancellation when the user interrupts
- Incremental LLM output
- TTS text coalescing
- WebSocket lifecycle management
- Realtime session pooling
- Tests for VAD, streaming, stale inputs, speculative turns, TTS coalescing, and session lifecycle

References:

- https://github.com/huggingface/speech-to-speech
- https://github.com/huggingface/speech-to-speech/blob/main/src/speech_to_speech/baseHandler.py
- https://github.com/huggingface/speech-to-speech/blob/main/src/speech_to_speech/s2s_pipeline.py
- https://github.com/huggingface/speech-to-speech/blob/main/src/speech_to_speech/VAD/vad_handler.py
- https://github.com/huggingface/speech-to-speech/blob/main/src/speech_to_speech/VAD/vad_iterator.py
- https://github.com/huggingface/speech-to-speech/tree/main/tests

---

## 3. Existing ElBruno libraries and confirmed gaps

### 3.1 ElBruno.Whisper

Repository:

https://github.com/elbruno/ElBruno.Whisper

Already implemented:

- Local Whisper inference through ONNX Runtime
- Automatic model download
- Tiny, base, small, medium, and large model families
- English and multilingual variants
- Dependency injection through `AddWhisper`
- Progress reporting
- Detected language
- Duration
- Timestamp support
- Unit and integration tests
- OIDC Trusted Publishing

The new project must not rebuild Whisper.

Missing work in `ElBruno.Whisper`:

- Implement `Microsoft.Extensions.AI.ISpeechToTextClient`
- Accept arbitrary caller-owned `Stream` input
- Accept raw PCM with explicit format metadata
- Support Microsoft.Extensions.AI `DataContent`
- Implement incremental transcription through `GetStreamingTextAsync`
- Map segment and word timestamps into MEAI response metadata
- Define and test concurrent-use behavior
- Improve cancellation responsiveness
- Remove file-only assumptions from the integration path

### 3.2 ElBruno.LocalLLMs

Repository:

https://github.com/elbruno/ElBruno.LocalLLMs

Already suitable:

- Implements `IChatClient`
- Token streaming
- CPU, CUDA, and DirectML
- Dependency injection
- Automatic model download
- Model metadata
- Structured errors

Only realtime hardening is required:

- Verify prompt cancellation aborts generation quickly
- Add deterministic cancellation tests
- Emit lifecycle diagnostics useful for speech orchestration

### 3.3 ElBruno.VibeVoiceTTS

Repository:

https://github.com/elbruno/ElBruno.VibeVoiceTTS

Already implemented:

- Native C# ONNX Runtime inference
- In-memory `float[]` audio
- WAV output
- Voice presets
- Dependency injection
- CUDA, DirectML, and CPU fallback
- Automatic model download

Missing:

- `ITextToSpeechClient`
- Streaming PCM chunks
- Cancellation during generation
- Explicit concurrency policy
- Standard MIME types and metadata

This is the recommended first TTS provider because it already returns audio in memory.

### 3.4 ElBruno.QwenTTS

Repository:

https://github.com/elbruno/ElBruno.QwenTTS

Already implemented:

- Native C# ONNX Runtime inference
- Qwen3-TTS 0.6B and 1.7B
- Multiple speakers
- Instruct control
- Voice cloning
- CUDA and DirectML
- Automatic model download
- WAV output

Missing:

- `ITextToSpeechClient`
- In-memory output as the primary core API
- Streaming PCM chunks
- Cancellation
- Explicit thread-safety and pooling behavior
- Standard response metadata

### 3.5 ElBruno.HuggingFace.Downloader

Repository:

https://github.com/elbruno/ElBruno.HuggingFace.Downloader

Already implemented:

- Required and optional files
- Authentication
- Atomic writes
- Progress
- Caching
- Dependency injection
- CLI
- Existing-file detection

Missing for large speech bundles:

- Revision and commit pinning
- Snapshot-style downloads
- Manifest-based bundles
- SHA-256 verification
- Resumable downloads
- Cache keys by repository and resolved revision

---

## 4. Goals

### G-001 — Reusable speech runtime

Provide reusable components for audio streaming, VAD, turn detection, and speech orchestration.

### G-002 — Microsoft.Extensions.AI first

Use these provider boundaries:

- `ISpeechToTextClient`
- `IChatClient`
- `ITextToSpeechClient`
- `IRealtimeClient`, where appropriate

Microsoft.Extensions.AI `10.7.0` is current at the time of this PRD. Versions must be centrally managed.

### G-003 — Local-first and provider-neutral

The main sample works entirely locally, while the runtime also supports Azure, OpenAI-compatible, Ollama-compatible, and custom providers.

### G-004 — Streaming

Stream at every useful boundary:

- Incoming PCM frames
- VAD decisions
- Partial and final transcription
- LLM text deltas
- TTS audio chunks
- Session events

### G-005 — Barge-in

When the user begins speaking while the assistant is generating or playing audio, cancel the current response and prioritize the new turn.

### G-006 — Bounded resource use

All queues are bounded. No component accumulates unlimited audio or text.

### G-007 — Testability

Most tests run with fake providers and deterministic prerecorded audio. Model downloads and GPU tests are excluded from default CI.

### G-008 — NuGet-ready

Packages are independently consumable and published through NuGet Trusted Publishing with GitHub Actions OIDC.

---

## 5. Non-goals

The first releases will not:

- Train models
- Reimplement Whisper, Qwen3-TTS, or VibeVoice
- Port every Python backend
- Guarantee bit-for-bit preprocessing parity
- Implement speaker diarization in the MVP
- Implement voice cloning in the orchestration package
- Build a desktop UI
- Require Semantic Kernel
- Require Microsoft Agent Framework
- Require Aspire
- Require Python at runtime
- Ship model binaries inside NuGet packages

---

## 6. Primary use cases

### UC-001 — Audio file to spoken answer

Input a WAV file, transcribe it, send the transcript to an `IChatClient`, synthesize the answer, and save a WAV file.

### UC-002 — Local microphone conversation

Stream microphone frames into a session and play synthesized chunks as they become available.

### UC-003 — WebSocket voice agent

Stream audio through ASP.NET Core WebSockets and receive transcript, state, text, and audio events.

### UC-004 — Barge-in

Interrupt an assistant response by speaking. The old response is cancelled and a new turn begins.

### UC-005 — Cloud/local mixing

Use local Whisper, Azure OpenAI, and local VibeVoice without changing orchestration code.

### UC-006 — Aspire development experience

Run the sample through Aspire and view traces, logs, metrics, and health checks.

---

## 7. Architecture principles

1. Provider-specific inference stays outside this repository.
2. Internal audio processing uses allocation-conscious native types.
3. Public provider boundaries use Microsoft.Extensions.AI.
4. Channels are bounded.
5. Cancellation propagates end-to-end.
6. Session state is isolated.
7. Audio transport is separate from orchestration.
8. Core packages remain platform-neutral.
9. Windows audio capture is isolated in its own package.
10. Every stage emits structured observability.
11. Audio is not persisted unless explicitly requested.
12. Models are never bundled in NuGet packages.

---

## 8. Repository structure

All production code lives under `/src`.

All documentation lives under `/docs`, except `README.md` and `LICENSE`.

```text
/
├── .github/
│   ├── ISSUE_TEMPLATE/
│   └── workflows/
│       ├── ci.yml
│       ├── publish.yml
│       └── codeql.yml
├── docs/
│   ├── PRD.md
│   ├── architecture.md
│   ├── audio-formats.md
│   ├── vad.md
│   ├── pipeline.md
│   ├── realtime.md
│   ├── observability.md
│   ├── performance.md
│   ├── testing.md
│   ├── publishing.md
│   └── provider-conformance.md
├── src/
│   ├── ElBruno.Speech.Abstractions/
│   ├── ElBruno.Speech.Audio/
│   ├── ElBruno.Speech.Vad.Silero/
│   ├── ElBruno.Speech.Pipeline/
│   ├── ElBruno.Speech.AspNetCore/
│   ├── ElBruno.Speech.NAudio/
│   ├── ElBruno.Speech.OpenTelemetry/
│   ├── ElBruno.Speech.Cli/
│   └── samples/
│       ├── FileToSpeech/
│       ├── LocalVoiceAgent/
│       ├── WebSocketVoiceAgent/
│       └── AspireVoiceAgent/
├── tests/
│   ├── ElBruno.Speech.Audio.Tests/
│   ├── ElBruno.Speech.Vad.Silero.Tests/
│   ├── ElBruno.Speech.Pipeline.Tests/
│   ├── ElBruno.Speech.AspNetCore.Tests/
│   ├── ElBruno.Speech.Conformance.Tests/
│   └── ElBruno.Speech.IntegrationTests/
├── benchmarks/
│   └── ElBruno.Speech.Benchmarks/
├── Directory.Build.props
├── Directory.Packages.props
├── ElBruno.Speech.slnx
├── global.json
├── LICENSE
└── README.md
```

---

## 9. Proposed NuGet packages

### ElBruno.Speech.Abstractions

Contains:

- Audio format records
- Audio frame
- VAD contracts
- Speech session contracts
- Pipeline update types
- Turn and transcript types
- No native dependencies

### ElBruno.Speech.Audio

Contains:

- WAV reader/writer
- PCM conversion
- Mono conversion
- Resampling
- Normalization
- Framing
- Ring buffers
- Pooled buffers
- Stream adapters
- Duration calculations

### ElBruno.Speech.Vad.Silero

Contains:

- Silero ONNX runtime
- Stateful streaming VAD
- Speech-start/end detection
- Threshold and silence configuration
- Downloader integration

### ElBruno.Speech.Pipeline

Contains:

- VAD → STT → LLM → TTS orchestration
- Bounded channels
- Text chunking
- Barge-in
- Session lifecycle
- Cancellation
- Provider capability handling

### ElBruno.Speech.AspNetCore

Contains:

- Service registration
- WebSocket endpoint mapping
- Session registry
- Health checks
- Binary and JSON protocol
- Optional realtime protocol mapping

### ElBruno.Speech.NAudio

Contains:

- Windows microphone input
- Windows speaker output
- Device enumeration
- Format negotiation
- NAudio dependency isolated from core

### ElBruno.Speech.OpenTelemetry

Contains:

- Activities
- Metrics
- Instrumentation helpers
- Aspire-friendly setup

### ElBruno.Speech.Cli

Commands:

```text
elbrunospeech devices
elbrunospeech transcribe input.wav
elbrunospeech vad input.wav
elbrunospeech talk
elbrunospeech serve
elbrunospeech benchmark
```

---

## 10. Core domain model

### AudioFormat

```csharp
public sealed record AudioFormat(
    int SampleRate,
    int Channels,
    AudioSampleFormat SampleFormat)
{
    public static AudioFormat Pcm16KhzMono =>
        new(16_000, 1, AudioSampleFormat.Int16);

    public int BytesPerSample { get; }
    public int BytesPerSecond { get; }
}
```

### AudioFrame

```csharp
public readonly record struct AudioFrame(
    ReadOnlyMemory<byte> Data,
    AudioFormat Format,
    long SequenceNumber,
    TimeSpan Timestamp,
    bool IsFinal = false);
```

Requirements:

- Immutable frame view
- Explicit pooled-memory ownership rules
- Monotonic sequence numbers per session
- Long-lived consumers copy memory

### VAD contracts

```csharp
public interface IVoiceActivityDetector : IAsyncDisposable
{
    ValueTask<IVoiceActivitySession> CreateSessionAsync(
        VoiceActivityDetectionOptions? options = null,
        CancellationToken cancellationToken = default);
}

public interface IVoiceActivitySession : IAsyncDisposable
{
    ValueTask<VoiceActivityResult> ProcessAsync(
        AudioFrame frame,
        CancellationToken cancellationToken = default);

    ValueTask ResetAsync(CancellationToken cancellationToken = default);
}
```

```csharp
public sealed record VoiceActivityResult(
    float SpeechProbability,
    VoiceActivityState State,
    bool SpeechStarted,
    bool SpeechEnded,
    TimeSpan? SpeechStart,
    TimeSpan? SpeechEnd);
```

### Speech session

```csharp
public interface ISpeechSession : IAsyncDisposable
{
    string SessionId { get; }

    ValueTask WriteAudioAsync(
        AudioFrame frame,
        CancellationToken cancellationToken = default);

    ValueTask CompleteInputAsync(
        CancellationToken cancellationToken = default);

    ValueTask CancelResponseAsync(
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<SpeechSessionUpdate> GetUpdatesAsync(
        CancellationToken cancellationToken = default);
}
```

### Speech pipeline

```csharp
public interface ISpeechPipeline
{
    ValueTask<ISpeechSession> CreateSessionAsync(
        SpeechSessionOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### Session updates

```csharp
public abstract record SpeechSessionUpdate(
    string SessionId,
    long SequenceNumber,
    DateTimeOffset CreatedAt);

public sealed record SpeechStartedUpdate(...) : SpeechSessionUpdate(...);
public sealed record SpeechEndedUpdate(...) : SpeechSessionUpdate(...);
public sealed record PartialTranscriptUpdate(...) : SpeechSessionUpdate(...);
public sealed record FinalTranscriptUpdate(...) : SpeechSessionUpdate(...);
public sealed record AssistantTextDeltaUpdate(...) : SpeechSessionUpdate(...);
public sealed record AssistantAudioChunkUpdate(...) : SpeechSessionUpdate(...);
public sealed record ResponseStartedUpdate(...) : SpeechSessionUpdate(...);
public sealed record ResponseCompletedUpdate(...) : SpeechSessionUpdate(...);
public sealed record ResponseCancelledUpdate(...) : SpeechSessionUpdate(...);
public sealed record SpeechSessionErrorUpdate(...) : SpeechSessionUpdate(...);
```

---

## 11. Pipeline builder API

```csharp
services.AddSpeechPipeline(builder =>
{
    builder
        .UseVoiceActivityDetector<SileroVoiceActivityDetector>()
        .UseSpeechToText(sp => sp.GetRequiredService<ISpeechToTextClient>())
        .UseChatClient(sp => sp.GetRequiredService<IChatClient>())
        .UseTextToSpeech(sp => sp.GetRequiredService<ITextToSpeechClient>())
        .UseSentenceChunking(options =>
        {
            options.MinimumCharacters = 24;
            options.MaximumCharacters = 220;
            options.FlushTimeout = TimeSpan.FromMilliseconds(350);
        })
        .UseBargeIn(options =>
        {
            options.Enabled = true;
            options.CancelOnSpeechStart = true;
        })
        .UseBoundedChannels(options =>
        {
            options.AudioCapacity = 64;
            options.TextCapacity = 128;
            options.OutputCapacity = 128;
        });
});
```

A direct-construction API must also exist for console applications.

---

## 12. Functional requirements

### 12.1 Audio input and normalization

- Accept PCM16 and Float32.
- Support mono and stereo.
- Support 8, 16, 22.05, 24, 44.1, and 48 kHz.
- Normalize STT/VAD input to 16 kHz mono.
- Support streaming and complete-stream conversion.
- Read and write RIFF/WAVE PCM and IEEE float.
- Reject unsupported compressed formats clearly.
- Avoid per-frame heap allocations after warmup.
- Retain configurable pre-roll audio. Default: 200 ms.
- Preserve timing through resampling.
- Keep caller-owned streams open.

### 12.2 Voice Activity Detection

Implement Silero VAD through ONNX Runtime.

Reference:

https://github.com/snakers4/silero-vad

Suggested options:

```csharp
public sealed class SileroVadOptions
{
    public float SpeechThreshold { get; set; } = 0.5f;
    public float NegativeThreshold { get; set; } = 0.35f;
    public TimeSpan MinimumSpeechDuration { get; set; } =
        TimeSpan.FromMilliseconds(250);
    public TimeSpan MinimumSilenceDuration { get; set; } =
        TimeSpan.FromMilliseconds(500);
    public TimeSpan SpeechPadding { get; set; } =
        TimeSpan.FromMilliseconds(150);
    public TimeSpan PreRoll { get; set; } =
        TimeSpan.FromMilliseconds(200);
}
```

Requirements:

- Isolated recurrent state per session
- Reset without recreating the model
- Shared immutable ONNX session when safe
- Emit probability and state transitions
- Download model through `ElBruno.HuggingFace.Downloader`
- Support 8 kHz and 16 kHz when available

### 12.3 Turn assembly

- Start on confirmed speech.
- Include pre-roll.
- End after configured silence.
- Configurable maximum utterance duration. Default: 30 seconds.
- Support rolling long-utterance segments.
- Every utterance has turn ID, timestamps, duration, format, and optional VAD confidence.
- Stale frames from an older turn cannot enter a newer turn.

### 12.4 Speech-to-text

- Consume `ISpeechToTextClient`.
- Support `GetTextAsync` and `GetStreamingTextAsync`.
- Do not fake partial transcripts for providers that only return final text.
- Include language, timestamps, model, and provider metadata.
- Isolate non-fatal failures to the current turn.

### 12.5 Language model

- Consume `IChatClient`.
- Use streaming by default.
- Keep history per session.
- Support initial system prompt, initial history, chat options, tools, and reducers.
- Allow stateless sessions.
- Work with local and cloud clients.

### 12.6 Text segmentation for TTS

Do not wait for the complete LLM response.

Flush at:

- Sentence punctuation
- Newline
- Maximum character count
- Maximum wait time
- End of response

Avoid splitting:

- Decimal numbers
- Common abbreviations
- URLs
- Email addresses
- Markdown code fences
- Tool-call JSON

Tool calls must never be spoken unless explicitly enabled.

The text normalizer must be replaceable.

### 12.7 Text-to-speech

- Consume `ITextToSpeechClient`.
- Use `GetStreamingAudioAsync` when supported.
- Fall back to `GetAudioAsync`.
- Emit MIME type, sample rate, channels, segment ID, sequence number, and final flag.
- Preserve output order.
- Configure TTS concurrency. Default: one.

### 12.8 Barge-in and cancellation

When VAD confirms new speech during an assistant response:

- Cancel LLM generation.
- Cancel pending text segments.
- Cancel current TTS.
- Clear pending playback.
- Emit `ResponseCancelledUpdate`.
- Keep providers alive.
- Reject stale updates by generation ID.

History policies:

- Keep complete assistant text
- Keep only spoken text
- Remove interrupted response
- Add interruption annotation

Pipeline cancellation-overhead target: under 250 ms, excluding providers that ignore cancellation.

### 12.9 Session management

Each session owns:

- VAD state
- Conversation history
- Turn sequence
- Cancellation sources
- Audio buffers
- Output sequence numbers

States:

- Created
- Listening
- User speaking
- Transcribing
- Thinking
- Speaking
- Interrupted
- Completed
- Faulted
- Disposed

Support idle timeout and maximum concurrent sessions.

### 12.10 Transport abstractions

```csharp
public interface IAudioInput : IAsyncDisposable
{
    AudioFormat Format { get; }

    IAsyncEnumerable<AudioFrame> ReadFramesAsync(
        CancellationToken cancellationToken = default);
}

public interface IAudioOutput : IAsyncDisposable
{
    ValueTask WriteAsync(
        AudioFrame frame,
        CancellationToken cancellationToken = default);

    ValueTask ClearAsync(
        CancellationToken cancellationToken = default);
}
```

Required implementations:

- File input
- Memory input
- Null output
- WAV output
- NAudio microphone
- NAudio speaker
- WebSocket input/output

### 12.11 ASP.NET Core

```csharp
services.AddElBrunoSpeech();
app.MapElBrunoSpeechWebSocket("/speech");
app.MapElBrunoSpeechHealthChecks("/health/speech");
```

Protocol commands:

- Configure session
- Append audio
- Commit input
- Cancel response
- Clear output
- Close session

Server events:

- Session created
- Speech started
- Speech stopped
- Transcript delta
- Transcript final
- Response text delta
- Response audio delta
- Response completed
- Response cancelled
- Error

OpenAI Realtime compatibility should be a later layer and must not block the MVP.

---

## 13. Microsoft.Extensions.AI integration

Current relevant abstractions include:

- `ISpeechToTextClient`
- `SpeechToTextResponse`
- `SpeechToTextResponseUpdate`
- `ITextToSpeechClient`
- `TextToSpeechResponse`
- `TextToSpeechResponseUpdate`
- `IRealtimeClient`
- `IRealtimeClientSession`
- `RealtimeSessionOptions`
- `VoiceActivityDetectionOptions`
- Logging middleware
- OpenTelemetry middleware

The speech and realtime APIs are marked experimental at the time of this PRD.

Rules:

1. Use Central Package Management.
2. Isolate MEAI integration from DSP internals.
3. Do not leak MEAI concrete types into `ElBruno.Speech.Audio`.
4. Add contract tests for package upgrades.
5. Provider libraries reference `Microsoft.Extensions.AI.Abstractions`.
6. Applications and orchestration packages may reference `Microsoft.Extensions.AI`.

References:

- https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions
- https://www.nuget.org/packages/Microsoft.Extensions.AI
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.ispeechtotextclient
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.itexttospeechclient
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.irealtimeclient

---

## 14. Dependency injection example

```csharp
services.AddWhisper(options =>
{
    options.Model = KnownWhisperModels.WhisperBaseEn;
});

services.AddLocalLLMs(options =>
{
    options.Model = KnownModels.Phi35MiniInstruct;
});

services.AddVibeVoiceTTS(options =>
{
    options.SampleRate = 24_000;
});

services.AddSileroVad(options =>
{
    options.MinimumSilenceDuration = TimeSpan.FromMilliseconds(500);
});

services.AddSpeechPipeline(options =>
{
    options.EnableBargeIn = true;
    options.SystemPrompt = """
        You are a concise and helpful voice assistant.
        Keep answers short unless the user asks for detail.
        """;
});
```

The container should resolve:

```csharp
ISpeechToTextClient
IChatClient
ITextToSpeechClient
IVoiceActivityDetector
ISpeechPipeline
```

---

## 15. Concurrency and backpressure

Use bounded `Channel<T>` instances.

Recommended defaults:

| Channel | Capacity | Full mode |
|---|---:|---|
| Incoming audio | 64 | Wait, or drop oldest only in explicitly configured realtime mode |
| VAD results | 64 | Wait |
| Utterances | 4 | Wait |
| Transcript updates | 64 | Wait |
| LLM text updates | 128 | Wait |
| Speakable segments | 16 | Wait |
| Audio output | 128 | Wait |
| Public session updates | 256 | Drop only non-terminal diagnostics |

Final transcript, error, completion, and cancellation events must never be silently dropped.

Buffer rules:

- Use `ArrayPool<byte>` or `MemoryPool<byte>`.
- Return each buffer once.
- Never expose returned memory.
- Add cancellation and disposal stress tests.

Provider capabilities:

```csharp
public sealed record SpeechProviderCapabilities(
    bool SupportsConcurrentRequests,
    bool SupportsStreaming,
    bool SupportsProgressiveGeneration,
    bool SupportsCancellation,
    int? RecommendedMaxConcurrency);
```

A provider may be wrapped by a semaphore-based limiter.

---

## 16. Observability

Activity source:

```text
ElBruno.Speech
```

Activities:

- `speech.session`
- `speech.vad`
- `speech.turn`
- `speech.transcription`
- `speech.chat`
- `speech.tts`
- `speech.audio.playback`
- `speech.response`
- `speech.barge_in`

Metrics:

- `speech.sessions.active`
- `speech.sessions.created`
- `speech.sessions.failed`
- `speech.audio.frames.received`
- `speech.audio.frames.dropped`
- `speech.vad.frame.duration`
- `speech.vad.speech_probability`
- `speech.turn.duration`
- `speech.stt.time_to_first_update`
- `speech.stt.duration`
- `speech.llm.time_to_first_token`
- `speech.tts.time_to_first_audio`
- `speech.tts.duration`
- `speech.response.time_to_first_audio`
- `speech.responses.cancelled`
- `speech.channel.depth`
- `speech.channel.wait_duration`

Privacy defaults:

- Do not log audio.
- Do not log transcripts.
- Do not log prompts.
- Do not log synthesized audio.
- Allow opt-in content logging only for local development.

---

## 17. Error model

```csharp
public class SpeechPipelineException : Exception
{
    public string ErrorCode { get; }
    public bool IsTransient { get; }
    public string? SessionId { get; }
    public string? TurnId { get; }
}
```

Derived errors:

- `UnsupportedAudioFormatException`
- `AudioBufferOverflowException`
- `VadInferenceException`
- `SpeechToTextProviderException`
- `ChatProviderException`
- `TextToSpeechProviderException`
- `SpeechSessionCapacityException`
- `SpeechSessionDisposedException`
- `SpeechProviderCapabilityException`
- `SpeechProtocolException`

Public errors must avoid leaking tokens, private content, or sensitive paths.

---

## 18. Security requirements

- No long-lived NuGet API key.
- Do not log Hugging Face tokens.
- Support `HF_TOKEN` or explicit credential providers.
- Validate WebSocket message sizes.
- Configure maximum audio bytes per session.
- Configure maximum utterance duration.
- Configure maximum response text length.
- Reject unsupported content types.
- Use provider timeouts and cancellation.
- Disable persistence by default.
- Validate model manifests and hashes when available.
- Run CodeQL and Dependabot.
- Produce deterministic builds, symbols, and Source Link.

---

## 19. Performance requirements

Performance depends on model and hardware. Pipeline overhead must be measured separately.

- No unbounded queues.
- Audio framing allocates zero managed objects per steady-state frame after warmup.
- VAD runs faster than realtime on a current x64 CPU.
- Cancellation overhead is under 250 ms when providers honor cancellation.
- Benchmark first transcript, first token, first audio, total latency, real-time factor, RAM, VRAM, allocations, and concurrent sessions.

Reference environments:

- CPU-only laptop
- NVIDIA workstation
- Windows DirectML machine
- Linux CPU
- Linux CUDA

---

## 20. Testing strategy

### Unit tests

No model downloads.

Test:

- WAV parsing
- PCM conversion
- Resampling
- Frame boundaries
- Ring buffer wraparound
- VAD state transitions with fake probabilities
- Turn assembly
- Text segmentation
- Bounded channels
- Cancellation
- Stale-frame rejection
- Session state
- Error mapping
- Event ordering

### Golden audio

Small committed fixtures:

- Silence
- One word
- Short sentence
- Leading silence
- Trailing silence
- Two turns
- Background noise
- 8 kHz mono
- 48 kHz stereo

### Integration categories

```text
Integration
ModelDownload
Gpu
WebSocket
LongRunning
```

Default CI excludes model downloads, GPU, and long-running tests.

### Provider conformance

Reusable tests for:

- `ISpeechToTextClient`
- `ITextToSpeechClient`
- `IChatClient` cancellation
- VAD providers

Verify:

- Disposal
- Cancellation
- Stream ownership
- Metadata
- Empty input
- Invalid format
- Concurrent calls
- Update ordering
- Exactly one terminal outcome

### End-to-end

Use fake STT, LLM, and TTS providers for deterministic full-pipeline tests.

---

## 21. Samples

### FileToSpeech

```text
input.wav → Whisper → LocalLLM → VibeVoice → output.wav
```

### LocalVoiceAgent

- NAudio microphone
- Silero VAD
- ElBruno.Whisper
- ElBruno.LocalLLMs
- ElBruno.VibeVoiceTTS
- Speaker playback
- Barge-in

### WebSocketVoiceAgent

ASP.NET Core WebSocket endpoint with a browser client.

### AspireVoiceAgent

Aspire AppHost with service, web UI, telemetry, health checks, and dashboard.

---

## 22. Roadmap

### Phase 0 — Repository foundation

Deliver:

- Repository and MIT license
- README and docs
- `.slnx`
- `Directory.Build.props`
- `Directory.Packages.props`
- CI, CodeQL, Dependabot
- Package metadata
- Trusted Publishing workflow
- Fake provider test infrastructure

Exit:

- `dotnet build`
- `dotnet test`
- `dotnet pack`
- Windows and Linux CI green

### Phase 1 — Audio primitives

Deliver:

- Audio types
- WAV reader/writer
- PCM16 and Float32 conversion
- Mono conversion
- Streaming resampler
- Framer
- Ring buffer
- File input/output
- Benchmarks

### Phase 2 — Silero VAD and turn detection

Deliver:

- Silero ONNX provider
- Session state
- Speech-start/end
- Pre-roll
- Utterance assembly
- VAD CLI
- Tests

### Phase 3 — File-based MVP

Deliver:

- `ISpeechPipeline`
- STT → LLM → TTS
- FileToSpeech sample
- Text segmentation
- WAV output

Dependencies:

- `ElBruno.Whisper` implements `ISpeechToTextClient`
- One TTS provider implements `ITextToSpeechClient`

Suggested release:

```text
0.1.0-preview.1
```

### Phase 4 — Streaming local voice agent

Deliver:

- Microphone and speaker
- Streaming session
- Partial transcript events
- Streaming audio
- Barge-in
- LocalVoiceAgent

Suggested release:

```text
0.2.0-preview.1
```

### Phase 5 — ASP.NET Core and WebSockets

Deliver:

- Registration
- WebSocket protocol
- Browser sample
- Capacity management
- Health checks
- Authentication hooks

Suggested release:

```text
0.3.0-preview.1
```

### Phase 6 — MEAI realtime adapter

Deliver:

- Evaluate direct `IRealtimeClient`
- Map session events
- Compatibility tests
- Experimental API docs

### Phase 7 — Production hardening

Deliver:

- OpenTelemetry package
- Aspire sample
- Performance baselines
- Load tests
- Security review
- Stable provider capabilities

Suggested stable release:

```text
1.0.0
```

---

## 23. MVP definition

The smallest useful MVP is:

```text
WAV input
  → audio normalization
  → optional VAD
  → ElBruno.Whisper through ISpeechToTextClient
  → any IChatClient
  → sentence chunking
  → any ITextToSpeechClient
  → WAV output
```

MVP limits:

- One session
- File input and output
- No microphone
- No WebSocket
- No barge-in
- No realtime protocol compatibility
- Streaming LLM text may be used
- TTS may initially return complete audio per text segment

---

## 24. Version 1.0 acceptance criteria

- Windows and Linux
- `net8.0` and `net10.0`
- No NAudio dependency in core
- No Python runtime
- MEAI provider boundaries
- Silero VAD
- Raw PCM streaming
- File and WebSocket transports
- Bounded backpressure
- Barge-in
- Cancellation
- Multiple isolated sessions
- OpenTelemetry
- Fully local sample
- Cloud/local sample
- Unit, conformance, integration, and load tests
- OIDC Trusted Publishing
- Source Link, symbols, README, license, icon
- Migration notes for experimental MEAI changes

---

## 25. Package metadata

Shared settings:

```xml
<PropertyGroup>
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  <LangVersion>latest</LangVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>

  <Authors>Bruno Capuano</Authors>
  <Company>ElBruno</Company>
  <PackageProjectUrl>https://github.com/elbruno/ElBruno.Speech</PackageProjectUrl>
  <RepositoryUrl>https://github.com/elbruno/ElBruno.Speech</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>nuget_logo.png</PackageIcon>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  <Deterministic>true</Deterministic>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

Every package contains:

- README
- LICENSE
- Icon
- XML docs
- Source Link
- Symbol package

---

## 26. NuGet Trusted Publishing

Follow the pattern used by:

- https://github.com/elbruno/ElBruno.LocalLLMs/blob/main/docs/publishing.md
- https://github.com/elbruno/ElBruno.Whisper/blob/main/docs/publishing.md

Official references:

- https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing
- https://devblogs.microsoft.com/dotnet/enhanced-security-is-here-with-the-new-trust-publishing-on-nuget-org/

### NuGet.org policy

Create a Trusted Publishing policy:

- Package owner: the ElBruno NuGet account or organization
- Repository owner: `elbruno`
- Repository: `ElBruno.Speech`
- Workflow file: `publish.yml`
- Environment: `release`

Use the filename only, not `.github/workflows/publish.yml`.

### GitHub environment

Create:

```text
release
```

Recommended protection:

- Main branch only
- Optional approval
- No deployment secret required

### GitHub secret

Create:

```text
NUGET_USER
```

Value: the NuGet.org profile username, not an email address.

Do not store a long-lived NuGet API key.

### Workflow permission

```yaml
permissions:
  contents: read
  id-token: write
```

---

## 27. Proposed publish workflow

Create `.github/workflows/publish.yml`:

```yaml
name: Publish NuGet packages

on:
  release:
    types: [published]
  workflow_dispatch:
    inputs:
      version:
        description: Optional package version
        required: false
        type: string

permissions:
  contents: read
  id-token: write

jobs:
  build-test-pack-publish:
    runs-on: ubuntu-latest
    environment: release

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            10.0.x

      - name: Resolve version
        id: version
        shell: bash
        run: |
          if [ -n "${{ inputs.version }}" ]; then
            VERSION="${{ inputs.version }}"
          else
            VERSION="${GITHUB_REF_NAME#v}"
          fi

          if [ -z "$VERSION" ]; then
            echo "Unable to resolve version."
            exit 1
          fi

          echo "value=$VERSION" >> "$GITHUB_OUTPUT"

      - name: Restore
        run: dotnet restore ElBruno.Speech.slnx

      - name: Build
        run: >
          dotnet build ElBruno.Speech.slnx
          --configuration Release
          --no-restore
          /p:Version=${{ steps.version.outputs.value }}

      - name: Test
        run: >
          dotnet test ElBruno.Speech.slnx
          --configuration Release
          --no-build
          --filter "Category!=Integration&Category!=Gpu&Category!=LongRunning"
          --logger "trx"
          --results-directory artifacts/test-results

      - name: Pack
        run: >
          dotnet pack ElBruno.Speech.slnx
          --configuration Release
          --no-build
          --output artifacts/packages
          /p:Version=${{ steps.version.outputs.value }}

      - name: Verify expected packages
        shell: bash
        run: |
          find artifacts/packages -name "*.nupkg" -not -name "*.symbols.nupkg" -print

          COUNT=$(find artifacts/packages -name "*.nupkg" -not -name "*.symbols.nupkg" | wc -l)
          if [ "$COUNT" -lt 1 ]; then
            echo "No packages were produced."
            exit 1
          fi

      - name: Upload package artifacts
        uses: actions/upload-artifact@v4
        with:
          name: nuget-packages-${{ steps.version.outputs.value }}
          path: |
            artifacts/packages/*.nupkg
            artifacts/packages/*.snupkg

      - name: NuGet OIDC login
        id: nuget-login
        uses: NuGet/login@v1
        with:
          user: ${{ secrets.NUGET_USER }}

      - name: Publish packages
        shell: bash
        run: |
          for package in artifacts/packages/*.nupkg; do
            dotnet nuget push "$package" \
              --api-key "${{ steps.nuget-login.outputs.NUGET_API_KEY }}" \
              --source "https://api.nuget.org/v3/index.json" \
              --skip-duplicate
          done
```

Notes:

- Request the temporary key immediately before publishing.
- NuGet temporary keys are short-lived.
- `--skip-duplicate` makes reruns safe.
- `.snupkg` files should be generated beside packages and retained as workflow artifacts. Validate symbol ingestion on NuGet.org after the first publish.
- Add a package allowlist before the first stable release so sample projects cannot be packed accidentally.

Recommended allowlist:

```text
ElBruno.Speech.Abstractions
ElBruno.Speech.Audio
ElBruno.Speech.Vad.Silero
ElBruno.Speech.Pipeline
ElBruno.Speech.AspNetCore
ElBruno.Speech.NAudio
ElBruno.Speech.OpenTelemetry
```

---

## 28. Release process

1. Update changelog.
2. Run API compatibility checks.
3. Run unit tests.
4. Run conformance tests.
5. Run integration tests.
6. Run the file-to-speech sample.
7. Run the realtime sample when applicable.
8. Pack locally.
9. Inspect package contents.
10. Confirm no model files are included.
11. Create GitHub release `vX.Y.Z`.
12. Monitor `publish.yml`.
13. Verify every package on NuGet.org.
14. Verify symbols.
15. Install packages in an empty project.
16. Publish compatibility notes.

---

## 29. Risks

### Experimental Microsoft.Extensions.AI APIs

Mitigation:

- Isolate adapters
- Centralize versions
- Contract tests
- Do not leak MEAI into DSP packages

### TTS is not truly progressive

Mitigation:

- Support complete-audio fallback
- Segment LLM text
- Distinguish progressive generation from chunked delivery

### Whisper is not a native streaming transducer

Mitigation:

- Rolling-window provisional transcription
- Clearly label provisional text
- Keep STT pluggable
- Add Parakeet or another streaming ASR later

### Platform-specific audio capture

Mitigation:

- Keep capture outside core
- Start with NAudio
- Add Linux/macOS packages later

### Large model downloads

Mitigation:

- Revision pinning
- Manifests
- Checksums
- Resume
- Atomic finalization

### Stale output after interruption

Mitigation:

- Turn IDs
- Generation IDs
- Linked cancellation
- Output clearing
- Sequence validation

---

## 30. Repository rules

- Production code under `/src`
- Tests under `/tests`
- Benchmarks under `/benchmarks`
- Docs under `/docs`, except root README and LICENSE
- Generated images use ElBruno CLI tools
- Nullable enabled
- Central package management
- Warnings as errors
- File-scoped namespaces
- Immutable records for messages
- Async APIs
- Public async APIs accept `CancellationToken`
- No static mutable state
- No service locator
- No content logging by default
- Every channel bounded
- Native resources disposable
- No secrets in samples
- No model files in Git
- No large audio fixtures
- Consistent PR titles for changelog automation

---

## 31. Copilot kickoff prompt

Copy this PRD to `docs/PRD.md`, then use:

```text
You are implementing the new repository ElBruno.Speech.

Read docs/PRD.md completely before writing code.

Start only with Phase 0 and Phase 1. Do not implement VAD, STT, LLM, TTS, WebSockets, or microphone support yet.

Requirements:
- Create a .NET solution targeting net8.0 and net10.0.
- Use a .slnx solution file.
- Put all production code under src.
- Put all tests under tests.
- Put all documentation under docs except README.md and LICENSE.
- Add Directory.Build.props and Directory.Packages.props.
- Enable nullable reference types, deterministic builds, XML docs, Source Link, symbols, and warnings as errors.
- Create:
  - ElBruno.Speech.Abstractions
  - ElBruno.Speech.Audio
  - ElBruno.Speech.Audio.Tests
  - samples/FileToSpeech
- Implement:
  - AudioFormat
  - AudioSampleFormat
  - AudioFrame
  - WAV PCM and Float32 reader/writer
  - PCM16 to/from Float32
  - stereo-to-mono conversion
  - streaming frame splitter
  - bounded ring buffer with pre-roll
  - duration calculations
- Public async APIs accept CancellationToken.
- Use pooled buffers where useful and document ownership.
- Add tests for malformed WAV, empty files, mono/stereo, formats, framing, ring-buffer wraparound, cancellation, and disposal.
- Add CI for Windows and Ubuntu.
- Add publish.yml using NuGet OIDC Trusted Publishing, but do not publish.
- Add docs/publishing.md using repository ElBruno.Speech, workflow publish.yml, and environment release.
- Do not add Python.
- Do not add NAudio to core.
- Do not download models in unit tests.
- Do not leave TODO-only implementations.
- Build and run tests before finishing.
- Summarize files, API decisions, test results, and deviations.
```

---

## 32. References

### Source project

- https://github.com/huggingface/speech-to-speech
- https://github.com/huggingface/speech-to-speech/blob/main/README.md
- https://github.com/huggingface/speech-to-speech/blob/main/src/speech_to_speech/baseHandler.py
- https://github.com/huggingface/speech-to-speech/blob/main/src/speech_to_speech/s2s_pipeline.py
- https://github.com/huggingface/speech-to-speech/blob/main/src/speech_to_speech/VAD/vad_handler.py
- https://github.com/huggingface/speech-to-speech/blob/main/src/speech_to_speech/VAD/vad_iterator.py
- https://github.com/huggingface/speech-to-speech/tree/main/tests

### ElBruno repositories

- https://github.com/elbruno/ElBruno.Whisper
- https://github.com/elbruno/ElBruno.LocalLLMs
- https://github.com/elbruno/ElBruno.QwenTTS
- https://github.com/elbruno/ElBruno.VibeVoiceTTS
- https://github.com/elbruno/ElBruno.HuggingFace.Downloader

### Microsoft.Extensions.AI

- https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions
- https://www.nuget.org/packages/Microsoft.Extensions.AI
- https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.ispeechtotextclient
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.itexttospeechclient
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.irealtimeclient

### Runtime and audio

- https://onnxruntime.ai/docs/get-started/with-csharp.html
- https://github.com/snakers4/silero-vad
- https://github.com/naudio/NAudio
- https://github.com/ar1st0crat/NWaves

### Publishing

- https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing
- https://devblogs.microsoft.com/dotnet/enhanced-security-is-here-with-the-new-trust-publishing-on-nuget-org/
- https://github.com/elbruno/ElBruno.LocalLLMs/blob/main/docs/publishing.md
- https://github.com/elbruno/ElBruno.Whisper/blob/main/docs/publishing.md
