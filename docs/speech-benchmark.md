# Fixed-reference multilingual CPU comparison

**Whisper large-v3 made fewer word errors on this small bundle, especially in
Spanish, but took about 88 times as long as INT8 Parakeet on this CPU setup.
FP32 Parakeet was not a consistent accuracy improvement.** Existing provider
defaults remain unchanged.

WER and CER are error rates, not success scores: **lower is better; 0% is
perfect**. WER is `(substitutions + deletions + insertions) / reference words`.
It is not interchangeable with `100% - accuracy`, because insertions count.

## Same inputs and scorer

All runs used the immutable FLEURS fixture bundle v1.0.0: ten English and ten
Spanish clips, 193.4 seconds total, 212 English and 208 Spanish reference
words. References, selection, normalization, accents, and scoring policy were
unchanged. Manifest SHA-256:
`14940a5d06617d5c79ee5c050cdaa5a0e249e03af55fbd40e52791ff2adad2e1`.

Models ran in separate sequential processes with four CPU inference threads,
automatic language selection, no gold-language hints, and a full first-clip
warm-up excluded from timed inference. Final Parakeet runs were repeated after
initial build activity ended. Whisper inference was not run concurrently with
another model or a build. Editing and lightweight report/API inspection
continued; this is a shared development host, not a dedicated laboratory.

The host exposed 18 cores / 36 logical processors with an AMD EPYC 74F3 CPU.
Whisper used .NET 10.0.13 on Windows build 26300. Parakeet used Python 3.14.4,
NumPy 2.4.6, and ONNX Runtime 1.31.0 CPU, with four intra-op threads, one inter-op
thread, and sequential execution. Both Parakeet precisions used the same
preprocessor and greedy TDT decoder. Whisper.net 1.9.1 used whisper.cpp CPU,
default greedy sampling, temperature zero, no temperature fallback, and no
context between clips.

This controls the references and scoring, **not the runtime architecture**.
Python/ORT Parakeet and .NET/whisper.cpp Whisper are different implementations.
INT8 scores reproduced the existing .NET Parakeet baseline exactly. Original
exporter checkpoint lineage is not independently established.

## Accuracy observed

| Configuration | English WER | Spanish WER | English CER | Spanish CER |
|---|---:|---:|---:|---:|
| Parakeet v3 INT8 | 12/212 = **5.66%** | 7/208 = **3.37%** | 22/1007 = 2.18% | 11/1030 = 1.07% |
| Parakeet v3 FP32 | 10/212 = **4.72%** | 8/208 = **3.85%** | 23/1007 = 2.28% | 15/1030 = 1.46% |
| Whisper large-v3, unquantized GGML | 11/212 = **5.19%** | 5/208 = **2.40%** | 32/1007 = 3.18% | 6/1030 = 0.58% |

Corpus WER is 19/420 (4.52%) for INT8, 18/420 (4.29%) for FP32, and 16/420
(3.81%) for Whisper. Aggregate counts are summed before dividing.

Whisper's WER advantage over INT8 is **one fewer English edit and two fewer
Spanish edits**, not evidence of a universal or statistically established
improvement. Its English CER is worse. FP32 improves English WER by two edits
but worsens Spanish by one edit and worsens CER in both languages.

Examples from the unchanged reports:

- Whisper retains "a un sacapuntas" where both Parakeet outputs omit or alter
  those words, improving that Spanish clip by two edits.
- Whisper recognizes "Northern Marianas Emergency Management Office" more
  closely than the Parakeet outputs in one English clip.
- Whisper renders "twentieth" as "20th" and "twelve" as "12". The pinned scorer
  intentionally does not equate spelling and numeric formatting. It also
  retains the `splendours`/`splendors` distinction.
- All configurations still differ from references on some names. None of these
  text comparisons has been adjudicated by listening, and no reference was
  changed to favor a model.

## CPU cost observed

| Configuration | Inference for 193.4 s audio | RTF | Peak process MiB | Inference model bytes |
|---|---:|---:|---:|---:|
| Parakeet INT8 | 8.74 s | **0.045** | 933.9 | 670,619,706 |
| Parakeet FP32 | 10.54 s | **0.054** | 2,614.4 | 2,549,945,622 |
| Whisper large-v3 | 773.24 s | **3.998** | 3,697.8 | 3,095,033,483 |

RTF is inference seconds divided by audio seconds. Below one is faster than
real time. Thus Whisper took approximately four seconds of processing per
second of input in this configuration, not four times faster. Whisper encodes
short recordings in padded windows, which is important for this clip workload.

Model-load times were 2.95 s (INT8), 4.71 s (FP32), and 2.68 s (Whisper),
excluding preceding digest validation. Warm-up times were 0.49 s, 0.68 s,
and 38.55 s respectively. These are single-run observations, not cold-disk
benchmarks or latency percentiles. Peak working set includes process/loading/
warm-up memory, not just weights; MiB means 1,048,576 bytes. Parakeet byte totals
cover inference assets, excluding the published bundle's 97-byte config file.

## Recommendation and next evidence

Keep the verified INT8 Parakeet bundle and existing defaults. FP32 is not a
drop-in improvement for Spanish and costs substantially more memory.

Expose [Whisper large-v3 as an explicit alternative](whisper.md), particularly
for offline multilingual evaluation where its measured CPU cost is acceptable.
Do not switch the interactive voice pipeline to this CPU configuration by
default. Do not promise the same tradeoff for another machine, GPU, thread
budget, turbo model, or sampling strategy; those require separately labeled
runs. Canary remains a future candidate, not an integrated or measured model
in this comparison.

Before accepting accuracy gates or closing issue #101, obtain the remaining
independently reviewed podcast references and timestamp evidence. This tiny,
read-speech subset does not measure conversational speech, noise robustness,
code-switching, speaker variation, reviewed alignment, or unseen training data.
Thresholds remain unapproved and no model was retrained, republished, or
silently substituted.

## Reproduce and inspect

The [benchmark instructions](../tools/speech-benchmark/README.md) include exact
commands and immutable download pins. Complete generated reports contain every
reference/hypothesis, edit count, model revision, runtime, and timing:

- [INT8 Parakeet](benchmarks/speech-fixtures-v1/int8.json)
- [FP32 Parakeet](benchmarks/speech-fixtures-v1/fp32.json)
- [Whisper large-v3](benchmarks/speech-fixtures-v1/whisper.json)
- [New Whisper provider integration, including native segments](benchmarks/speech-fixtures-v1/whisper-provider.json)

The provider integration run transcribed all twenty clips on .NET 10. Every
hypothesis and edit count exactly matched the independent Whisper benchmark.
Its per-request cold inference timings are separate evidence, not the warmed
performance measurements above. Native detected languages were English and
Spanish; this is not a language-detection accuracy benchmark.

These reports include source transcript excerpts and model-derived text from
the CC BY 4.0 FLEURS fixtures. Retain the [bundle attribution and license](../tests/fixtures/speech/v1.0.0/ATTRIBUTION.md)
when reusing them. Dataset credit: FLEURS (Conneau et al., 2022), the speech
version of the FLoRes translation benchmark. Dataset authors and speakers do
not endorse these models or results. Model
weights are not included. Parakeet weights are CC BY 4.0; the pinned Whisper
GGML repository declares MIT. No model upload to another account is needed.
