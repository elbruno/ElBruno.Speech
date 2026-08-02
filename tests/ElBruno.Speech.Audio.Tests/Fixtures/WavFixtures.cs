namespace ElBruno.Speech.Audio.Tests.Fixtures;

/// <summary>Provides synthetic WAV data for deterministic tests.</summary>
public static class WavFixtures
{
    /// <summary>Returns 100 ms of 440 Hz sine wave at 16 kHz mono Int16 (3200 bytes of PCM).</summary>
    public static byte[] Generate440HzSine16KMono100Ms()
    {
        const int sampleRate = 16_000;
        const double frequency = 440.0;
        const double durationSec = 0.1;
        int sampleCount = (int)(sampleRate * durationSec);
        var bytes = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            double t = i / (double)sampleRate;
            short s = (short)(short.MaxValue * Math.Sin(2 * Math.PI * frequency * t));
            bytes[i * 2] = (byte)(s & 0xFF);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return bytes;
    }
}
