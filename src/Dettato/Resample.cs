using System.Numerics;

namespace Dettato;

/// <summary>A signal at another rate: a low-pass (windowed sinc, Kaiser) read at the new sample times.
/// The pass band ends at 0.913 of the lower of the two Nyquist frequencies and the stop band begins at it,
/// as soxr's "HQ" has them; the samples are not soxr's (docs/HANDOFF.md: what that moves).</summary>
static class Resample
{
    const double Pass = 0.913, Stop = 1.0, Attenuation = 120;

    /// <summary><paramref name="x"/> at <paramref name="up"/> / <paramref name="down"/> of its rate, times <paramref name="gain"/>.</summary>
    public static double[] To(double[] x, int up, int down, double gain = 1)
    {
        int g = Gcd(up, down);
        up /= g; down /= g;
        if (up == down) return gain == 1 ? x : [.. x.Select(v => v * gain)];

        double lower = Math.Min(1.0, (double)up / down);        // the lower Nyquist, in units of the input's
        double centre = 0.5 * lower * (Pass + Stop) / 2;        // cycles a sample of the input: where the filter is at a half
        double width = 0.5 * lower * (Stop - Pass);
        double beta = 0.1102 * (Attenuation - 8.7);
        int half = (int)Math.Ceiling((Attenuation - 8) / (2.285 * 2 * Math.PI * width) / 2);
        int length = 2 * half + 1;
        var taps = new double[up][];
        double i0 = Bessel(beta);
        for (int phase = 0; phase < up; phase++)
        {
            var h = taps[phase] = new double[length];
            for (int t = -half; t <= half; t++)
            {
                double u = t - (double)phase / up, r = u / (half + 1);
                double sinc = u == 0 ? 2 * centre : Math.Sin(2 * Math.PI * centre * u) / (Math.PI * u);
                h[t + half] = gain * sinc * Bessel(beta * Math.Sqrt(Math.Max(0, 1 - r * r))) / i0;
            }
        }

        int n = x.Length;
        long count = ((long)n * up + down - 1) / down;
        var y = new double[count];
        Parallel.For(0L, count, i =>
        {
            long at = i * down;
            int centreAt = (int)(at / up);
            var h = taps[(int)(at % up)];
            int from = Math.Max(0, centreAt - half), to = Math.Min(n - 1, centreAt + half);
            y[i] = to < from ? 0 : Dot(x.AsSpan(from, to - from + 1), h.AsSpan(from - centreAt + half, to - from + 1));
        });
        return y;
    }

    static double Dot(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        int i = 0, step = Vector<double>.Count;
        var sum = Vector<double>.Zero;
        for (; i <= a.Length - step; i += step) sum += new Vector<double>(a[i..]) * new Vector<double>(b[i..]);
        double total = Vector.Sum(sum);
        for (; i < a.Length; i++) total += a[i] * b[i];
        return total;
    }

    /// <summary>The modified Bessel function of the first kind, order 0.</summary>
    static double Bessel(double x)
    {
        double sum = 1, term = 1;
        for (int k = 1; k < 200; k++)
        {
            term *= x / (2 * k) * (x / (2 * k));
            sum += term;
            if (term < sum * 1e-17) break;
        }
        return sum;
    }

    static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
}
