# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.Speech — a reusable, local-first speech runtime for .NET 8/10
- **Stack:** C#, .NET 8/10, ASP.NET Core, WebSockets, OpenTelemetry, NAudio, System.CommandLine
- **Created:** 2026-07-03

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- NAudio dependency isolated to ElBruno.Speech.NAudio — core packages remain platform-neutral
- WebSocket protocol: binary frames for PCM audio, JSON frames for session events
- OIDC Trusted Publishing via GitHub Actions — no stored NuGet API tokens
- Samples are the integration test: FileToSpeech, LocalVoiceAgent, WebSocketVoiceAgent, AspireVoiceAgent
- OpenTelemetry follows ASP.NET Core semantic conventions; activities cover every pipeline stage
- CLI tool: elbrunospeech — devices, transcribe, vad, talk, serve, benchmark
