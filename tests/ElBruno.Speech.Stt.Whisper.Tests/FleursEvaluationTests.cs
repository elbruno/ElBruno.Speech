using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ElBruno.Speech.Audio;
using ElBruno.Speech.TestUtils;
using Xunit;

namespace ElBruno.Speech.Stt.Whisper.Tests;

public sealed class FleursEvaluationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ModelDownload")]
    [Trait("Category", "WhisperFixtureEvaluation")]
    public async Task TranscribesEveryIndependentFleursReference()
    {
        string model = Environment.GetEnvironmentVariable("WHISPER_MODEL_PATH")
            ?? throw new InvalidOperationException("Set WHISPER_MODEL_PATH for explicit model download/verification.");
        string output = Environment.GetEnvironmentVariable("SPEECH_FIXTURES_REPORT_DIRECTORY")
            ?? throw new InvalidOperationException("Set SPEECH_FIXTURES_REPORT_DIRECTORY.");
        string directory = Path.Combine(AppContext.BaseDirectory, "speech-fixtures");
        var bundle = SpeechFixtureBundle.LoadAndValidate(directory);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(45));
        await WhisperModel.DownloadAsync(model, timeout.Token);
        var load = Stopwatch.StartNew();
        using var client = new WhisperSpeechToTextClient(new() { ModelPath = model, InputFormat = Pcm16InputFormat.Wav });
        load.Stop();
        var clips = new List<Clip>();
        bool complete = false;
        Directory.CreateDirectory(output);
        try
        {
            foreach (var fixture in bundle.Fixtures)
            {
                await using var audio = File.OpenRead(SpeechFixtureBundle.ResolvePath(directory, fixture.Path));
                var elapsed = Stopwatch.StartNew();
                var response = await client.GetTextAsync(audio, cancellationToken: timeout.Token);
                elapsed.Stop();
                Assert.False(string.IsNullOrWhiteSpace(response.Text));
                var transcript = Assert.IsType<WhisperTranscript>(response.RawRepresentation);
                Assert.False(transcript.HasWordIntervals);
                clips.Add(new(fixture.Id, fixture.Language, fixture.Transcript, response.Text,
                    TranscriptionScorer.Score(fixture.Transcript, response.Text), elapsed.Elapsed.TotalSeconds,
                    transcript.Segments));
            }
            Assert.Equal(bundle.Fixtures.Select(f => f.Id), clips.Select(c => c.Id));
            complete = true;
        }
        finally
        {
            var report = new
            {
                Complete = complete, ExpectedClips = bundle.Fixtures.Length, EvaluatedClips = clips.Count,
                bundle.BundleVersion, bundle.Revision,
                ManifestSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(
                    Path.Combine(directory, "manifest.json")))).ToLowerInvariant(),
                ScoringPolicy = TranscriptionScorer.Policy,
                ModelRepository = WhisperModel.Repository, ModelRevision = WhisperModel.Revision,
                ModelSha256 = WhisperModel.Sha256, NativeRuntime = global::Whisper.net.WhisperFactory.GetRuntimeInfo(),
                Threads = 4, Language = "automatic", Context = "disabled across clips",
                Temperature = 0, TemperatureIncrement = 0,
                Framework = RuntimeInformation.FrameworkDescription, OperatingSystem = RuntimeInformation.OSDescription,
                LoadSeconds = load.Elapsed.TotalSeconds, ThresholdStatus = "unapproved-baseline",
                AcceptedAccuracyGate = false,
                Languages = clips.GroupBy(c => c.Language).ToDictionary(g => g.Key, g => new TranscriptionScore(
                    ErrorCounts.Sum(g.Select(c => c.Score.Words)), ErrorCounts.Sum(g.Select(c => c.Score.Characters)))),
                TimestampMetrics = "unsupported: no reviewed timestamp references", Clips = clips
            };
            await File.WriteAllTextAsync(Path.Combine(output, $"whisper-fleurs-{Environment.Version.Major}.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true
                }));
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "WhisperNativeSmoke")]
    public async Task NativeModelReturnsSpanishSegmentsAndCanBeReusedAfterCancellation()
    {
        string model = Environment.GetEnvironmentVariable("WHISPER_MODEL_PATH")
            ?? throw new InvalidOperationException("Set WHISPER_MODEL_PATH to an explicitly prepared pinned model.");
        string directory = Path.Combine(AppContext.BaseDirectory, "speech-fixtures");
        var bundle = SpeechFixtureBundle.LoadAndValidate(directory);
        var fixture = bundle.Fixtures.First(f => f.Language == "es_419");
        using var client = new WhisperSpeechToTextClient(new() { ModelPath = model, InputFormat = Pcm16InputFormat.Wav });
        await using var audio = File.OpenRead(SpeechFixtureBundle.ResolvePath(directory, fixture.Path));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var pending = client.GetTextAsync(audio, cancellationToken: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        audio.Position = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var response = await client.GetTextAsync(audio, cancellationToken: timeout.Token);
        var transcript = Assert.IsType<WhisperTranscript>(response.RawRepresentation);
        Assert.NotEmpty(transcript.Text);
        Assert.Equal("es", transcript.DetectedLanguage);
        Assert.NotEmpty(transcript.Segments);
    }

    private sealed record Clip(string Id, string Language, string Reference, string Hypothesis,
        TranscriptionScore Score, double InferenceSeconds, IReadOnlyList<WhisperSegment> Segments);
}
