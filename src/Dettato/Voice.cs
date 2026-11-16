using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Dettato;

/// <summary>How loud the singing is, every 10 ms: the vocals Demucs (htdemucs, MIT) separates from the recording.
/// Its network is an ONNX file without the STFT and the inverse STFT (tools/d2/demucs_onnx.py), which are done
/// here; the recording is split and put back together as demucs.apply.apply_model does with shifts=0 (7.8 s
/// segments, a quarter overlapping, weighed by a triangle). The channels are one: the recording is read in mono
/// and given to both. The sections take where singing begins and stops from it (Form.Phrases).</summary>
static class Voice
{
    public const string Model = "htdemucs-mm8.onnx";     // its matrix products in int8 (85 MB; tools/d2/demucs_onnx.py)
    public const int Parallel = 3;      // segments at once, each on the session's 8 threads (2 at once or 6 of 4 threads were slower)
    public const int Rate = 44100;
    public const int Hop10ms = Rate / 100;
    const int Segment = 343980;         // int(44100 * 39 / 5), the length the network was trained on
    const int Nfft = 4096, Hop = 1024, Bins = Nfft / 2;
    const int Frames = (Segment + Hop - 1) / Hop;   // 336
    const int Vocals = 3;               // drums, bass, other, vocals
    const double Overlap = 0.25;

    static readonly double[] Window = Enumerable.Range(0, Nfft).Select(n => 0.5 - 0.5 * Math.Cos(2 * Math.PI * n / Nfft)).ToArray();

    /// <summary>The vocals' root mean square every 10 ms of <paramref name="mono"/> (at 44100 Hz).</summary>
    public static float[] Loudness(InferenceSession model, double[] mono, int parallel = 1)
    {
        int n = mono.Length;
        double mean = 0;
        foreach (double v in mono) mean += v;
        mean /= n;
        double sq = 0;
        foreach (double v in mono) sq += (v - mean) * (v - mean);
        double std = Math.Sqrt(sq / (n - 1));
        var x = new float[n];
        for (int i = 0; i < n; i++) x[i] = (float)((mono[i] - mean) / std);

        int stride = (int)((1 - Overlap) * Segment);
        var weight = new double[Segment];
        for (int i = 0; i < Segment / 2; i++) weight[i] = i + 1;
        for (int i = Segment / 2; i < Segment; i++) weight[i] = Segment - i;
        double top = weight.Max();
        for (int i = 0; i < Segment; i++) weight[i] /= top;

        var offsets = new List<int>();
        for (int o = 0; o < n; o += stride) offsets.Add(o);
        var outs = new float[offsets.Count][];
        System.Threading.Tasks.Parallel.For(0, offsets.Count, new ParallelOptions { MaxDegreeOfParallelism = parallel }, k =>
        {
            int offset = offsets[k], length = Math.Min(Segment, n - offset);
            // TensorChunk.padded: centred on the chunk, with the recording around it where there is some
            int delta = Segment - length, start = offset - delta / 2;
            var chunk = new float[Segment];
            for (int i = 0; i < Segment; i++)
            {
                int j = start + i;
                if (j >= 0 && j < n) chunk[i] = x[j];
            }
            var sung = Separate(model, chunk);
            var trimmed = new float[length];         // center_trim
            Array.Copy(sung, delta / 2, trimmed, 0, length);
            outs[k] = trimmed;
        });
        var sum = new double[n];
        var weights = new double[n];
        for (int k = 0; k < offsets.Count; k++)
        {
            int offset = offsets[k];
            for (int i = 0; i < outs[k].Length; i++)
            {
                sum[offset + i] += weight[i] * outs[k][i];
                weights[offset + i] += weight[i];
            }
        }
        int frames = n / Hop10ms;
        var rms = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            double s = 0;
            for (int i = f * Hop10ms; i < (f + 1) * Hop10ms; i++)
            {
                double v = sum[i] / weights[i] * std + mean;
                s += v * v;
            }
            rms[f] = (float)Math.Sqrt(s / Hop10ms);
        }
        return rms;
    }

    /// <summary>One segment of the mix (normalized) to the vocals, both channels averaged.</summary>
    static float[] Separate(InferenceSession model, float[] chunk)
    {
        // HTDemucs._spec: reflect by 3/8 of a window each side (and up to whole hops), then torch.stft centred
        // (reflect by half a window), normalized; the last bin and the first and last two frames are dropped
        int pad = Hop / 2 * 3;
        var padded = Reflect(chunk, pad, pad + Frames * Hop - Segment);
        var centred = Reflect(padded, Nfft / 2, Nfft / 2);
        var mag = new float[4 * Bins * Frames];
        var fft = new Fft(Nfft);
        var re = new double[Nfft];
        var im = new double[Nfft];
        double norm = 1.0 / Math.Sqrt(Nfft);
        for (int t = 0; t < Frames; t++)
        {
            int at = (t + 2) * Hop;
            for (int i = 0; i < Nfft; i++) { re[i] = centred[at + i] * Window[i]; im[i] = 0; }
            fft.Forward(re, im);
            for (int b = 0; b < Bins; b++)
            {
                float r = (float)(re[b] * norm), m = (float)(im[b] * norm);
                for (int c = 0; c < 2; c++)         // [channel 0 real, channel 0 imaginary, channel 1 real, …]
                {
                    mag[((2 * c) * Bins + b) * Frames + t] = r;
                    mag[((2 * c + 1) * Bins + b) * Frames + t] = m;
                }
            }
        }
        var mix = new float[2 * Segment];
        Array.Copy(chunk, 0, mix, 0, Segment);
        Array.Copy(chunk, 0, mix, Segment, Segment);
        using var results = model.Run([
            NamedOnnxValue.CreateFromTensor("mix", new DenseTensor<float>(mix, [1, 2, Segment])),
            NamedOnnxValue.CreateFromTensor("mag", new DenseTensor<float>(mag, [1, 4, Bins, Frames])),
        ]);
        var spec = results.First(r => r.Name == "spec").AsTensor<float>();
        var wave = results.First(r => r.Name == "wave").AsTensor<float>();

        // HTDemucs._ispec of the vocals: a zero bin on top and two empty frames each side, torch.istft centred and
        // normalized, then the 3/8 of a window each side taken off
        int framesOut = Frames + 4;
        var output = new float[Segment];
        for (int c = 0; c < 2; c++)
        {
            int full = Nfft + Hop * (framesOut - 1);
            var signal = new double[full];
            var envelope = new double[full];
            for (int t = 0; t < framesOut; t++)
            {
                int tt = t - 2;
                if (tt >= 0 && tt < Frames)
                {
                    for (int b = 0; b < Bins; b++) { re[b] = spec[0, Vocals, 2 * c, b, tt]; im[b] = spec[0, Vocals, 2 * c + 1, b, tt]; }
                    re[Bins] = 0; im[Bins] = 0;
                    for (int b = 1; b < Bins; b++) { re[Nfft - b] = re[b]; im[Nfft - b] = -im[b]; }
                    // the inverse transform: conjugate, forward, conjugate, over n; normalized: times sqrt(n)
                    for (int i = 0; i < Nfft; i++) im[i] = -im[i];
                    fft.Forward(re, im);
                    double scale = Math.Sqrt(Nfft) / Nfft;
                    for (int i = 0; i < Nfft; i++) signal[t * Hop + i] += re[i] * scale * Window[i];
                }
                for (int i = 0; i < Nfft; i++) envelope[t * Hop + i] += Window[i] * Window[i];
            }
            // torch.istft(center=True, length=le): from half a window in, le samples
            for (int i = 0; i < Segment; i++)
            {
                int j = Nfft / 2 + pad + i;
                double v = envelope[j] > 1e-11 ? signal[j] / envelope[j] : 0;
                output[i] += (float)((v + wave[0, Vocals, c, i]) / 2);
            }
        }
        return output;
    }

    /// <summary>numpy's and torch's reflect padding (the edge sample not repeated).</summary>
    static float[] Reflect(float[] x, int left, int right)
    {
        int n = x.Length;
        var y = new float[n + left + right];
        for (int i = 0; i < y.Length; i++)
        {
            int j = i - left;
            if (j < 0) j = -j;
            if (j >= n) j = 2 * (n - 1) - j;
            y[i] = x[j];
        }
        return y;
    }
}
