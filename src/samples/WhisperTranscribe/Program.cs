using System.Text.Json;
using ElBruno.Speech.Audio;
using ElBruno.Speech.Stt.Whisper;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
if (args.Length == 2 && args[0] == "download")
{
    await WhisperModel.DownloadAsync(args[1], cancellation.Token);
    Console.WriteLine("Pinned large-v3 model verified.");
    return 0;
}
if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: WhisperTranscribe download model.bin OR WhisperTranscribe input.wav model.bin output.json");
    return 2;
}
using var client = new WhisperSpeechToTextClient(new() { ModelPath = args[1], InputFormat = Pcm16InputFormat.Wav });
await using var audio = File.OpenRead(args[0]);
var response = await client.GetTextAsync(audio, cancellationToken: cancellation.Token);
var transcript = (WhisperTranscript)response.RawRepresentation!;
await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(new
{
    response.ModelId, ModelRepository = WhisperModel.Repository, ModelRevision = WhisperModel.Revision,
    transcript.Text, transcript.DetectedLanguage, transcript.HasWordIntervals, transcript.HasSegmentIntervals,
    transcript.TimingProvenance,
    Segments = transcript.Segments.Select(s => new
    {
        s.Text, StartSeconds = s.Start.TotalSeconds, EndSeconds = s.End.TotalSeconds,
        s.DetectedLanguage, s.ExtendsPastInput
    })
}, new JsonSerializerOptions { WriteIndented = true }), cancellation.Token);
Console.WriteLine($"Saved {transcript.Segments.Count} unreviewed native segment predictions.");
return 0;
