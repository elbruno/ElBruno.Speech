using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ElBruno.Speech.Audio;
using ElBruno.Speech.TestUtils;
using Whisper.net;

if (args.Length != 4 || args[0] is not ("whisper" or "score"))
    throw new ArgumentException("Usage: SpeechBenchmark whisper model.bin bundle-directory output.json OR score input.json bundle-directory output.json");
var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
string bundleDirectory = args[2];
var bundle = SpeechFixtureBundle.LoadAndValidate(bundleDirectory);
var results = new List<Clip>();
object provenance;
if (args[0] == "whisper")
{
    string model = args[1];
    using (var stream = File.OpenRead(model))
    {
        if (stream.Length != 3095033483 ||
            Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() !=
            "64d182b440b98d5203c4f9bd541544d84c605196c4f7b845dfa11fb23594d1e2")
            throw new InvalidDataException("Expected pinned unquantized ggml-large-v3.bin.");
    }
    var load = Stopwatch.StartNew();
    using var factory = WhisperFactory.FromPath(model, new WhisperFactoryOptions { UseGpu = false });
    await using var processor = factory.CreateBuilder().WithLanguage("auto").WithThreads(4)
        .WithNoContext().WithTemperature(0).WithTemperatureInc(0).Build();
    load.Stop();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(45));
    static float[] ReadSamples(string path)
    {
        var (_, pcm) = WavReader.Read(path);
        var samples = new float[pcm.Length / 2];
        PcmConverter.Int16ToFloat32(pcm.Span, samples);
        return samples;
    }
    var warmup = Stopwatch.StartNew();
    await foreach (var _ in processor.ProcessAsync(ReadSamples(SpeechFixtureBundle.ResolvePath(
        bundleDirectory, bundle.Fixtures[0].Path)), timeout.Token)) { }
    warmup.Stop();
    foreach (var fixture in bundle.Fixtures)
    {
        var samples = ReadSamples(SpeechFixtureBundle.ResolvePath(bundleDirectory, fixture.Path));
        var elapsed = Stopwatch.StartNew();
        var pieces = new List<string>();
        await foreach (var segment in processor.ProcessAsync(samples, timeout.Token))
            pieces.Add(segment.Text);
        elapsed.Stop();
        string hypothesis = string.Concat(pieces).Trim();
        results.Add(new(fixture.Id, fixture.Language, fixture.Transcript, hypothesis,
            TranscriptionScorer.Score(fixture.Transcript, hypothesis), elapsed.Elapsed.TotalSeconds));
        Console.WriteLine($"{fixture.Id}: {elapsed.Elapsed.TotalSeconds:F2}s");
    }
    provenance = new
    {
        Model = "whisper-large-v3", Repository = "ggerganov/whisper.cpp",
        Revision = "5359861c739e955e79d9a303bcbc70fb988958b1",
        Precision = "unquantized GGML (predominantly F16 weights)",
        ModelBytes = new FileInfo(model).Length, Runtime = "Whisper.net 1.9.1",
        NativeRuntime = WhisperFactory.GetRuntimeInfo(), Threads = 4,
        Language = "automatic, no reference language hints", Context = "disabled across clips",
        Temperature = 0, TemperatureIncrement = 0,
        Sampling = "greedy (Whisper.net default)",
        LoadSeconds = load.Elapsed.TotalSeconds, WarmupSeconds = warmup.Elapsed.TotalSeconds,
        PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
        Framework = RuntimeInformation.FrameworkDescription, OperatingSystem = RuntimeInformation.OSDescription
    };
}
else
{
    using var input = JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
    var root = input.RootElement;
    provenance = root.GetProperty("provenance").Clone();
    var clips = root.GetProperty("clips").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString()!);
    if (clips.Count != 20 || !clips.Keys.ToHashSet().SetEquals(bundle.Fixtures.Select(f => f.Id)))
        throw new InvalidDataException("Input does not cover the pinned bundle.");
    foreach (var fixture in bundle.Fixtures)
    {
        var clip = clips[fixture.Id];
        string hypothesis = clip.GetProperty("hypothesis").GetString()!;
        double seconds = clip.GetProperty("inferenceSeconds").GetDouble();
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new InvalidDataException("Inference time must be finite and nonnegative.");
        results.Add(new(fixture.Id, fixture.Language, fixture.Transcript, hypothesis,
            TranscriptionScorer.Score(fixture.Transcript, hypothesis), seconds));
    }
}
if (results.Count != 20) throw new InvalidDataException("Incomplete benchmark.");
var languages = results.GroupBy(c => c.Language).ToDictionary(g => g.Key, g => new
{
    Words = ErrorCounts.Sum(g.Select(c => c.Score.Words)),
    Characters = ErrorCounts.Sum(g.Select(c => c.Score.Characters)),
    InferenceSeconds = g.Sum(c => c.InferenceSeconds)
});
double audioSeconds = bundle.Fixtures.Sum(f => f.DurationSeconds);
var report = new
{
    Complete = true, Provenance = provenance, bundle.BundleVersion,
    ManifestSha256 = Convert.ToHexString(SHA256.HashData(
        await File.ReadAllBytesAsync(Path.Combine(bundleDirectory, "manifest.json")))).ToLowerInvariant(),
    ScoringPolicy = TranscriptionScorer.Policy, AudioSeconds = audioSeconds,
    InferenceSeconds = results.Sum(c => c.InferenceSeconds),
    RealTimeFactor = results.Sum(c => c.InferenceSeconds) / audioSeconds,
    Languages = languages, Clips = results,
    TimestampMetrics = "unsupported: no reviewed timestamp references"
};
await File.WriteAllTextAsync(args[3], JsonSerializer.Serialize(report, jsonOptions));
Console.WriteLine($"Saved {args[3]}");

sealed record Clip(string Id, string Language, string Reference, string Hypothesis,
    TranscriptionScore Score, double InferenceSeconds);
