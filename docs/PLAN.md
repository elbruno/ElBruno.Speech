# ElBruno.Speech — Implementation Plan

> **Status as of 2026-07-03:** Phases 0–7 complete. 1.0.0 sign-off checklist in Phase 7 section.
> **v1.0.0 released 2026-08-02** — All 8 packages published to NuGet.org. GitHub release: https://github.com/elbruno/ElBruno.Speech/releases/tag/v1.0.0

The full specification lives in [`docs/PRD.md`](PRD.md). This file tracks phase-by-phase progress.

---

## Phase 0 — Repository Foundation ✅ DONE

| Task | Status |
|------|--------|
| Solution scaffold (`.sln`, all `.csproj`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`) | ✅ done |
| `ElBruno.Speech.Abstractions` — foundation types (AudioFrame, AudioFormat, AudioSampleFormat, AudioTransport, SpeechPipelineException hierarchy, SpeechProviderCapabilities) | ✅ done |
| `tests/ElBruno.Speech.TestUtils` — FakeSpeechToTextClient, FakeChatClient, FakeTextToSpeechClient | ✅ done |
| `.github/workflows/ci.yml` — build + test, .NET 8 & 10 | ✅ done |
| `.github/workflows/publish.yml` — OIDC Trusted Publishing to NuGet | ✅ done |
| `.github/workflows/codeql.yml` | ✅ done |
| `.github/dependabot.yml` | ✅ done |
| README.md, LICENSE (MIT) | ✅ done |
| `dotnet build / test / pack` all green | ✅ done |

**Exit criteria met:** clean build, all placeholder tests pass, all 8 packages pack.

---

## Phase 1 — Audio Primitives ✅ DONE

**Owner:** Tank (Audio/VAD engineer)

### Abstractions additions
- `IVadClient` — voice activity detection contract
- `ISpeechPipeline` — full pipeline contract
- `SpeechPipelineOptions` — configuration record

### `ElBruno.Speech.Audio` implementation
Replace `AudioPlaceholder.cs` stub with:
- `WavReader` / `WavWriter` — read/write PCM WAV files (16-bit, mono, 16 kHz)
- `PcmConverter` — Int16 ↔ Float32 sample conversion
- `MonoConverter` — stereo → mono downmix
- `AudioResampler` — resample from 8/22.05/24/44.1/48 kHz → 16 kHz
- `AudioFramer` — chop a stream into fixed-size frames (default 20 ms / 320 samples)
- `AudioRingBuffer` — lock-free ring buffer backed by `ArrayPool<byte>`
- `PooledAudioBuffer` — `IDisposable` wrapper for pooled audio memory
- `FileAudioInput` / `WavAudioOutput` — file-backed IAudioInput / IAudioOutput
- `MemoryAudioInput` / `NullAudioOutput` — in-memory / sink implementations

### Tests (`tests/ElBruno.Speech.Audio.Tests`)
- Unit tests for every converter and framer
- Golden WAV fixtures committed to `tests/fixtures/`
- Round-trip encode/decode assertions

### Benchmarks baseline
- `AudioFramerBenchmark` and `ResamplerBenchmark` using BenchmarkDotNet

---

## Phase 2 — Voice Activity Detection ✅ DONE

**Owner:** Tank

- `ElBruno.Speech.Vad.Silero` — ONNX Runtime + Silero VAD v4 model
  - `SileroVadClient : IVadClient`
  - `VadOptions` — threshold, min-silence-ms, min-speech-ms
  - Frame buffer management, model inference
- Model download: Silero VAD ONNX (`silero_vad.onnx` to `~/.cache/elbruno-speech/models/`)
- Tests: frame classification, sensitivity thresholds

---

## Phase 3 — Pipeline + FileToSpeech Sample ✅ DONE

**Owner:** Trinity (Pipeline engineer)

- `ElBruno.Speech.Pipeline` — `DefaultSpeechPipeline : ISpeechPipeline`
  - Channels-based VAD → STT → LLM → TTS backpressure pipeline
  - `BoundedChannel<AudioFrame>` for each stage
  - DI extension: `AddSpeechPipeline()`
- `src/samples/FileToSpeech/` — read a WAV file → transcribe → respond → speak

---

## Phase 4 — NAudio + LocalVoiceAgent Sample ✅ DONE

**Owner:** Tank / Trinity

- `ElBruno.Speech.NAudio` — real microphone input and speaker output via NAudio
  - `NAudioMicrophoneInput : IAudioInput`
  - `NAudioSpeakerOutput : IAudioOutput`
- Streaming + barge-in: interrupt TTS when new speech detected
- `src/samples/LocalVoiceAgent/` — full local voice loop

---

## Phase 5 — ASP.NET Core + WebSocket Sample ✅ DONE

**Owner:** Apoc (Platform engineer)

- `ElBruno.Speech.AspNetCore` — WebSocket voice hub
  - `SpeechHub` (SignalR-style endpoint)
  - `UseSpeechWebSocket()` extension
  - `AddSpeechPipelineAspNetCore()` DI
- `src/samples/WebSocketVoiceAgent/` — browser ↔ server voice over WebSocket

---

## Phase 6 — MEAI IRealtimeClient Adapter ✅ DONE

**Owner:** Trinity

- Evaluate `IRealtimeClient` maturity in MEAI 10.x
- If stable: `SpeechPipelineRealtimeAdapter` wrapping the pipeline as `IRealtimeClient`
- If not stable: stub + issue filed for future release

---

## Phase 7 — Production Hardening & 1.0.0 ✅ DONE

**Owner:** Morpheus (Lead) + Apoc

- `ElBruno.Speech.OpenTelemetry` — metrics, traces, logs via OTEL (Apoc)
- `ElBruno.Speech.Cli` — `elbrunospeech` dotnet tool (Apoc)
- `src/samples/AspireVoiceAgent/` — Aspire AppHost orchestrating the full stack (Apoc)
- **Conformance tests** (`tests/ElBruno.Speech.Conformance.Tests/`) — 22 tests, 0 failed ✅
- **Architecture docs** — `docs/architecture.md`, `docs/pipeline.md`, `docs/audio-formats.md`, `docs/observability.md` ✅
- **Production hardening** — STT/LLM/TTS error isolation + 30s STT timeout in `DefaultSpeechPipeline` ✅
- 1.0.0 sign-off (see checklist below) ✅

### 1.0.0 Acceptance Criteria

| Criterion | Status |
|-----------|--------|
| All packages build on net8.0 and net10.0 | ✅ |
| All unit tests pass (no failures) | ✅ Audio: 19, VAD: 9+1skip, Pipeline: 15, AspNetCore: 5, Conformance: 22 — zero failures |
| Integration tests skip cleanly in CI (model-absent) | ✅ |
| All 6 Morpheus-owned packages pack without error | ✅ Abstractions, Audio, Vad.Silero, Pipeline, AspNetCore, NAudio |
| OpenTelemetry and Cli packages pack | ✅ |
| No circular package references | ✅ |
| Public API documented (XML doc on all public types) | ✅ |
| Conformance tests verify provider contracts | ✅ 22 tests across 5 suites |
| Architecture docs written | ✅ architecture.md, pipeline.md, audio-formats.md, observability.md |
| FileToSpeech sample builds and runs | ✅ |
| LocalVoiceAgent sample builds | ✅ |
| WebSocketVoiceAgent sample builds | ✅ |
| AspireVoiceAgent sample builds | ✅ |
| CLI tool builds and packs as dotnet tool | ✅ |
| DefaultSpeechPipeline resilience (error isolation + STT timeout) | ✅ |

---

## Key Technical Constraints

| Constraint | Value |
|-----------|-------|
| Target frameworks | `net8.0;net10.0` |
| MEAI version | `10.7.0` (stable) |
| Microsoft.Extensions.* | `10.0.9` |
| OpenTelemetry | `1.16.0` |
| ONNX Runtime | `1.27.1` |
| NAudio | `2.3.0` |
| Canonical audio format | 16 kHz, mono, Int16 PCM |
| MEAI001 suppression | global (speech APIs are `[Experimental]`) |
| Package management | Central (`Directory.Packages.props`) — no `Version` on `PackageReference` |

## Team Roster

| Name | Role | Domain |
|------|------|--------|
| Morpheus | Lead/Architect | Scope, decisions, code review |
| Trinity | Pipeline Engineer | Pipeline, channels, MEAI adapters |
| Tank | Audio/VAD Engineer | Audio I/O, PCM, VAD, ONNX |
| Apoc | Platform Engineer | ASP.NET Core, DI, OTel, CLI |
| Switch | QA/Tester | Tests, conformance, golden fixtures |
| Scribe | Session Logger | Memory, decisions, logs |
| Ralph | Work Monitor | Backlog, keep-alive |
| Rai | RAI Reviewer | Safety, ethics |
| Fact Checker | Verifier | Claims, devil's advocate |

See `.squad/team.md` for full details and charters.
