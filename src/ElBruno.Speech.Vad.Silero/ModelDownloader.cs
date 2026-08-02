namespace ElBruno.Speech.Vad.Silero;

/// <summary>Downloads the Silero VAD ONNX model to the local cache on first use.</summary>
internal static class ModelDownloader
{
    private const string ModelUrl =
        "https://huggingface.co/onnx-community/silero-vad/resolve/main/onnx/model.onnx";

    private static readonly string DefaultCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache", "elbruno-speech", "models");

    public static readonly string DefaultModelPath = Path.Combine(
        DefaultCacheDir, "silero_vad_v4.onnx");

    /// <summary>
    /// Ensures the model file exists at <paramref name="modelPath"/>,
    /// downloading it if necessary.
    /// </summary>
    public static async Task EnsureModelAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(modelPath)) return;

        var dir = Path.GetDirectoryName(modelPath)!;
        Directory.CreateDirectory(dir);

        var tempPath = modelPath + ".download";
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "ElBruno.Speech/1.0");

            using var response = await http.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var src = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var dst = File.Create(tempPath);
            await src.CopyToAsync(dst, cancellationToken);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }

        File.Move(tempPath, modelPath, overwrite: true);
    }
}
