namespace ElBruno.Speech.Stt.Parakeet;

public enum ParakeetInputFormat
{
    RawPcm16,
    Wav
}

/// <summary>Input is mono 16 kHz signed little-endian PCM16, raw or WAV wrapped.</summary>
public sealed class ParakeetOptions
{
    public string ModelDirectory { get; set; } = ParakeetModel.DefaultDirectory;
    public ParakeetInputFormat InputFormat { get; set; } = ParakeetInputFormat.RawPcm16;
    public int MaxInputBytes { get; set; } = 64 * 1024 * 1024;
    public int ChunkSeconds { get; set; } = 20;

    internal ParakeetOptions Snapshot()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ModelDirectory);
        if (!Enum.IsDefined(InputFormat))
            throw new ArgumentOutOfRangeException(nameof(InputFormat));
        if (MaxInputBytes < 44)
            throw new ArgumentOutOfRangeException(nameof(MaxInputBytes));
        if (ChunkSeconds is < 1 or > 30)
            throw new ArgumentOutOfRangeException(nameof(ChunkSeconds), "Use chunks between 1 and 30 seconds.");
        return new ParakeetOptions
        {
            ModelDirectory = ModelDirectory, InputFormat = InputFormat,
            MaxInputBytes = MaxInputBytes, ChunkSeconds = ChunkSeconds
        };
    }
}
