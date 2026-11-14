using System.Globalization;

namespace Dettato;

/// <summary>numpy's and Python's arithmetic where it is not the plain one, so that the rules here (Reader.cs) read
/// what tools/read_chords.py reads, to the last bit where that is cheap: sums in numpy's order, medians, Python's
/// floor division and its rounding to decimals.</summary>
static class Np
{
    /// <summary>numpy's sum of a contiguous run (add.reduce): in order under 8 values, eight sums side by side up
    /// to 128, halves beyond.</summary>
    public static float Sum(ReadOnlySpan<float> a)
    {
        int n = a.Length;
        if (n < 8)
        {
            float sum = 0;
            foreach (float v in a) sum += v;
            return sum;
        }
        if (n <= 128)
        {
            float r0 = a[0], r1 = a[1], r2 = a[2], r3 = a[3], r4 = a[4], r5 = a[5], r6 = a[6], r7 = a[7];
            int i = 8;
            for (; i < n - n % 8; i += 8)
            {
                r0 += a[i]; r1 += a[i + 1]; r2 += a[i + 2]; r3 += a[i + 3];
                r4 += a[i + 4]; r5 += a[i + 5]; r6 += a[i + 6]; r7 += a[i + 7];
            }
            float sum = ((r0 + r1) + (r2 + r3)) + ((r4 + r5) + (r6 + r7));
            for (; i < n; i++) sum += a[i];
            return sum;
        }
        int half = n / 2;
        half -= half % 8;
        return Sum(a[..half]) + Sum(a[half..]);
    }

    public static double Sum(ReadOnlySpan<double> a)
    {
        int n = a.Length;
        if (n < 8)
        {
            double sum = 0;
            foreach (double v in a) sum += v;
            return sum;
        }
        if (n <= 128)
        {
            double r0 = a[0], r1 = a[1], r2 = a[2], r3 = a[3], r4 = a[4], r5 = a[5], r6 = a[6], r7 = a[7];
            int i = 8;
            for (; i < n - n % 8; i += 8)
            {
                r0 += a[i]; r1 += a[i + 1]; r2 += a[i + 2]; r3 += a[i + 3];
                r4 += a[i + 4]; r5 += a[i + 5]; r6 += a[i + 6]; r7 += a[i + 7];
            }
            double sum = ((r0 + r1) + (r2 + r3)) + ((r4 + r5) + (r6 + r7));
            for (; i < n; i++) sum += a[i];
            return sum;
        }
        int half = n / 2;
        half -= half % 8;
        return Sum(a[..half]) + Sum(a[half..]);
    }

    public static float Mean(ReadOnlySpan<float> a) => a.Length == 0 ? float.NaN : Sum(a) / a.Length;

    public static double Mean(ReadOnlySpan<double> a) => a.Length == 0 ? double.NaN : Sum(a) / a.Length;

    public static float Median(ReadOnlySpan<float> a)
    {
        if (a.Length == 0) return float.NaN;
        var sorted = a.ToArray();
        Array.Sort(sorted);
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    public static double Median(IEnumerable<double> values)
    {
        var sorted = values.ToArray();
        if (sorted.Length == 0) return double.NaN;
        Array.Sort(sorted);
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    public static List<double> Diff(IReadOnlyList<double> a)
    {
        var d = new List<double>(Math.Max(a.Count - 1, 0));
        for (int i = 1; i < a.Count; i++) d.Add(a[i] - a[i - 1]);
        return d;
    }

    /// <summary>Python's a // b of two floats.</summary>
    public static double FloorDiv(double a, double b)
    {
        double mod = a % b;
        double div = (a - mod) / b;
        if (mod != 0 && (b < 0) != (mod < 0)) div -= 1.0;
        if (div == 0) return Math.CopySign(0.0, a / b);
        double floor = Math.Floor(div);
        return div - floor > 0.5 ? floor + 1.0 : floor;
    }

    /// <summary>Python's round(x, digits): the nearest decimal of so many digits to the number as it is held.</summary>
    public static double Round(double x, int digits) =>
        double.Parse(x.ToString("F" + digits, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>Python's repr of a float.</summary>
    public static string Repr(double x)
    {
        string s = x.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('E') || s.Contains('N') || s.Contains('I') ? s : s + ".0";
    }
}
