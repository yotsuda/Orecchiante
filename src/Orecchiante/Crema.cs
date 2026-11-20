using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Orecchiante;

/// <summary>The chords of a recording as crema's model hears them: 44100 Hz -> the constant-Q magnitudes of two
/// harmonics, in dB under the loudest (pumpp's HCQTMag) -> the model (ONNX) -> a probability for each of 170
/// chords, every 4096 samples. What tools/crema_chords.py does with TensorFlow.</summary>
static class Crema
{
    public const int Rate = 44100, Hop = 4096, Bins = 216, PerOctave = 36, Chords = 170;
    public const double Step = (double)Hop / Rate;
    static readonly double C1 = 440 * Math.Pow(2, (24 - 69) / 12.0);

    static readonly string[] Roots = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    static readonly string[] Qualities = ["min", "maj", "dim", "aug", "min6", "maj6", "min7", "minmaj7", "maj7", "7", "dim7", "hdim7", "sus2", "sus4"];
    /// <summary>The chords in the order tools/read_chords.py folds them from: root by root, then "X" and "N".</summary>
    public static readonly string[] Names = [.. Roots.SelectMany(r => Qualities.Select(q => $"{r}:{q}")), "X", "N"];
    /// <summary>Where the model's n-th class is among <see cref="Names"/>: the model has them in the order of their letters.</summary>
    static readonly int[] Where = [.. Names.Order(StringComparer.Ordinal).Select(tag => Array.IndexOf(Names, tag))];

    /// <summary>The model's input, [frame][bin][harmonic] in one row: dB, 0 for the loudest of a harmonic and no lower than -80.</summary>
    public static float[] Input(double[] mono, out int frames)
    {
        int count = frames = mono.Length / Hop;
        var input = new float[count * Bins * 2];
        for (int h = 0; h < 2; h++)
        {
            var mag = Cqt.Magnitude(mono, Rate, Hop, C1 * (h + 1), Bins, PerOctave);
            int have = Math.Min(count, mag[0].Length);
            float loudest = 0;
            for (int k = 0; k < Bins; k++)
                for (int t = 0; t < have; t++) loudest = Math.Max(loudest, mag[k][t]);
            const double least = 1e-5;
            double reference = 20 * Math.Log10(Math.Max(least, loudest));
            double quietest = 20 * Math.Log10(least) - reference, floor = -80;      // 80 dB under the loudest, which is at 0
            for (int t = 0; t < count; t++)
                for (int k = 0; k < Bins; k++)
                {
                    double db = t < have ? 20 * Math.Log10(Math.Max(least, mag[k][t])) - reference : quietest;
                    input[(t * Bins + k) * 2 + h] = (float)Math.Max(db, floor);
                }
        }
        return input;
    }

    /// <summary>log p of each of <see cref="Names"/>, [frame * 170 + chord].</summary>
    public static float[] LogP(InferenceSession session, float[] input, int frames)
    {
        var tensor = new DenseTensor<float>(input, new[] { 1, frames, Bins, 2 });
        using var results = session.Run([NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), tensor)], ["chord_tag"]);
        var prob = results[0].AsEnumerable<float>().ToArray();
        if (prob.Length != frames * Chords) throw new InvalidDataException($"{prob.Length} values for {frames} frames of {Chords} chords");
        var logp = new float[prob.Length];
        for (int t = 0; t < frames; t++)
            for (int c = 0; c < Chords; c++) logp[t * Chords + Where[c]] = MathF.Log(MathF.Max(prob[t * Chords + c], 1e-12f));
        return logp;
    }
}
