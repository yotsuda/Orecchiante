using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Orecchiante;

/// <summary>The chords of a recording as BTC hears them (Park et al., ISMIR 2019; MIT), the second ear (the owner,
/// 2026-11-16): 22050 Hz, cut in 10-second pieces -> log(|CQT| + 1e-6) of each (144 bins from C1, 24 to the octave,
/// every 2048 samples) -> the model (ONNX, tools/d2/btc_onnx.py) a window of 108 frames at a time -> log p of each of
/// 170 chords, in the order of <see cref="Crema.Names"/>. What tools/btc_chords.py does with torch.</summary>
static class Btc
{
    public const int Rate = 22050, Hop = 2048, Bins = 144, PerOctave = 24, Window = 108, Chords = 170;
    const int Piece = Rate * 10;
    /// <summary>A frame's seconds as BTC's own code counts them (ten seconds over 108 frames).</summary>
    public const double Step = 10.0 / Window;
    static readonly double C1 = 440 * Math.Pow(2, (24 - 69) / 12.0);

    /// <summary>The model's input, [frame * 144 + bin].</summary>
    public static float[] Input(double[] y, out int frames)
    {
        var pieces = new List<float[][]>();
        int start = 0;
        while (y.Length > start + Piece)
        {
            pieces.Add(Cqt.Magnitude(y[start..(start + Piece)], Rate, Hop, C1, Bins, PerOctave));
            start += Piece;
        }
        pieces.Add(Cqt.Magnitude(y[start..], Rate, Hop, C1, Bins, PerOctave));
        frames = pieces.Sum(p => p[0].Length);
        var input = new float[frames * Bins];
        int at = 0;
        foreach (var mag in pieces)
        {
            for (int t = 0; t < mag[0].Length; t++, at++)
                for (int k = 0; k < Bins; k++) input[at * Bins + k] = MathF.Log(mag[k][t] + 1e-6f);
        }
        return input;
    }

    /// <summary>log p of each of the 170 chords, [frame * 170 + chord]: the frames in windows of 108, the last one
    /// filled out with zeros, as BTC's own code does.</summary>
    public static float[] LogP(InferenceSession session, float[] input, int frames)
    {
        int windows = frames / Window + 1;
        var logp = new float[frames * Chords];
        var window = new float[Window * Bins];
        for (int w = 0; w < windows; w++)
        {
            int first = w * Window, have = Math.Min(Window, frames - first);
            if (have <= 0) break;
            Array.Clear(window);
            Array.Copy(input, first * Bins, window, 0, have * Bins);
            var tensor = new DenseTensor<float>(window, new[] { 1, Window, Bins });
            using var results = session.Run([NamedOnnxValue.CreateFromTensor("feature", tensor)]);
            var said = results[0].AsEnumerable<float>().ToArray();
            Array.Copy(said, 0, logp, first * Chords, have * Chords);
        }
        return logp;
    }

    /// <summary>Two ears: log of the mean of crema's and BTC's probabilities, on crema's frames (each takes the BTC frame
    /// its middle falls in), as tools/two_ears.py; both as the files hold them (half precision) and the mean too.</summary>
    public static float[] Average(float[] first, int frames, double step, float[] second, int secondFrames, double secondStep)
    {
        var both = new float[frames * Chords];
        for (int t = 0; t < frames; t++)
        {
            int u = (int)((t + 0.5) * step / secondStep);
            u = Math.Clamp(u, 0, secondFrames - 1);
            for (int c = 0; c < Chords; c++)
            {
                double p = Math.Exp((float)(Half)first[t * Chords + c]), q = Math.Exp((float)(Half)second[u * Chords + c]);
                both[t * Chords + c] = (float)(Half)Math.Log(Math.Max((p + q) / 2, 1e-12));
            }
        }
        return both;
    }
}
