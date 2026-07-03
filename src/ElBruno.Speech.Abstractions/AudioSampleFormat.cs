namespace ElBruno.Speech;

/// <summary>Specifies the sample format of a PCM audio stream.</summary>
public enum AudioSampleFormat
{
    /// <summary>Signed 16-bit integer PCM (little-endian).</summary>
    Int16 = 0,

    /// <summary>32-bit IEEE 754 floating-point PCM, normalized to [-1.0, 1.0].</summary>
    Float32 = 1,
}
