using System.Diagnostics;
using System.Text.Json;
using ElBruno.Speech.Stt.Parakeet;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: ParakeetTranscribe input.wav model-directory output.json");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var load = Stopwatch.StartNew();
using var client = new ParakeetSpeechToTextClient(new()
{
    ModelDirectory = args[1],
    InputFormat = ParakeetInputFormat.Wav
});
load.Stop();
await using var input = File.OpenRead(args[0]);
var inference = Stopwatch.StartNew();
var response = await client.GetTextAsync(input, cancellationToken: cancellation.Token);
inference.Stop();
var transcript = (ParakeetTranscript)response.RawRepresentation!;
var result = new
{
    response.ModelId,
    ModelRepository = ParakeetModel.Repository,
    ModelRevision = ParakeetModel.Revision,
    transcript.Text,
    transcript.DetectedLanguage,
    transcript.HasWordIntervals,
    transcript.HasSegmentIntervals,
    transcript.TimingProvenance,
    Tokens = transcript.Tokens.Select(token => new { token.Text, StartSeconds = token.Start.TotalSeconds, token.IsInPadding }),
    LoadSeconds = load.Elapsed.TotalSeconds,
    InferenceSeconds = inference.Elapsed.TotalSeconds,
    PeakProcessWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64
};
await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(result, new JsonSerializerOptions
{
    WriteIndented = true
}), cancellation.Token);
Console.WriteLine($"Saved {transcript.Tokens.Count} native token starts. Load {load.Elapsed.TotalSeconds:F2}s, inference {inference.Elapsed.TotalSeconds:F2}s.");
return 0;
