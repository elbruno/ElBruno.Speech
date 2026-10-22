using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElBruno.Speech.TestUtils;

public sealed record SpeechFixture
{
    public required string Id { get; init; }
    public required string Language { get; init; }
    public required string Path { get; init; }
    public required string Sha256 { get; init; }
    public required int SampleRate { get; init; }
    public required int Channels { get; init; }
    public required string SampleFormat { get; init; }
    public required int SampleCount { get; init; }
    public required double DurationSeconds { get; init; }
    public required string RawTranscript { get; init; }
    public required string Transcript { get; init; }
    public required string SourceId { get; init; }
    public required string SourceFilename { get; init; }
    public required string SourceSha256 { get; init; }
    public required string SourceUrl { get; init; }
    public required string SourceMember { get; init; }
    public required string Transformation { get; init; }
}

public sealed record SpeechFixtureSource
{
    public required string Configuration { get; init; }
    public required string Split { get; init; }
    public required string MetadataPath { get; init; }
    public required string MetadataSha256 { get; init; }
    public required string ArchivePath { get; init; }
}

public sealed record SpeechFixtureBundle
{
    public const string UpstreamRevision = "70bb2e84b976b7e960aa89f1c648e09c59f894dd";
    public const long MaximumBytes = 20 * 1024 * 1024;
    public required int SchemaVersion { get; init; }
    public required string BundleVersion { get; init; }
    public required string Dataset { get; init; }
    public required string Revision { get; init; }
    public required string License { get; init; }
    public required string LicenseUrl { get; init; }
    public required bool ReviewedWordTimestamps { get; init; }
    public required string Selection { get; init; }
    public required SpeechFixtureSource[] Sources { get; init; }
    public required SpeechFixture[] Fixtures { get; init; }
    public required Dictionary<string, string> Notices { get; init; }

    public static SpeechFixtureBundle LoadAndValidate(string directory)
    {
        var bundle = JsonSerializer.Deserialize<SpeechFixtureBundle>(
            File.ReadAllText(ResolvePath(directory, "manifest.json")),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            }) ?? throw new InvalidDataException("Null fixture manifest.");
        bundle.Validate(directory);
        return bundle;
    }

    public void Validate(string directory)
    {
        Require(SchemaVersion == 1 && BundleVersion == "1.0.0", "Unsupported fixture version.");
        Require(Dataset == "google/fleurs" && Revision == UpstreamRevision, "Unexpected dataset revision.");
        Require(License == "CC-BY-4.0" && LicenseUrl == "https://creativecommons.org/licenses/by/4.0/",
            "Missing dataset license.");
        Require(!ReviewedWordTimestamps && !string.IsNullOrWhiteSpace(Selection), "Invalid reference provenance.");
        Require(Fixtures is { Length: 20 } && Fixtures.All(f => f is not null), "Expected twenty fixtures.");
        Require(Sources is { Length: 2 } && Sources.All(s => s is not null), "Expected two source configurations.");
        Require(Sources.Select(s => s.Configuration).Distinct().Count() == 2, "Duplicate source configuration.");
        Require(Notices is not null, "Missing notices.");
        string[] languages = ["en_us", "es_419"];
        Require(Fixtures.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() == 20, "Duplicate fixture ID.");
        var expected = new HashSet<string>(StringComparer.Ordinal) { "manifest.json" };
        string[] notices = ["README.md", "ATTRIBUTION.md", "LICENSE.txt"];
        Require(Notices.Keys.Order().SequenceEqual(notices.Order()), "Missing license/attribution notices.");
        foreach (var notice in Notices)
        {
            VerifyHash(ResolvePath(directory, notice.Key), notice.Value);
            expected.Add(notice.Key);
        }
        foreach (string language in languages)
        {
            var group = Fixtures.Where(f => f.Language == language).ToArray();
            Require(group.Length == 10 && group.Select(f => f.SourceId).Distinct().Count() == 10,
                $"Expected ten distinct sentences for {language}.");
            var source = Sources.SingleOrDefault(s => s.Configuration == language);
            Require(source is not null && source.Split == "test" &&
                source.MetadataPath == $"data/{language}/test.tsv" &&
                source.ArchivePath == $"data/{language}/audio/test.tar.gz" &&
                IsHash(source.MetadataSha256), $"Invalid source metadata for {language}.");
        }
        foreach (var fixture in Fixtures)
        {
            Require(!string.IsNullOrWhiteSpace(fixture.Id) &&
                !string.IsNullOrWhiteSpace(fixture.Transcript) &&
                !string.IsNullOrWhiteSpace(fixture.RawTranscript) &&
                !string.IsNullOrWhiteSpace(fixture.Transformation) &&
                !string.IsNullOrWhiteSpace(fixture.SourceFilename) &&
                !string.IsNullOrWhiteSpace(fixture.SourceId) && IsHash(fixture.SourceSha256),
                "Missing fixture reference/provenance.");
            Require(languages.Contains(fixture.Language) &&
                fixture.SourceFilename == System.IO.Path.GetFileName(fixture.SourceFilename) &&
                fixture.SourceFilename.EndsWith(".wav", StringComparison.Ordinal) &&
                fixture.Path == $"{fixture.Language}/{fixture.SourceFilename}" &&
                fixture.SourceMember == $"test/{fixture.SourceFilename}" &&
                fixture.SourceUrl == $"https://huggingface.co/datasets/google/fleurs/resolve/{Revision}/data/{fixture.Language}/audio/test.tar.gz",
                "Unexpected fixture source/path.");
            Require(expected.Add(fixture.Path), "Duplicate fixture path.");
            string path = ResolvePath(directory, fixture.Path);
            VerifyHash(path, fixture.Sha256);
            Require(fixture.SampleRate == 16000 && fixture.Channels == 1 &&
                fixture.SampleFormat == "pcm_s16le" && fixture.SampleCount is >= 32000 and <= 240000 &&
                fixture.DurationSeconds == fixture.SampleCount / 16000d, "Invalid fixture audio metadata.");
            ValidateWave(File.ReadAllBytes(path), fixture.SampleCount);
        }
        var actual = Directory.EnumerateFiles(directory, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint
        }).Select(p => System.IO.Path.GetRelativePath(directory, p).Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
        Require(expected.SetEquals(actual), "Unexpected or missing files in bundle.");
        Require(actual.Sum(p => new FileInfo(ResolvePath(directory, p)).Length) <= MaximumBytes,
            "Uncompressed bundle exceeds 20 MiB.");
    }

    public static string ResolvePath(string directory, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) &&
            !relative.Contains('\\') && !relative.Contains(':') &&
            !System.IO.Path.IsPathRooted(relative) &&
            relative.Split('/').All(p => p is not ("" or "." or "..")), "Unsafe fixture path.");
        string current = System.IO.Path.GetFullPath(directory);
        Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Linked bundle directory.");
        foreach (string part in relative.Split('/'))
        {
            current = System.IO.Path.Combine(current, part);
            if (File.Exists(current) || Directory.Exists(current))
                Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Linked fixture path.");
        }
        return current;
    }

    private static void VerifyHash(string path, string digest)
    {
        Require(IsHash(digest), "Malformed SHA-256.");
        using var stream = File.OpenRead(path);
        Require(Convert.ToHexString(SHA256.HashData(stream)).Equals(digest, StringComparison.OrdinalIgnoreCase),
            $"SHA-256 mismatch: {System.IO.Path.GetFileName(path)}.");
    }

    private static bool IsHash(string? value) =>
        value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private static void ValidateWave(byte[] bytes, int count)
    {
        Require(bytes.Length == 44 + count * 2, "Invalid canonical WAV length.");
        Require(bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 8).SequenceEqual("WAVEfmt "u8) &&
            bytes.AsSpan(36, 4).SequenceEqual("data"u8), "Invalid canonical WAV header.");
        Require(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) == bytes.Length - 8 &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)) == 16 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(20)) == 1 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(22)) == 1 &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24)) == 16000 &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28)) == 32000 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(32)) == 2 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(34)) == 16 &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(40)) == count * 2, "Invalid PCM16 WAV format.");
    }

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
