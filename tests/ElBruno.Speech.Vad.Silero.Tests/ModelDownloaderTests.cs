using ElBruno.Speech.Vad.Silero;
using FluentAssertions;
using Xunit;

namespace ElBruno.Speech.Vad.Silero.Tests;

public sealed class ModelDownloaderTests
{
    [Fact]
    public void DefaultModelPath_ContainsCacheDir()
    {
        ModelDownloader.DefaultModelPath.Should().Contain(".cache");
        ModelDownloader.DefaultModelPath.Should().Contain("elbruno-speech");
        ModelDownloader.DefaultModelPath.Should().EndWith("silero_vad_v4.onnx");
    }

    [Fact]
    public void DefaultModelPath_IsAbsolute()
    {
        Path.IsPathRooted(ModelDownloader.DefaultModelPath).Should().BeTrue();
    }
}
