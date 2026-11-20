using System.Globalization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Orecchiante;

/// <summary>The beats and bar lines of a recording as Beat This! hears them: 22050 Hz -> log mel -> the model
/// (ONNX) -> peaks. What tools/track_beats.py does with torch, and tools/d2/ears_onnx.py with numpy.</summary>
static class Beats
{
    public const int Rate = 22050;
    const int Frame = 1024, Hop = 441, Bands = 128, Bins = Frame / 2 + 1;
    const int Size = 1500, Border = 6;
    static readonly double[] Window = [.. Enumerable.Range(0, Frame).Select(i => 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / Frame))];
    static readonly float[] Bank = MakeBank();      // [bin * Bands + band]

    /// <summary>The lines of a .beats file: a beat's time, its number in the bar (1 on a bar line, 0 before the first),
    /// and the downbeat logit there (2026-11-17: which of the lines of bars of half the beats begin bars).</summary>
    public static string Lines(InferenceSession session, double[] mono)
    {
        var spect = LogMel(mono, out int frames);
        var (beat, down) = Logits(session, spect, frames);
        var beats = Peaks(beat);
        var downs = Peaks(down).Select(d => beats.Length == 0 ? d : beats.MinBy(b => Math.Abs(b - d))).Distinct().Order().ToArray();
        var lines = new System.Text.StringBuilder();
        int number = 0;
        foreach (var t in beats)
        {
            bool isDown = downs.Length > 0 && downs.Min(d => Math.Abs(d - t)) < 0.035;
            number = isDown ? 1 : number > 0 ? number + 1 : 0;
            lines.Append(string.Create(CultureInfo.InvariantCulture, $"{t:F3}\t{number}\t{AtBeat(down, t):F2}\n"));
        }
        return lines.ToString();
    }

    /// <summary>How sure the model is that a bar begins at the beat at <paramref name="t"/> seconds: the largest downbeat
    /// logit within 2 frames of it (tools/track_beats.py's at_beat).</summary>
    static float AtBeat(float[] down, double t)
    {
        int i = (int)Math.Round(t * 50, MidpointRounding.ToEven);
        float best = float.NegativeInfinity;
        for (int j = Math.Max(i - 2, 0); j <= Math.Min(i + 2, down.Length - 1); j++) best = Math.Max(best, down[j]);
        return best;
    }

    static double ToMel(double hz) => hz >= 1000 ? 15 + Math.Log(hz / 1000) / (Math.Log(6.4) / 27) : hz * 3 / 200;
    static double ToHz(double mel) => mel >= 15 ? 1000 * Math.Exp(Math.Log(6.4) / 27 * (mel - 15)) : mel * 200 / 3;

    static float[] MakeBank()
    {
        double low = ToMel(30), high = ToMel(11000);
        var points = Enumerable.Range(0, Bands + 2).Select(i => ToHz(low + (high - low) * i / (Bands + 1))).ToArray();
        var bank = new float[Bins * Bands];
        for (int k = 0; k < Bins; k++)
        {
            double f = 11025.0 * k / (Bins - 1);
            for (int m = 0; m < Bands; m++)
            {
                double down = (f - points[m]) / (points[m + 1] - points[m]), up = (points[m + 2] - f) / (points[m + 2] - points[m + 1]);
                bank[k * Bands + m] = (float)Math.Max(0, Math.Min(down, up));
            }
        }
        return bank;
    }

    /// <summary>Beat This!'s input: frames * 128, log(1 + 1000 * mel) of the magnitudes of centred, reflected frames.</summary>
    static float[] LogMel(double[] signal, out int frames)
    {
        int pad = Frame / 2, n = signal.Length;
        int count = frames = 1 + n / Hop;
        var spect = new float[count * Bands];
        var fft = new Fft(Frame);
        Parallel.For(0, count, () => (new double[Frame], new double[Frame], new float[Bins]), (t, _, buffers) =>
        {
            var (re, im, mag) = buffers;
            for (int i = 0; i < Frame; i++)
            {
                int j = t * Hop + i - pad;
                if (j < 0) j = -j; else if (j >= n) j = 2 * (n - 1) - j;
                re[i] = (float)signal[j] * Window[i];
                im[i] = 0;
            }
            fft.Forward(re, im);
            for (int k = 0; k < Bins; k++) mag[k] = (float)(Math.Sqrt(re[k] * re[k] + im[k] * im[k]) / 32.0);
            for (int m = 0; m < Bands; m++)
            {
                float sum = 0;
                for (int k = 0; k < Bins; k++) sum += mag[k] * Bank[k * Bands + m];
                spect[t * Bands + m] = MathF.Log(1 + 1000 * sum);
            }
            return buffers;
        }, _ => { });
        return spect;
    }

    /// <summary>beat_this.inference.split_predict_aggregate, keep_first: 30 s chunks, 6 frames of each edge dropped.</summary>
    static (float[] Beat, float[] Down) Logits(InferenceSession session, float[] spect, int frames)
    {
        var starts = new List<int>();
        for (int s = -Border; s < frames - Border; s += Size - 2 * Border) starts.Add(s);
        if (frames > Size - 2 * Border) starts[^1] = frames - (Size - Border);
        var beat = Enumerable.Repeat(-1000f, frames).ToArray();
        var down = Enumerable.Repeat(-1000f, frames).ToArray();
        foreach (int start in Enumerable.Reverse(starts))
        {
            int from = Math.Max(start, 0), to = Math.Min(start + Size, frames);
            int left = Math.Max(0, -start), right = Math.Max(0, Math.Min(Border, start + Size - frames));
            int length = left + (to - from) + right;
            var chunk = new DenseTensor<float>(new[] { 1, length, Bands });
            spect.AsSpan(from * Bands, (to - from) * Bands).CopyTo(chunk.Buffer.Span[(left * Bands)..]);
            using var results = session.Run([NamedOnnxValue.CreateFromTensor("spect", chunk)]);
            var b = results.First(r => r.Name == "beat").AsEnumerable<float>().ToArray();
            var d = results.First(r => r.Name == "downbeat").AsEnumerable<float>().ToArray();
            for (int i = Border; i < length - Border; i++)
            {
                int at = start + i;
                if (at < 0 || at >= frames) continue;
                beat[at] = b[i]; down[at] = d[i];
            }
        }
        return (beat, down);
    }

    /// <summary>The largest within 3 frames either side and above 0; neighbouring peaks averaged; in seconds (50 frames a second).</summary>
    static double[] Peaks(float[] logit)
    {
        var found = new List<int>();
        for (int i = 0; i < logit.Length; i++)
        {
            if (logit[i] <= 0) continue;
            bool top = true;
            for (int j = Math.Max(0, i - 3); j <= Math.Min(logit.Length - 1, i + 3) && top; j++) top = logit[j] <= logit[i];
            if (top) found.Add(i);
        }
        if (found.Count == 0) return [];
        var result = new List<double>();
        double p = found[0]; int c = 1;
        foreach (int q in found.Skip(1))
        {
            if (q - p <= 1) { c++; p += (q - p) / c; }
            else { result.Add(p); p = q; c = 1; }
        }
        result.Add(p);
        return [.. result.Select(f => f / 50.0)];
    }
}
