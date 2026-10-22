namespace ElBruno.Speech.Stt.Parakeet;

internal sealed record DecoderStep(float[] Logits, float[] State1, float[] State2);
internal sealed record DecodedToken(int Id, int Frame);

internal static class TdtDecoder
{
    public static List<DecodedToken> Decode(int frames, int vocabularySize,
        Func<int, int, float[], float[], DecoderStep> evaluate, CancellationToken cancellationToken)
    {
        int blank = vocabularySize - 1;
        int previous = blank, frame = 0, emitted = 0;
        float[] state1 = new float[1280], state2 = new float[1280];
        var tokens = new List<DecodedToken>();
        while (frame < frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = evaluate(frame, previous, state1, state2);
            if (step.Logits.Length != vocabularySize + 5)
                throw new InvalidDataException("Expected Parakeet v3 vocabulary and five duration logits.");
            int token = ArgMax(step.Logits.AsSpan(0, vocabularySize));
            int duration = ArgMax(step.Logits.AsSpan(vocabularySize));
            if (token != blank)
            {
                tokens.Add(new DecodedToken(token, frame));
                previous = token;
                state1 = step.State1;
                state2 = step.State2;
                emitted++;
            }
            // Blank predictions must not commit recurrent state. Zero-duration loops
            // are bounded as in the reference TDT greedy decoder.
            if (duration > 0)
            {
                frame += duration;
                emitted = 0;
            }
            else if (token == blank || emitted == 10)
            {
                frame++;
                emitted = 0;
            }
        }
        return tokens;
    }

    private static int ArgMax(ReadOnlySpan<float> values)
    {
        int best = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (!float.IsFinite(values[i]))
                throw new InvalidDataException("The model returned non-finite logits.");
            if (values[i] > values[best]) best = i;
        }
        return best;
    }
}
