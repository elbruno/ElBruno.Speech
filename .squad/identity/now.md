---
updated_at: 2026-07-03T20:32:00Z
focus_area: v0.5.0 NuGet publish — BLOCKED on NUGET_API_KEY secret
active_issues:
  - nuget-publish-v0.5.0-blocked
---

# What We're Focused On

**Phase 7: Production Hardening & 1.0.0** — ✅ COMPLETE (all agents).

- **Apoc** (Backend Dev): OTel package, CLI tool, AspireVoiceAgent — ✅ COMPLETE
- **Morpheus** (Architect): Conformance tests, architecture docs, pipeline hardening — ✅ COMPLETE

Phases 0–7 complete. See `docs/PLAN.md` for the 1.0.0 sign-off checklist.

## Phase 7 Deliverables (Apoc — done)

- `src/ElBruno.Speech.OpenTelemetry/SpeechPipelineMetrics.cs` — meter + counters/histograms
- `src/ElBruno.Speech.OpenTelemetry/SpeechPipelineActivitySource.cs` — activity source + helpers
- `src/ElBruno.Speech.OpenTelemetry/ServiceCollectionExtensions.cs` — `AddSpeechPipelineTelemetry()` DI extension
- `src/ElBruno.Speech.Cli/Program.cs` — `elbrunospeech` dotnet tool: `devices`, `transcribe`, `vad`, `talk`, `serve`, `bench`
- `src/samples/AspireVoiceAgent/Program.cs` — Aspire-compatible web app with OTel, WebSocket, health checks
- Full solution: 0 errors, 0 warnings on net8.0 and net10.0
- Both packages pack successfully: `ElBruno.Speech.OpenTelemetry.1.0.0.nupkg`, `ElBruno.Speech.Cli.1.0.0.nupkg`
- Decision recorded: `.squad/decisions/inbox/apoc-phase7-otel-cli.md`

## Phase 7 Deliverables (Morpheus — done)

- `tests/ElBruno.Speech.Conformance.Tests/` — 22 conformance tests, 0 failures (net8.0 + net10.0)
- `docs/architecture.md`, `docs/pipeline.md`, `docs/audio-formats.md`, `docs/observability.md`
- `DefaultSpeechPipeline` resilience — STT/LLM/TTS error isolation + 30s STT timeout
- 6 packages pack: Abstractions, Audio, Vad.Silero, Pipeline, AspNetCore, NAudio
- `docs/PLAN.md` updated with 1.0.0 sign-off checklist
- Decision recorded: `.squad/decisions/inbox/morpheus-phase7-architecture.md`

## Full Test Summary (net8.0 + net10.0)

| Suite | Passed | Skipped | Failed |
|-------|--------|---------|--------|
| Audio.Tests | 19 | 0 | 0 |
| Vad.Silero.Tests | 9 | 1 | 0 |
| Pipeline.Tests | 15 | 0 | 0 |
| AspNetCore.Tests | 5 | 0 | 0 |
| Conformance.Tests | 22 | 0 | 0 |
| **Total** | **70** | **1** | **0** |

