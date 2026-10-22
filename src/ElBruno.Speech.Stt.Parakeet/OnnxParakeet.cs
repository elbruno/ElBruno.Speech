using System.Globalization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.Speech.Stt.Parakeet;

internal sealed class OnnxParakeet : IDisposable
{
    private readonly InferenceSession _preprocessor;
    private readonly InferenceSession _encoder;
    private readonly InferenceSession _decoder;
    private readonly string[] _vocabulary;

    public OnnxParakeet(string directory)
    {
        ParakeetModel.VerifyDirectory(directory);
        _vocabulary = File.ReadAllLines(Path.Combine(directory, "vocab.txt"))
            .Select((line, index) =>
            {
                int separator = line.LastIndexOf(' ');
                if (separator < 1 || !int.TryParse(line.AsSpan(separator + 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int id) || id != index)
                    throw new InvalidDataException("Invalid vocabulary token index.");
                return line[..separator];
            }).ToArray();
        if (_vocabulary.Length != 8193 || _vocabulary[^1] != "<blk>")
            throw new InvalidDataException("Expected the multilingual Parakeet v3 vocabulary.");

        using var options = new SessionOptions();
        _preprocessor = new InferenceSession(Path.Combine(directory, "nemo128.onnx"), options);
        try
        {
            _encoder = new InferenceSession(Path.Combine(directory, "encoder-model.int8.onnx"), options);
            try
            {
                _decoder = new InferenceSession(Path.Combine(directory, "decoder_joint-model.int8.onnx"), options);
            }
            catch
            {
                _encoder.Dispose();
                throw;
            }
        }
        catch
        {
            _preprocessor.Dispose();
            throw;
        }
    }

    public IReadOnlyList<ParakeetToken> Recognize(float[] samples, CancellationToken cancellationToken)
    {
        using var run = new RunOptions();
        using var registration = cancellationToken.Register(() => run.Terminate = true);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var features = _preprocessor.Run(
                [Input("waveforms", samples, [1, samples.Length]),
                 Input("waveforms_lens", new long[] { samples.Length }, [1])],
                ["features", "features_lens"], run);
            using var encoded = _encoder.Run(
                [NamedOnnxValue.CreateFromTensor("audio_signal", features[0].AsTensor<float>()),
                 NamedOnnxValue.CreateFromTensor("length", features[1].AsTensor<long>())],
                ["outputs", "encoded_lengths"], run);
            var tensor = encoded[0].AsTensor<float>();
            int frames = checked((int)encoded[1].AsTensor<long>()[0]);
            if (tensor.Dimensions.Length != 3 || tensor.Dimensions[0] != 1 ||
                tensor.Dimensions[1] != 1024 || frames < 0 || frames > tensor.Dimensions[2])
                throw new InvalidDataException("Unexpected Parakeet encoder shape.");
            float[] frameValues = new float[1024];
            var tokens = TdtDecoder.Decode(frames, _vocabulary.Length, (frame, previous, state1, state2) =>
            {
                for (int channel = 0; channel < frameValues.Length; channel++)
                    frameValues[channel] = tensor[0, channel, frame];
                using var output = _decoder.Run(
                    [Input("encoder_outputs", frameValues, [1, 1024, 1]),
                     Input("targets", new[] { previous }, [1, 1]),
                     Input("target_length", new[] { 1 }, [1]),
                     Input("input_states_1", state1, [2, 1, 640]),
                     Input("input_states_2", state2, [2, 1, 640])],
                    ["outputs", "output_states_1", "output_states_2"], run);
                return new DecoderStep(output[0].AsEnumerable<float>().ToArray(),
                    output[1].AsEnumerable<float>().ToArray(), output[2].AsEnumerable<float>().ToArray());
            }, cancellationToken);
            return tokens.Select(token => new ParakeetToken(_vocabulary[token.Id],
                TimeSpan.FromSeconds(token.Frame * 0.08))).ToArray();
        }
        catch (OnnxRuntimeException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static NamedOnnxValue Input<T>(string name, T[] values, int[] dimensions) =>
        NamedOnnxValue.CreateFromTensor(name, new DenseTensor<T>(values, dimensions));

    public void Dispose()
    {
        _decoder.Dispose();
        _encoder.Dispose();
        _preprocessor.Dispose();
    }
}
