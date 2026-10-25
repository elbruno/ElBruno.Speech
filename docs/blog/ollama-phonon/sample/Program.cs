#pragma warning disable MEAI001

using ElBruno.Speech;
using ElBruno.Speech.Audio;
using ElBruno.Speech.Tts.Ollama;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddOllamaSpeech(new OllamaSpeechOptions
{
    Endpoint = new Uri("http://localhost:11434/v1/audio/speech"),
    Model = "phonon"
});

using var provider = services.BuildServiceProvider();
var speech = provider.GetRequiredService<ITextToSpeechClient>();
using var pcm = new MemoryStream();

await foreach (var update in speech.GetStreamingAudioAsync(
    "Hello from C sharp! My app has a voice thanks to Ollama Phonon.",
    new TextToSpeechOptions { AudioFormat = "pcm" }))
{
    foreach (var audio in update.Contents.OfType<DataContent>())
        await pcm.WriteAsync(audio.Data);
}

byte[] samples = pcm.ToArray();
if (samples.Length == 0 || samples.Length % 2 != 0)
    throw new InvalidDataException("Expected complete PCM16 audio samples.");

WavWriter.Write("hello.wav",
    new AudioFormat(24000, 1, AudioSampleFormat.Int16), samples);

Console.WriteLine($"Saved: {Path.GetFullPath("hello.wav")}");
