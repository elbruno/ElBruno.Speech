using ElBruno.Speech.Stt.Parakeet;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace ElBruno.Speech.Stt.Parakeet.Tests;

public class ParakeetIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ModelDownload")]
    public async Task PublishedBundleDownloadsAndTranscribes()
    {
        string fixtures = Environment.GetEnvironmentVariable("PARAKEET_FIXTURE_DIRECTORY")
            ?? throw new InvalidOperationException("Set PARAKEET_FIXTURE_DIRECTORY.");
        string directory = Path.Combine(fixtures, "downloaded-parakeet-model");
        await ParakeetModel.DownloadAsync(directory);
        using var client = new ParakeetSpeechToTextClient(new()
        {
            ModelDirectory = directory, InputFormat = ParakeetInputFormat.Wav
        });
        await using var input = File.OpenRead(Path.Combine(fixtures, "es.wav"));
        var response = await client.GetTextAsync(input);
        Assert.Equal((await File.ReadAllTextAsync(Path.Combine(fixtures, "es.hypothesis.txt"))).Trim(), response.Text);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("es")]
    [InlineData("en")]
    [InlineData("mixed")]
    public async Task RecordedAudioMatchesPinnedReferenceRuntime(string language)
    {
        string? directory = Environment.GetEnvironmentVariable("PARAKEET_MODEL_DIRECTORY");
        string? fixtures = Environment.GetEnvironmentVariable("PARAKEET_FIXTURE_DIRECTORY");
        if (directory is null || fixtures is null)
            throw new InvalidOperationException("Set PARAKEET_MODEL_DIRECTORY and PARAKEET_FIXTURE_DIRECTORY to run real-model tests.");
        var load = Stopwatch.StartNew();
        using var client = new ParakeetSpeechToTextClient(new()
        {
            ModelDirectory = directory, InputFormat = ParakeetInputFormat.Wav
        });
        load.Stop();
        await using var input = File.OpenRead(Path.Combine(fixtures, language + ".wav"));
        var inference = Stopwatch.StartNew();
        var response = await client.GetTextAsync(input);
        inference.Stop();
        string expected = (await File.ReadAllTextAsync(Path.Combine(fixtures, language + ".hypothesis.txt"))).Trim();
        Assert.Equal(expected, response.Text);
        var transcript = Assert.IsType<ParakeetTranscript>(response.RawRepresentation);
        Assert.NotEmpty(transcript.Tokens);
        Assert.All(transcript.Tokens, token => Assert.True(token.Start >= TimeSpan.Zero));
        Assert.True(transcript.Tokens.Zip(transcript.Tokens.Skip(1)).All(pair => pair.First.Start <= pair.Second.Start));
        using var tokenReference = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixtures, language + ".hypothesis.json")));
        var reference = tokenReference.RootElement.EnumerateArray().ToArray();
        Assert.Equal(reference.Length, transcript.Tokens.Count);
        for (int i = 0; i < reference.Length; i++)
        {
            Assert.Equal(reference[i].GetProperty("text").GetString(), transcript.Tokens[i].Text);
            Assert.Equal(reference[i].GetProperty("start_seconds").GetDouble(),
                transcript.Tokens[i].Start.TotalSeconds, precision: 6);
        }
        Console.WriteLine($"{language}: load={load.Elapsed.TotalSeconds:F2}s inference={inference.Elapsed.TotalSeconds:F2}s " +
            $"peak-working-set={Process.GetCurrentProcess().PeakWorkingSet64 / 1048576}MiB text={response.Text}");
    }
}
