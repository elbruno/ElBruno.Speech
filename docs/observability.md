# Observability

> **Status:** `ElBruno.Speech.OpenTelemetry` is being implemented by Apoc concurrently with Phase 7. This document describes the planned observability surface.

---

## Package

```
ElBruno.Speech.OpenTelemetry
```

Targets `net8.0;net10.0`. Depends on `ElBruno.Speech.Pipeline` + `OpenTelemetry` 1.13.0.

---

## Meter

**Meter name:** `ElBruno.Speech`

| Metric | Instrument | Unit | Description |
|--------|-----------|------|-------------|
| `elbruno.speech.utterances` | Counter | `{utterances}` | Total utterances processed |
| `elbruno.speech.stt.duration` | Histogram | `ms` | STT latency per utterance |
| `elbruno.speech.llm.duration` | Histogram | `ms` | LLM latency per transcript |
| `elbruno.speech.tts.duration` | Histogram | `ms` | TTS latency per response |
| `elbruno.speech.pipeline.errors` | Counter | `{errors}` | Provider errors (isolated, non-fatal) |
| `elbruno.speech.audio.frames_dropped` | Counter | `{frames}` | Frames dropped due to channel overflow |

---

## Activity Source

**Activity source name:** `ElBruno.Speech`

| Activity | Description |
|----------|-------------|
| `pipeline.utterance` | Span covering STT → LLM → TTS for one utterance |
| `stt.recognize` | Individual STT provider call |
| `llm.respond` | Individual LLM provider call |
| `tts.synthesize` | Individual TTS segment synthesis |

---

## DI Registration

```csharp
services.AddSpeechPipelineTelemetry(); // registers meter + activity source + decorators
```

With OpenTelemetry SDK:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("ElBruno.Speech"))
    .WithTracing(t => t.AddSource("ElBruno.Speech"));
```

---

## Notes

- Telemetry is injected as **decorators** around the MEAI provider interfaces — no changes to `DefaultSpeechPipeline` internals.
- All metrics use the `elbruno.speech.*` namespace to avoid conflicts.
- This page will be updated once `ElBruno.Speech.OpenTelemetry` is complete.
