# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.Speech — a reusable, local-first speech runtime for .NET 8/10
- **Stack:** C#, .NET 8/10, Microsoft.Extensions.AI, ONNX Runtime, System.Threading.Channels, xUnit, BenchmarkDotNet
- **Created:** 2026-07-03

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- Architecture: all packages built against `ElBruno.Speech.Abstractions` — no circular dependencies allowed
- Pipeline boundary: ISpeechPipeline → ISpeechSession → SpeechSessionUpdate stream
- Audio canonical format: 16 kHz mono PCM16 for STT/VAD
- No model binaries bundled in NuGet; all models downloaded via ElBruno.HuggingFace.Downloader
- Public API contracts defined before implementation; Morpheus owns all interface design
