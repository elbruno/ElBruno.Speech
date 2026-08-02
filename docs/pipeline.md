# DefaultSpeechPipeline — Design Reference

`DefaultSpeechPipeline` implements `ISpeechPipeline` using five concurrent `System.Threading.Channels` stages connected in a linear producer–consumer chain.

---

## The 5 Stages

| Stage | Task | Input | Output |
|-------|------|-------|--------|
| 1 — Audio Producer | `ProduceAudioAsync` | `IAudioInput.ReadFramesAsync()` | `Channel<AudioFrame>` |
| 2 — VAD | `RunVadStageAsync` | `Channel<AudioFrame>` | `Channel<byte[]>` (utterance PCM) |
| 3 — STT | `RunSttStageAsync` | `Channel<byte[]>` | `Channel<string>` (transcript) |
| 4 — LLM | `RunLlmStageAsync` | `Channel<string>` | `Channel<string>` (response) |
| 5 — TTS | `RunTtsStageAsync` | `Channel<string>` | `IAudioOutput.WriteAsync()` |

All five run concurrently via `Task.WhenAll`. The pipeline returns when all stages complete.

---

## Bounded Channels

```
IAudioInput ──► [AudioFrameChannel 64/DropOldest] ──► VAD
                                                        │
                                               [UtteranceChannel 4/Wait]
                                                        │
                                                       STT ──► [TranscriptChannel 4/Wait]
                                                                         │
                                                                        LLM ──► [ResponseChannel 8/Wait]
                                                                                         │
                                                                                        TTS ──► IAudioOutput
```

- **Audio channel** drops old frames on overflow to prevent microphone stall.
- **Downstream channels** use `Wait` — back-pressure is acceptable since utterances are rare.

---

## Generation IDs and Barge-In

```csharp
int genId = Interlocked.Increment(ref _generationId);
// ... foreach segment in TextSegmenter.Segment(text):
if (genId != _generationId) break; // newer LLM response arrived → abort
```

Each LLM response increments `_generationId`. The TTS stage checks `genId == _generationId` before synthesising each text segment. A barge-in (new speech detected while TTS is playing) causes the LLM stage to emit a new response, incrementing the generation ID and stopping the stale TTS loop.

---

## VAD Pass-Through Mode

When no `IVadClient` is injected (constructor parameter `vad: null`), the VAD stage treats **all frames as speech** and flushes them as a single utterance when:

- `frame.IsFinal == true` — end-of-stream signal from `MemoryAudioInput`
- The channel reader completes (producer finished)

This enables testing without a real VAD model and supports file-based pipelines.

---

## Error Isolation

Each provider call is wrapped in a `try/catch`:

```csharp
catch (OperationCanceledException) when (token.IsCancellationRequested)
    throw;  // propagate pipeline cancellation
catch (Exception ex)
    _logger.LogWarning(ex, "Provider threw — skipping.");
    continue;  // next utterance / transcript / segment
```

A single bad utterance will not crash the session.

### STT Timeout

The STT stage adds a 30-second per-utterance timeout via `Task.WhenAny`:

```csharp
var completed = await Task.WhenAny(sttTask, Task.Delay(SttTimeout, token));
if (completed != sttTask)
    // log warning, skip utterance
```

---

## DI Registration

`AddSpeechPipeline()` registers:

- `DefaultSpeechPipeline` → `ISpeechPipeline` as **transient**
- `SpeechPipelineOptions` as **singleton** (from configuration or default)

```csharp
services.AddSpeechPipeline();
// or with custom options:
services.AddSpeechPipeline(opts => opts with { FrameDurationMs = 30 });
```

---

## TextSegmenter

`TextSegmenter.Segment(text)` splits long LLM responses at sentence boundaries before TTS synthesis, enabling lower latency (TTS starts before the full response is generated). Texts under 50 characters are returned as-is (no split).
