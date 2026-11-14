namespace Dettato;

/// <summary>The constant-Q transform as librosa 1.0 computes it (librosa.cqt with its defaults: Hann windows,
/// filters of unit L1 norm, the smallest hundredth of each filter's spectrum dropped, zeros beyond the ends,
/// lengths scaled out). One octave of filters is laid over the spectra of 2^k-sample frames, the signal is
/// halved in rate, and the same filters read the octave below. tools/net_check.py compares it with librosa's.</summary>
static class Cqt
{
    const double HannBandwidth = 1.50018310546875;      // librosa.filters.WINDOW_BANDWIDTHS["hann"]
    const double Sparsity = 0.01;

    /// <summary>The magnitudes, [bin][frame], of <paramref name="bins"/> filters from <paramref name="lowest"/> Hz up,
    /// <paramref name="perOctave"/> to the octave, every <paramref name="hop"/> samples of <paramref name="y"/>.</summary>
    public static float[][] Magnitude(double[] y, double rate, int hop, double lowest, int bins, int perOctave)
    {
        var freqs = new double[bins];
        for (int k = 0; k < bins; k++) freqs[k] = lowest * Math.Pow(2, (double)k / perOctave);
        var alpha = RelativeBandwidth(freqs);
        double cutoff = 0;
        for (int k = 0; k < bins; k++) cutoff = Math.Max(cutoff, freqs[k] * (1 + 0.5 * HannBandwidth * alpha[k]));
        if (cutoff > rate / 2) throw new ArgumentException($"filters up to {cutoff:F0} Hz do not fit under {rate / 2:F0} Hz");
        int octaves = (bins + perOctave - 1) / perOctave, filters = Math.Min(perOctave, bins);

        // The rate may come down before the first octave, while the hop still halves once an octave after it.
        int early = Math.Min(Math.Max(0, (int)Math.Ceiling(Math.Log2(rate / 2 / cutoff)) - 2),
                             Math.Max(0, TwoFactors(hop) - octaves + 1));
        if (early > 0)
        {
            int factor = 1 << early;
            if (y.Length < factor) throw new ArgumentException("too short a signal");
            y = Resample.To(y, 1, factor, Math.Sqrt(factor));
            hop /= factor;
            rate /= factor;
        }

        var result = new float[bins][];
        double myRate = rate;
        int myHop = hop, frames = int.MaxValue;
        for (int i = 0; i < octaves; i++)
        {
            int from = Math.Max(0, bins - filters * (i + 1)), to = bins - filters * i;
            var basis = Basis(freqs, alpha, from, to, myRate, Math.Sqrt(rate / myRate), out int size);
            var response = Respond(y, size, myHop, basis);
            for (int k = from; k < to; k++)
            {
                float scale = (float)(1 / Math.Sqrt(rate / (alpha[k] * freqs[k])));     // 1 / sqrt(the filter's length)
                var row = result[k] = response[k - from];
                for (int t = 0; t < row.Length; t++) row[t] *= scale;
            }
            frames = Math.Min(frames, response[0].Length);
            if (i < octaves - 1 && from > 0 && myHop % 2 == 0 && freqs[from - 1] <= myRate / 5)
            {
                myHop /= 2;
                myRate /= 2;
                y = Resample.To(y, 1, 2, Math.Sqrt(2));
            }
        }
        for (int k = 0; k < bins; k++)
            if (result[k].Length != frames) Array.Resize(ref result[k], frames);
        return result;
    }

    /// <summary>librosa.filters._relative_bandwidth: (r^2 - 1) / (r^2 + 1) for a step of r from a filter to the next.</summary>
    static double[] RelativeBandwidth(double[] freqs)
    {
        int n = freqs.Length;
        if (n == 1) throw new ArgumentException("two filters or more");
        var alpha = new double[n];
        for (int k = 0; k < n; k++)
        {
            int below = Math.Max(0, k - 1), above = Math.Min(n - 1, k + 1);
            double octave = (Math.Log2(freqs[above]) - Math.Log2(freqs[below])) / (above - below);
            double square = Math.Pow(2, 2 * octave);
            alpha[k] = (square - 1) / (square + 1);
        }
        return alpha;
    }

    static int TwoFactors(int x)
    {
        int twos = 0;
        for (; x > 0 && x % 2 == 0; x /= 2) twos++;
        return twos;
    }

    /// <summary>What is left of a filter's spectrum: the bins kept and their values.</summary>
    readonly record struct Filter(int[] At, float[] Re, float[] Im);

    /// <summary>The filters <paramref name="from"/>..<paramref name="to"/> at a rate, as spectra over frames of
    /// <paramref name="size"/> samples (the next power of two above the longest of them).</summary>
    static Filter[] Basis(double[] freqs, double[] alpha, int from, int to, double rate, double gain, out int size)
    {
        var lengths = new double[to - from];
        for (int k = from; k < to; k++) lengths[k - from] = rate / (alpha[k] * freqs[k]);
        int n = size = 1 << (int)Math.Ceiling(Math.Log2(lengths.Max()));
        var fft = new Fft(n);
        var basis = new Filter[to - from];
        Parallel.For(from, to, k =>
        {
            double length = lengths[k - from];
            int start = (int)Math.Floor(-length / 2), count = (int)Math.Floor(length / 2) - start, pad = (n - count) / 2;
            double[] re = new double[n], im = new double[n];
            double norm = 0;
            for (int j = 0; j < count; j++) norm += 0.5 - 0.5 * Math.Cos(2 * Math.PI * j / count);
            for (int j = 0; j < count; j++)
            {
                double window = (0.5 - 0.5 * Math.Cos(2 * Math.PI * j / count)) / norm * length / n;
                double angle = (start + j) * 2 * Math.PI * freqs[k] / rate;
                re[pad + j] = window * Math.Cos(angle);
                im[pad + j] = window * Math.Sin(angle);
            }
            fft.Forward(re, im);

            // librosa.util.sparsify_rows: the smallest values that are together under a hundredth of the row go.
            int kept = n / 2 + 1;
            var mags = new double[kept];
            double total = 0;
            for (int b = 0; b < kept; b++) total += mags[b] = Math.Sqrt(re[b] * re[b] + im[b] * im[b]);
            var sorted = (double[])mags.Clone();
            Array.Sort(sorted);
            double threshold = sorted[^1], running = 0;
            for (int b = 0; b < kept; b++)
            {
                running += sorted[b] / total;
                if (!(running < Sparsity)) { threshold = sorted[b]; break; }
            }
            var at = Enumerable.Range(0, kept).Where(b => mags[b] >= threshold).ToArray();
            basis[k - from] = new Filter(at, [.. at.Select(b => (float)(re[b] * gain))], [.. at.Select(b => (float)(im[b] * gain))]);
        });
        return basis;
    }

    /// <summary>The filters over the spectra of frames of <paramref name="size"/> samples centred every
    /// <paramref name="hop"/> samples, zeros beyond the ends: [filter][frame], magnitudes.</summary>
    static float[][] Respond(double[] y, int size, int hop, Filter[] basis)
    {
        int n = y.Length, frames = 1 + n / hop, half = size / 2;
        var fft = new Fft(size);
        var response = new float[basis.Length][];
        for (int k = 0; k < basis.Length; k++) response[k] = new float[frames];
        Parallel.For(0, frames, () => (new double[size], new double[size]), (t, _, buffers) =>
        {
            var (re, im) = buffers;
            int first = t * hop - half;
            for (int i = 0; i < size; i++)
            {
                int j = first + i;
                re[i] = j >= 0 && j < n ? (float)y[j] : 0;
                im[i] = 0;
            }
            fft.Forward(re, im);
            for (int k = 0; k < basis.Length; k++)
            {
                var (at, fr, fi) = basis[k];
                double sumRe = 0, sumIm = 0;
                for (int b = 0; b < at.Length; b++)
                {
                    double dr = re[at[b]], di = im[at[b]];
                    sumRe += fr[b] * dr - fi[b] * di;
                    sumIm += fr[b] * di + fi[b] * dr;
                }
                response[k][t] = (float)Math.Sqrt(sumRe * sumRe + sumIm * sumIm);
            }
            return buffers;
        }, _ => { });
        return response;
    }
}
