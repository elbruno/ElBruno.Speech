using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ElBruno.Speech.Audio;
using ElBruno.Speech.TestUtils;
using Xunit;

namespace ElBruno.Speech.Fixtures.Tests;

public sealed class SpeechFixtureTests : IDisposable
{
    private static string Bundle => Path.Combine(AppContext.BaseDirectory, "speech-fixtures");
    private readonly string _temporary = Path.Combine(Path.GetTempPath(), "speech-fixtures-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CommittedBundleIsCompleteAndValid()
    {
        var bundle = SpeechFixtureBundle.LoadAndValidate(Bundle);
        Assert.Equal(20, bundle.Fixtures.Length);
        Assert.All(bundle.Fixtures, fixture => Assert.NotEmpty(TranscriptionScorer.Normalize(fixture.Transcript)));
    }

    [Fact]
    public void RuntimeWavReaderAcceptsEveryFixture()
    {
        var bundle = SpeechFixtureBundle.LoadAndValidate(Bundle);
        foreach (var fixture in bundle.Fixtures)
        {
            var (format, samples) = WavReader.Read(SpeechFixtureBundle.ResolvePath(Bundle, fixture.Path));
            Assert.Equal(AudioFormat.Pcm16KhzMono, format);
            Assert.Equal(fixture.SampleCount * 2, samples.Length);
        }
    }

    [Theory]
    [InlineData("../escape.wav")]
    [InlineData("/absolute.wav")]
    [InlineData("C:/absolute.wav")]
    [InlineData("en_us\\escape.wav")]
    [InlineData("en_us/../escape.wav")]
    [InlineData("en_us//escape.wav")]
    public void RejectsUnsafePaths(string path) =>
        Assert.Throws<InvalidDataException>(() => SpeechFixtureBundle.ResolvePath(Bundle, path));

    [Theory]
    [InlineData("corrupt-audio")]
    [InlineData("invalid-header")]
    [InlineData("duplicate-id")]
    [InlineData("missing-file")]
    [InlineData("missing-notice")]
    [InlineData("wrong-license")]
    [InlineData("wrong-count")]
    [InlineData("null-fixtures")]
    [InlineData("null-source-filename")]
    [InlineData("duplicate-source-configuration")]
    [InlineData("oversized-notice")]
    [InlineData("extra-file")]
    public void RejectsInvalidBundle(string mutation)
    {
        CopyBundle();
        string manifestPath = Path.Combine(_temporary, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var fixtures = manifest["fixtures"]!.AsArray();
        string audio = SpeechFixtureBundle.ResolvePath(_temporary, fixtures[0]!["path"]!.GetValue<string>());
        switch (mutation)
        {
            case "corrupt-audio": File.WriteAllBytes(audio, [0, 1]); break;
            case "invalid-header":
                var bytes = File.ReadAllBytes(audio);
                bytes[0] = 0;
                File.WriteAllBytes(audio, bytes);
                fixtures[0]!["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                break;
            case "duplicate-id": fixtures[1]!["id"] = fixtures[0]!["id"]!.GetValue<string>(); break;
            case "missing-file": File.Delete(audio); break;
            case "missing-notice": manifest["notices"]!.AsObject().Remove("LICENSE.txt"); break;
            case "wrong-license": manifest["license"] = "MIT"; break;
            case "wrong-count": fixtures.RemoveAt(0); break;
            case "null-fixtures": manifest["fixtures"] = null; break;
            case "null-source-filename": fixtures[0]!["sourceFilename"] = null; break;
            case "duplicate-source-configuration":
                manifest["sources"]![1]!["configuration"] = manifest["sources"]![0]!["configuration"]!.GetValue<string>();
                break;
            case "oversized-notice":
                var notice = new byte[SpeechFixtureBundle.MaximumBytes + 1];
                File.WriteAllBytes(Path.Combine(_temporary, "README.md"), notice);
                manifest["notices"]!["README.md"] = Convert.ToHexString(SHA256.HashData(notice)).ToLowerInvariant();
                break;
            case "extra-file": File.WriteAllText(Path.Combine(_temporary, "unexpected.txt"), "extra"); break;
        }
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        var exception = Record.Exception(() => SpeechFixtureBundle.LoadAndValidate(_temporary));
        Assert.NotNull(exception);
        Assert.True(exception is InvalidDataException or FileNotFoundException, exception.ToString());
    }

    [Fact]
    public void RejectsMalformedManifest()
    {
        CopyBundle();
        File.WriteAllText(Path.Combine(_temporary, "manifest.json"), "{}");
        Assert.Throws<JsonException>(() => SpeechFixtureBundle.LoadAndValidate(_temporary));
    }

    private void CopyBundle()
    {
        foreach (string source in Directory.EnumerateFiles(Bundle, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(_temporary, Path.GetRelativePath(Bundle, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporary)) Directory.Delete(_temporary, recursive: true);
    }
}
