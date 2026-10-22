using System.Security.Cryptography;

namespace ElBruno.Speech.Stt.Parakeet;

/// <summary>The verified multilingual INT8 bundle. The model is CC BY 4.0, not the library's MIT license.</summary>
public static class ParakeetModel
{
    public const string ModelId = "parakeet-tdt-0.6b-v3";
    public const string Repository = "elbruno/parakeet-tdt-0.6b-v3-onnx";
    public const string Revision = "2a5f66ac53dfc1c817e75609d9903d75b4ec5895";
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache", "elbruno-speech", ModelId);

    internal static readonly ModelAsset[] Assets =
    [
        new("config.json", 97, "666903c76b9798caf2c210afd4f6cd60b08a8dbf9800ec8d7a3bc0d2148ac466"),
        new("vocab.txt", 93939, "d58544679ea4bc6ac563d1f545eb7d474bd6cfa467f0a6e2c1dc1c7d37e3c35d"),
        new("nemo128.onnx", 139764, "a9fde1486ebfcc08f328d75ad4610c67835fea58c73ba57e3209a6f6cf019e9f"),
        new("encoder-model.int8.onnx", 652183999, "6139d2fa7e1b086097b277c7149725edbab89cc7c7ae64b23c741be4055aff09"),
        new("decoder_joint-model.int8.onnx", 18202004, "eea7483ee3d1a30375daedc8ed83e3960c91b098812127a0d99d1c8977667a70")
    ];

    /// <summary>Explicitly downloads the pinned model, verifying SHA-256 and size before replacing files.
    /// Existing invalid files are reported, never silently accepted.</summary>
    public static async Task DownloadAsync(string? directory = null, CancellationToken cancellationToken = default)
    {
        directory ??= DefaultDirectory;
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        foreach (var asset in Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(directory, asset.Name);
            if (File.Exists(path))
            {
                VerifyAsset(path, asset);
                continue;
            }
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".download";
            try
            {
                using var response = await http.GetAsync(
                    $"https://huggingface.co/{Repository}/resolve/{Revision}/{asset.Name}",
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
                        if (length > asset.Size)
                            throw new InvalidDataException($"Model asset {asset.Name} exceeds its expected size.");
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }
                }
                VerifyAsset(temporary, asset);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    internal static void VerifyDirectory(string directory)
    {
        foreach (var asset in Assets)
            VerifyAsset(Path.Combine(directory, asset.Name), asset);
    }

    internal static void VerifyAsset(string path, ModelAsset asset)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length != asset.Size ||
            !Convert.ToHexString(SHA256.HashData(stream)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Model asset {asset.Name} failed size/SHA-256 validation. Remove the corrupt file and download again.");
    }
}

internal sealed record ModelAsset(string Name, long Size, string Sha256);
