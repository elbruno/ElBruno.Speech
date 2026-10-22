using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ElBruno.Speech.Stt.Parakeet;
using ElBruno.Speech.TestUtils;
using Xunit;

namespace ElBruno.Speech.Stt.Parakeet.Tests;

public sealed class FleursEvaluationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true
    };

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ModelDownload")]
    [Trait("Category", "SpeechFixtureEvaluation")]
    public async Task TranscribesEveryIndependentFleursReference()
    {
        string modelDirectory = Environment.GetEnvironmentVariable("PARAKEET_MODEL_DIRECTORY")
            ?? throw new InvalidOperationException("Set PARAKEET_MODEL_DIRECTORY for explicit model download/validation.");
        string outputDirectory = Environment.GetEnvironmentVariable("SPEECH_FIXTURES_REPORT_DIRECTORY")
            ?? throw new InvalidOperationException("Set SPEECH_FIXTURES_REPORT_DIRECTORY.");
        bool recording = Environment.GetEnvironmentVariable("SPEECH_FIXTURES_RECORD_BASELINE") == "1";
        string bundleDirectory = Path.Combine(AppContext.BaseDirectory, "speech-fixtures");
        var bundle = SpeechFixtureBundle.LoadAndValidate(bundleDirectory);
        string digest = Convert.ToHexString(SHA256.HashData(
            await File.ReadAllBytesAsync(Path.Combine(bundleDirectory, "manifest.json")))).ToLowerInvariant();
        RegressionConfiguration? configuration = recording ? null :
            JsonSerializer.Deserialize<RegressionConfiguration>(
                await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "FleursRegression.json")), JsonOptions)
            ?? throw new InvalidDataException("Missing regression configuration.");
        if (configuration is not null)
        {
            Assert.Equal(TranscriptionScorer.Policy, configuration.ScoringPolicy);
            Assert.Equal(digest, configuration.ManifestSha256);
            Assert.Equal(ParakeetModel.Revision, configuration.ModelRevision);
            Assert.Contains(configuration.Status, new[] { "proposed", "approved" });
            Assert.Equal(new[] { "en_us", "es_419" }, configuration.Languages.Keys.Order().ToArray());
            Assert.All(configuration.Languages.Values, limits =>
            {
                Assert.True(double.IsFinite(limits.MaximumWer) && limits.MaximumWer is >= 0 and <= 1);
                Assert.True(double.IsFinite(limits.MaximumCer) && limits.MaximumCer is >= 0 and <= 1);
            });
        }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await ParakeetModel.DownloadAsync(modelDirectory, cancellation.Token);
        var load = Stopwatch.StartNew();
        using var client = new ParakeetSpeechToTextClient(new()
        {
            ModelDirectory = modelDirectory, InputFormat = ParakeetInputFormat.Wav
        });
        load.Stop();
        var clips = new List<ClipResult>();
        var languageScores = new Dictionary<string, TranscriptionScore>();
        var comparisons = new Dictionary<string, bool>();
        Directory.CreateDirectory(outputDirectory);
        string reportPath = Path.Combine(outputDirectory, $"fleurs-{Environment.Version.Major}.json");
        bool complete = false;
        try
        {
            foreach (var fixture in bundle.Fixtures)
            {
                await using var input = File.OpenRead(SpeechFixtureBundle.ResolvePath(bundleDirectory, fixture.Path));
                var elapsed = Stopwatch.StartNew();
                var response = await client.GetTextAsync(input, cancellationToken: cancellation.Token);
                elapsed.Stop();
                Assert.False(string.IsNullOrWhiteSpace(response.Text));
                var score = TranscriptionScorer.Score(fixture.Transcript, response.Text);
                clips.Add(new(fixture.Id, fixture.Language, fixture.SourceId, fixture.Transcript,
                    response.Text, score, elapsed.Elapsed.TotalSeconds));
            }
            Assert.Equal(bundle.Fixtures.Select(f => f.Id), clips.Select(c => c.Id));
            foreach (var group in clips.GroupBy(c => c.Language))
            {
                var score = new TranscriptionScore(ErrorCounts.Sum(group.Select(c => c.Score.Words)),
                    ErrorCounts.Sum(group.Select(c => c.Score.Characters)));
                languageScores.Add(group.Key, score);
                if (configuration is not null)
                {
                    var limit = configuration.Languages[group.Key];
                    comparisons.Add(group.Key, score.Words.Rate <= limit.MaximumWer &&
                        score.Characters.Rate <= limit.MaximumCer);
                }
            }
            complete = true;
        }
        finally
        {
            // Persist partial coverage on inference failures as well as full baseline/regression evidence.
            var report = new
            {
                Complete = complete, ExpectedClips = bundle.Fixtures.Length, EvaluatedClips = clips.Count,
                bundle.BundleVersion, ManifestSha256 = digest, bundle.Revision,
                ScoringPolicy = TranscriptionScorer.Policy,
                ModelRepository = ParakeetModel.Repository, ModelRevision = ParakeetModel.Revision,
                OnnxRuntimeVersion = Microsoft.ML.OnnxRuntime.OrtEnv.Instance().GetVersionString(),
                Framework = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                LoadSeconds = load.Elapsed.TotalSeconds,
                ThresholdStatus = configuration?.Status ?? "baseline-recording",
                AcceptedAccuracyGate = configuration?.Status == "approved",
                TimestampMetrics = "unsupported: native token starts only; no reviewed word timing references",
                Languages = languageScores,
                Corpus = new TranscriptionScore(ErrorCounts.Sum(clips.Select(c => c.Score.Words)),
                    ErrorCounts.Sum(clips.Select(c => c.Score.Characters))),
                ProposedThresholdComparisons = comparisons, Clips = clips
            };
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        }
        if (configuration?.Status == "approved")
            Assert.All(comparisons, item => Assert.True(item.Value, $"Accuracy regression for {item.Key}; see {reportPath}."));
        Console.WriteLine($"Saved {clips.Count} independent-reference scores to {reportPath}. " +
            $"Threshold status: {configuration?.Status ?? "baseline-recording"}.");
    }

    private sealed record ClipResult(string Id, string Language, string SourceId, string Reference,
        string Hypothesis, TranscriptionScore Score, double InferenceSeconds);
    private sealed record Limits(double MaximumWer, double MaximumCer);
    private sealed record RegressionConfiguration(string Status, string ScoringPolicy, string ManifestSha256,
        string ModelRevision, string Rationale, Dictionary<string, Limits> Languages);
}
