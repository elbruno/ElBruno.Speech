# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.Speech — a reusable, local-first speech runtime for .NET 8/10
- **Stack:** C#, .NET 8/10, System.Threading.Channels, Microsoft.Extensions.AI, async iterators, BackgroundService
- **Created:** 2026-07-03

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- All channels are bounded; FullMode.DropOldest for audio, backpressure for text/output
- Barge-in uses generation IDs to reject stale updates across turn boundaries
- Text segmentation flushes at: sentence punctuation, newline, max chars (220), flush timeout (350ms), end of response
- Tool-call JSON must never be sent to TTS unless explicitly enabled
- Cancellation tokens must propagate end-to-end — no fire-and-forget tasks
