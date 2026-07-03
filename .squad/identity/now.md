---
updated_at: 2026-07-03T13:24:00Z
focus_area: Phase 1 — Audio Primitives
active_issues: []
---

# What We're Focused On

**Phase 1: Audio Primitives** — implementing `ElBruno.Speech.Audio`.

Phase 0 is complete (solution scaffold, Abstractions foundation types, CI/CD, fake providers, build/test/pack all green). See `docs/PLAN.md` for the full 8-phase roadmap.

## Next task

Implement `src/ElBruno.Speech.Audio/`:
- Replace `AudioPlaceholder.cs` with: `WavReader`, `WavWriter`, `PcmConverter`, `MonoConverter`, `AudioResampler`, `AudioFramer`, `AudioRingBuffer`, `PooledAudioBuffer`, `FileAudioInput`, `WavAudioOutput`, `MemoryAudioInput`, `NullAudioOutput`
- Also add to `ElBruno.Speech.Abstractions`: `IVadClient`, `ISpeechPipeline`, `SpeechPipelineOptions`
- Write tests in `tests/ElBruno.Speech.Audio.Tests/` with golden WAV fixtures in `tests/fixtures/`

**Agent to pick up this work:** Tank (Audio/VAD Engineer)

