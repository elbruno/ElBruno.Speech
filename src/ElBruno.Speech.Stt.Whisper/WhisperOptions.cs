using ElBruno.Speech.Audio;

namespace ElBruno.Speech.Stt.Whisper;

public sealed class WhisperOptions
{
    public string ModelPath { get; set; } = WhisperModel.DefaultPath;
    public Pcm16InputFormat InputFormat { get; set; } = Pcm16InputFormat.RawPcm16;
    public int MaxInputBytes { get; set; } = 64 * 1024 * 1024;
    public int Threads { get; set; } = 4;

    internal WhisperOptions Snapshot()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ModelPath);
        if (!Enum.IsDefined(InputFormat)) throw new ArgumentOutOfRangeException(nameof(InputFormat));
        if (MaxInputBytes < 44) throw new ArgumentOutOfRangeException(nameof(MaxInputBytes));
        if (Threads is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(Threads));
        return new()
        {
            ModelPath = ModelPath, InputFormat = InputFormat, MaxInputBytes = MaxInputBytes, Threads = Threads
        };
    }
}
