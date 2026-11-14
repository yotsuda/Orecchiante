namespace Dettato;

/// <summary>The discrete Fourier transform of a power of two of samples, in place, on separate real and imaginary parts.</summary>
sealed class Fft
{
    readonly int _n;
    readonly int[] _swap;
    readonly double[] _cos, _sin;       // of -2 pi k / n, k < n / 2

    public Fft(int n)
    {
        if (n < 2 || (n & (n - 1)) != 0) throw new ArgumentException($"{n} is not a power of two");
        _n = n;
        _swap = new int[n];
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            _swap[i] = j;
        }
        _cos = new double[n / 2];
        _sin = new double[n / 2];
        for (int k = 0; k < n / 2; k++)
        {
            _cos[k] = Math.Cos(-2 * Math.PI * k / n);
            _sin[k] = Math.Sin(-2 * Math.PI * k / n);
        }
    }

    public int Size => _n;

    public void Forward(double[] re, double[] im)
    {
        int n = _n;
        for (int i = 1; i < n; i++)
        {
            int j = _swap[i];
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            int half = len >> 1, stride = n / len;
            for (int i = 0; i < n; i += len)
            {
                for (int k = 0, w = 0; k < half; k++, w += stride)
                {
                    int a = i + k, b = a + half;
                    double vr = re[b] * _cos[w] - im[b] * _sin[w], vi = re[b] * _sin[w] + im[b] * _cos[w];
                    re[b] = re[a] - vr; im[b] = im[a] - vi;
                    re[a] += vr; im[a] += vi;
                }
            }
        }
    }
}
