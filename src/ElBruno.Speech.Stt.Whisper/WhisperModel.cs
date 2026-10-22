using System.Security.Cryptography;

namespace ElBruno.Speech.Stt.Whisper;

/// <summary>Pinned, unquantized GGML Whisper large-v3 weights (predominantly F16). Downloads are explicit.</summary>
public static class WhisperModel
{
    public const string ModelId = "whisper-large-v3";
    public const string Repository = "ggerganov/whisper.cpp";
    public const string Revision = "5359861c739e955e79d9a303bcbc70fb988958b1";
    public const string Filename = "ggml-large-v3.bin";
    public const long Size = 3095033483;
    public const string Sha256 = "64d182b440b98d5203c4f9bd541544d84c605196c4f7b845dfa11fb23594d1e2";
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache", "elbruno-speech", ModelId, Filename);

    /// <summary>Downloads about 3.1 GB; verifies cached files rather than silently replacing corruption.</summary>
    public static async Task DownloadAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        path ??= DefaultPath;
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            Verify(path);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".download";
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        try
        {
            using var response = await http.GetAsync(
                $"https://huggingface.co/{Repository}/resolve/{Revision}/{Filename}",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = File.Create(temporary))
            {
                byte[] buffer = new byte[81920];
                long length = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    length += read;
                    if (length > Size) throw new InvalidDataException("Whisper model exceeds its pinned size.");
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }
            Verify(temporary);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static void Verify(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        if (stream.Length != Size ||
            !Convert.ToHexString(SHA256.HashData(stream)).Equals(Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Whisper model failed size/SHA-256 validation. Remove the corrupt file and download again.");
    }
}
