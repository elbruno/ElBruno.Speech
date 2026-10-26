using System.ComponentModel;
using System.Diagnostics;
using ElBruno.Speech.Tts.Ollama;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSpeechDemo;

if (args.Contains("--help"))
{
    Console.WriteLine("""
        OllamaSpeechDemo [--text "..."] [--voice Freya] [--output directory]
                         [--endpoint http://localhost:11434/] [--play]

        Requires Ollama 0.40.9+ with Phonon installed on a host with MLX support.
        Run 'ollama pull phonon' on that host first. A remote server URL is supported.
        Host roots and /v1 API bases are completed to /v1/audio/speech automatically.
        Generates complete.wav, streamed.pcm, and streamed.wav.
        --play opens streamed.wav in your default audio player after generation.
        """);
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

try
{
    string text = "Hello! This is a local text to speech demo, built with C sharp and Ollama Phonon.";
    string outputDirectory = Path.Combine("artifacts", "ollama-speech");
    var options = new OllamaSpeechOptions();
    bool play = false;

    for (int i = 0; i < args.Length; i++)
    {
        string option = args[i];
        if (option == "--play")
        {
            play = true;
            continue;
        }
        if (option is not ("--text" or "--voice" or "--output" or "--endpoint"))
            throw new ArgumentException($"Unknown argument '{option}'. Use --help for usage.");
        if (++i >= args.Length)
            throw new ArgumentException($"Missing value for {option}.");

        switch (option)
        {
            case "--text": text = args[i]; break;
            case "--voice": options.Voice = args[i]; break;
            case "--output": outputDirectory = args[i]; break;
            case "--endpoint": options.Endpoint = new Uri(args[i], UriKind.Absolute); break;
        }
    }

    var services = new ServiceCollection();
    services.AddOllamaSpeech(options);
    using var provider = services.BuildServiceProvider();
    var speech = provider.GetRequiredService<ITextToSpeechClient>();

    Console.WriteLine($"Endpoint: {options.Endpoint}");
    Console.WriteLine($"Model: {options.Model}, voice: {options.Voice ?? "default"}");
    Console.WriteLine($"Text: {text}");
    await SpeechDemo.GenerateAsync(speech, text, outputDirectory, Console.Out, cancellation.Token);

    if (play)
    {
        string path = Path.GetFullPath(Path.Combine(outputDirectory, "streamed.wav"));
        using var player = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        Console.WriteLine("Opened streamed.wav in the system audio player. Listen to verify pronunciation and quality.");
    }

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Speech generation cancelled. Any partial streamed.pcm file is not a completed result.");
    return 130;
}
catch (HttpRequestException error)
{
    Console.Error.WriteLine($"Ollama request failed: {error.Message}");
    Console.Error.WriteLine("Check that Ollama is running, Phonon is installed, and the server has MLX support.");
    return 1;
}
catch (Exception error) when (error is ArgumentException or IOException or NotSupportedException or Win32Exception)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
