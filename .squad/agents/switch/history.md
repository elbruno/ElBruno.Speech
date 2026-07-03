# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.Speech — a reusable, local-first speech runtime for .NET 8/10
- **Stack:** C#, .NET 8/10, xUnit, BenchmarkDotNet, fake providers, deterministic WAV fixtures
- **Created:** 2026-07-03

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- All tests use fake providers and prerecorded WAV fixtures — no real model downloads in CI
- Model download and GPU tests tagged [Trait("Category", "Integration")] and excluded from default CI run
- Conformance tests validate ISpeechToTextClient, IChatClient, ITextToSpeechClient contracts with fakes
- Must cover VAD state transitions: Silence→Speech, Speech→Silence, SpeechStart, SpeechEnd, Reset
- Barge-in test pattern: inject speech VAD event during TTS → verify ResponseCancelledUpdate + clean state
- Stale-input test: inject old-turn frames after new turn starts → verify rejection by generation ID
