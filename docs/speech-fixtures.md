# Reusable speech test fixtures

The version 1.0.0 bundle in `tests/fixtures/speech/v1.0.0` contains ten complete
English (`en_us`) and ten complete Spanish (`es_419`) FLEURS test recordings.
Its uncompressed size, including references and notices, is 6,236,789 bytes,
below the enforced 20 MiB cap. No model weights are included.

## License and provenance

Audio and source transcript references are **CC BY 4.0**, separate from the
library code's MIT license. Commercial reuse and redistribution are permitted
under that license with attribution, a license link, and notices of changes.
Keep `ATTRIBUTION.md`, `LICENSE.txt`, `README.md`, and the manifest when reusing
the bundle. The source authors and speakers do not endorse this project.

Source: [google/fleurs](https://huggingface.co/datasets/google/fleurs) at revision
`70bb2e84b976b7e960aa89f1c648e09c59f894dd`, test splits only.
The manifest records metadata-file hashes, original WAV hashes, archive members,
converted WAV hashes, and the exact supplied raw and normalized transcript strings.
FLEURS uses source sentences validated against recordings by additional workers;
these are not model-generated hypotheses.

Selection is fixed independently of model outputs: take the first ten distinct
sentence IDs in each pinned test archive's order with 32,000-240,000 samples
(2-15 seconds). Keep complete clips, including source silence. There is no
trimming, concatenation, or resampling. Float32 source samples are converted with
`round(sample * 32768)` (ties to even), clamped to the PCM16 range, and placed in
canonical 44-byte-header WAV files. No demographic fields are copied.

This is a small read-speech regression collection, not a representative benchmark.
Independent references do not establish that a model never encountered the data
in training. There are **no reviewed word timestamps**. These fixtures do not
replace the outstanding independently reviewed podcast comparison in issue #101.

## Portable manifest

The bundle is WAV plus UTF-8 JSON, independent of NuGet, .NET, and speech providers.
Schema version `1` and bundle version `1.0.0` are distinct. Published payloads
are immutable: changed data or references require a new bundle version.

Top-level metadata includes dataset/revision, license, selection, source metadata
hashes, notice hashes, and `reviewedWordTimestamps: false`. Each `fixtures` entry
has:

| Field | Meaning |
|---|---|
| `id`, `language` | Stable recording ID and FLEURS configuration |
| `path`, `sha256` | Portable relative audio path and delivered file digest |
| `sampleRate`, `channels`, `sampleFormat` | 16000, 1, `pcm_s16le` |
| `sampleCount`, `durationSeconds` | Complete recording length |
| `rawTranscript`, `transcript` | Unmodified source raw and normalized references |
| `sourceId`, `sourceFilename`, `sourceSha256` | Source sentence ID, recording name, original WAV digest |
| `sourceUrl`, `sourceMember`, `transformation` | Pinned archive, member, and conversion provenance |

`sourceId` identifies a sentence, not necessarily a unique recording. The stable
fixture ID includes the recording filename. Paths in JSON use `/` for portability;
join their components using your platform's filesystem API.

## Offline checks and reproducibility

Run from the repository root, using Python 3.11+ and the repository's .NET SDK:

```powershell
python tools\speech-fixtures\fixtures.py verify
python -m unittest discover -s tools\speech-fixtures -p "test_*.py"
dotnet test tests\ElBruno.Speech.Fixtures.Tests --configuration Release
```

These commands need no models, credentials, package-specific speech provider, or
dataset access after normal .NET restore. CI runs offline fixture/scorer checks on
Windows and Ubuntu for .NET 8 and .NET 10. Validation includes counts, distinct
sentences, size, provenance, safe paths, exact file inventory, hashes, WAV headers,
and malformed/corrupt input cases.

To independently reproduce the payload, copy only the committed bundle's
`README.md` and `ATTRIBUTION.md` into a new empty output directory, then run:

```powershell
python tools\speech-fixtures\fixtures.py prepare --bundle C:\temp\fleurs-reproduced
python tools\speech-fixtures\fixtures.py verify --bundle C:\temp\fleurs-reproduced
```

The command reads the pinned TSV metadata and only a bounded prefix of each test
archive, stopping after selection. It does not download the hundreds-of-MiB
archives in full. Compressed reads are capped at 32 MiB per language. No Python
dependencies or upstream executable dataset scripts are used. Compare every
resulting file hash with the committed version before accepting a reproduction.
Preparation refuses an existing manifest. Network failures are reported, not
replaced by synthetic audio or model references.

## Real-model evaluation

For a fixed-reference INT8/FP32 Parakeet versus Whisper large-v3 CPU comparison,
see the [benchmark commands and measurement limits](../tools/speech-benchmark/README.md).
The [recorded comparison](speech-benchmark.md) preserves all hypotheses and
explains why a larger or higher-precision model is not automatically better.
The separate [Whisper provider](whisper.md) uses this same immutable bundle and
scorer. Its real-model evaluations are opt-in, not ordinary CI downloads.

Explicit local invocation:

```powershell
$env:PARAKEET_MODEL_DIRECTORY = 'C:\models\parakeet'
$env:SPEECH_FIXTURES_REPORT_DIRECTORY = 'C:\temp\fleurs-reports'
dotnet test tests\ElBruno.Speech.Stt.Parakeet.Tests --configuration Release --filter "Category=SpeechFixtureEvaluation"
python tools\speech-fixtures\check_report.py C:\temp\fleurs-reports\fleurs-10.json
```

This invocation explicitly downloads the pinned model when absent and verifies
all cached assets when present. It loads a client once per framework, evaluates all
twenty clips, and writes a report per framework. Existing Python/.NET
implementation-parity tests remain separate and unchanged.

The manual **Speech fixture evaluation and bundle** workflow uses an Ubuntu 24.04
CPU runner with .NET 10, immutable model/cache identifiers, hash verification,
complete-coverage checks, and uploaded JSON/TRX reports. Ordinary pull requests
never download the model. Reports preserve per-clip hypotheses and scores, model
and dataset revisions, framework/ONNX versions, language/corpus totals, timing,
coverage, and threshold status. Partial inference runs write partial reports and
fail; absent reports also fail the workflow.

### Scoring policy and baseline

**WER means word error rate, not an accuracy score: lower is better, and 0% is
perfect.** It counts substitutions (wrong words), deletions (missing words), and
insertions (extra words), divided by the number of reference words:

```text
WER = (substitutions + deletions + insertions) / reference words
```

English WER of 5.66% means 12 word edits over 212 reference words, approximately
six edits per 100 words. Spanish WER of 3.37% means seven edits over 208 reference
words, approximately three edits per 100 words. Neither means that only 5.66% or
3.37% of words were recognized correctly. Do not label `100% - WER` as accuracy:
insertions also count, and WER can exceed 100%. CER is the analogous character
error rate, also lower-is-better.

There is no universal good/bad cutoff. A wrong name, amount, or instruction can
matter more than several spelling differences. These twenty short read-speech
clips provide preliminary regression evidence, not production acceptance.
One extra word edit changes the English score by about 0.47 percentage points
and the Spanish score by about 0.48 percentage points. The lower Spanish value
does not establish that the model is generally better at Spanish.

Policy `unicode-nfc-lower-punctuation-space-v1` applies NFC normalization and
invariant lowercase, retains Unicode letters/numbers/combining marks (including
Spanish accents), replaces other characters with word separators, and collapses
whitespace. Apostrophes and hyphens separate words rather than joining them.
WER operates on words. CER operates on Unicode scalar values excluding spaces.
Raw references remain unchanged. Levenshtein ties prefer substitution, then
deletion, then insertion. Corpus rates divide summed errors by summed reference
lengths, not mean per-clip rates. Empty-reference rates are undefined (`null`),
with insertion counts retained; fixture references must be nonempty.

Initial Windows x64 CPU results with pinned Parakeet and ONNX Runtime 1.31.0 were
identical on .NET 8 and .NET 10:

| Language | WER | CER | Proposed WER limit | Proposed CER limit |
|---|---|---|---|---|
| English | 12/212 = 5.66% | 22/1007 = 2.18% | 8% | 5% |
| Spanish | 7/208 = 3.37% | 11/1030 = 1.07% | 6% | 4% |
| Corpus | 19/420 = 4.52% | 33/2037 = 1.62% | Not gated separately | Not gated separately |

`FleursRegression.json` pins the manifest digest, model revision, policy, and
per-language limits. Limits add two absolute percentage points to the baseline,
then round upward to the next whole percentage point. They are **proposed**,
not an accepted accuracy specification. Until review includes Linux evidence,
tests verify complete inference/reporting and record comparisons but do not
enforce these proposed accuracy limits. Reviewers can approve the configuration
through a normal code review; only `status: approved` enables the accuracy gate.
No automatic baseline update occurs.

For an intentional new baseline only, `SPEECH_FIXTURES_RECORD_BASELINE=1` records
evidence without loading thresholds. The report checker rejects baseline-recording
reports as configured regression runs, and CI never sets this variable. Do not
alter selection/references or regenerate limits just to hide a regression.

Native Parakeet metadata contains token starts only. Word-end, segment, and
reviewed timestamp accuracy are unsupported and are explicitly not scored.
Whisper and other provider adapters can consume the same references later.

### Improving quality without confusing the measurements

First inspect per-clip references and hypotheses. The initial outputs include
`splendours` versus `splendors` (a spelling variant), `3:2` versus `free to two`
(number/word representation plus a possible recognition error), and differences
in proper names and omitted words. These are text comparisons, not adjudicated
audio findings. Listen before deciding which are genuine recognition errors.
Keep the original references and primary scoring policy unchanged. Any additional
spelling/number-normalized metric must have its own documented policy and be
reported alongside, never substituted to make the baseline look better.

Keep this frozen bundle for fast regression tests. Use a separate, larger
evaluation set and independently reviewed podcast excerpts for model selection.
Compare on the same audio and scoring policy, recording WER/CER, names/numbers,
silence hallucinations, inference time, model size, and peak memory. Do not tune
models or thresholds on the twenty regression clips and claim held-out accuracy.

As checked on 2026-10-22, the official model catalogue still lists
[Parakeet TDT 0.6B v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3)
as a Spanish/English multilingual option. A newer Parakeet family model,
[Parakeet Unified English 0.6B](https://huggingface.co/nvidia/parakeet-unified-en-0.6b),
targets English, not a drop-in multilingual upgrade, and has different licensing.
No newer suitable multilingual Parakeet successor was identified in that search.

Candidate experiments, not implemented provider changes:

| Candidate | Reason to evaluate | Constraint |
|---|---|---|
| Existing Parakeet v3, FP32 versus current INT8 | Isolate any quantization-related quality loss before replacing the architecture | Larger assets/memory; verify preprocessing, source checkpoint, decoder and runtime parity; improvement is not guaranteed |
| [Whisper large-v3](https://huggingface.co/openai/whisper-large-v3) | Independent multilingual architecture and an accuracy-oriented baseline | Measure actual CPU/GPU latency and memory; requires a separately validated adapter/runtime |
| [Whisper large-v3-turbo](https://huggingface.co/openai/whisper-large-v3-turbo) | Faster multilingual Whisper comparison | Official card describes a speed/quality tradeoff, not guaranteed better accuracy |
| [Canary 1B v2](https://huggingface.co/nvidia/canary-1b-v2) | NVIDIA multilingual alternative with Spanish support and CC BY 4.0 weights | Different encoder-decoder/tokenizer/runtime integration; not a filename substitution in the TDT decoder |

The official full-FLEURS model cards report Parakeet v3 WER of 4.85% English
and 3.45% Spanish, and Canary v2 WER of 4.50% English and 2.90% Spanish.
These vendor-reported figures motivate a comparison, not a reproduced result:
their evaluation scope/runtime/scoring differs from our twenty-clip INT8 run.
Do not compare the vendor percentages directly against our subset or promise that
Canary will improve our recordings.

For long recordings, test pause-aware chunk boundaries and appropriate context
separately from model selection. Preserve complete coverage and original timeline
offsets, and measure missing/duplicated words. Denoising or VAD can remove speech;
neither should become a default just because it improves one example. Avoid an
LLM rewrite of hypotheses as an apparent recognition improvement, especially for
names, numbers, or instructions.

## Versioned archive and other repositories

Create a deterministic archive locally:

```powershell
python tools\speech-fixtures\fixtures.py pack --output C:\temp\speech-fixture-archive
```

The resulting `speech-fixtures-v1.0.0.zip` and `.zip.sha256` contain only the
validated bundle, not models, hypotheses, or repository implementation code.
ZIP members use fixed timestamps and permissions and stored compression, avoiding
zlib-version differences. Existing archives are never overwritten.

Version 1.0.0 ZIP SHA-256:
`71149f3516608015cc7eed046305ba9a0c630a6aa1462ef1e0cb397b39c0c414`.
The manifest SHA-256 is
`14940a5d06617d5c79ee5c050cdaa5a0e249e03af55fbd40e52791ff2adad2e1`.
Git attributes disable line-ending conversion for the immutable bundle so these
digests remain identical on Windows and Linux.

Publication is a separate reviewed operation. Run the manual workflow on `main`
with `publish_bundle: true` after merging the reviewed implementation. It creates
the `speech-fixtures-v1.0.0` release, uploads ZIP/checksum assets, downloads them
again, and verifies the archive digest. The NuGet publishing workflow excludes
the `speech-fixtures-` namespace. A fixture release neither requires nor publishes
a new NuGet package. Default manual runs upload workflow artifacts only.
Ordinary CI also packages and verifies the ZIP as a downloadable artifact, without
running model inference or publishing a release.

Once published, other ElBruno repositories can pin the asset version and the
reviewed ZIP digest in their own test tooling:

```powershell
$version = '1.0.0'
$destination = 'C:\temp\elbruno-fixtures'
gh release download "speech-fixtures-v$version" --repo elbruno/ElBruno.Speech --dir $destination
$archive = Join-Path $destination "speech-fixtures-v$version.zip"
$expected = '71149f3516608015cc7eed046305ba9a0c630a6aa1462ef1e0cb397b39c0c414'
$sidecar = ((Get-Content "$archive.sha256" -Raw).Trim() -split '\s+')[0]
$actual = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sidecar -ne $expected -or $actual -ne $expected) { throw 'Fixture archive checksum mismatch' }
Expand-Archive $archive -DestinationPath (Join-Path $destination 'bundle')
```

The sidecar detects transfer corruption; pin its reviewed digest independently in
consumer configuration to detect changed release assets. After extraction, check
the manifest's file hashes before reading references. A consumer need not install
ElBruno.Speech, its .NET test helper, or Python to read WAV/JSON. Keep attribution
and license notices with any copied fixtures. No other repositories are modified
by this implementation.
