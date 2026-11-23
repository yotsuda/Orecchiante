namespace Orecchiante;

/// <summary>The drum part read into a kit's hits on the chart's 16ths and written as a drum staff (2026-11-23):
/// tools/drums.py's steps, which say what each one is. A first try with no truth to score it on.</summary>
static class Kit
{
    const int Hop = 256, Size = Features.Size, Gap = 3;
    const double Low = 150, MidFrom = 200, MidTo = 3000, High = 6000;
    const double Kick = 0.5, Hat = 6, Snare = 0.7, Wires = 0.85, Floor = 6, ScaleMin = 12;
    /// <summary>Drums heard: the drum part within this many dB of the recording (2026-11-23: 8 songs mixed again
    /// without their drums and separated, -42.6 dB and down; 162 of 165 xsc songs -20.6 dB and up, the other three,
    /// with no drums it seems, -44.5 and down).</summary>
    public const double HeardDb = -30;

    /// <summary>Each bar's drums (Bar.Hands, Bar.Feet) from the drum part at 22050 Hz.</summary>
    public static void Read(double[] part, Chart chart)
    {
        var rise = Rises(part);
        var bars = chart.Bars;
        var (slots, where) = Bass.SlotsOf(bars);
        int n = slots.Length - 1;
        var v = new double[n, 3];
        for (int i = 0; i < n; i++)
        {
            int c = (int)Math.Round(slots[i] / ((double)Hop / Features.Rate));
            if (c - 2 < 0 || c + 3 > rise.GetLength(1)) continue;
            for (int b = 0; b < 3; b++)
                for (int t = c - 2; t < c + 3; t++) v[i, b] = Math.Max(v[i, b], rise[b, t]);
        }
        var scale = new double[3];
        for (int b = 0; b < 3; b++)
        {
            var strong = Enumerable.Range(0, n).Select(i => v[i, b]).Where(x => x > Floor).ToList();
            scale[b] = Math.Max(strong.Count > 0 ? Percentile(strong, 90) : ScaleMin, ScaleMin);
        }
        var hands = bars.Select(_ => new SortedDictionary<int, string>()).ToArray();
        var feet = bars.Select(_ => new SortedDictionary<int, string>()).ToArray();
        for (int i = 0; i < n; i++)
        {
            double lo = v[i, 0], mi = v[i, 1], hi = v[i, 2];
            bool kick = lo >= Kick * scale[0], hat = hi >= Hat, snare = mi >= Snare * scale[1] && (hi >= Wires * scale[2] || !kick);
            var (k, q) = where[i];
            if (kick) feet[k][q] = "bd";
            if (snare && hat) hands[k][q] = "<sn hh>";
            else if (snare) hands[k][q] = "sn";
            else if (hat) hands[k][q] = "hh";
        }
        for (int k = 0; k < bars.Count; k++)
        {
            int sixteenths = bars[k].Beats.Count * 4;
            bars[k].Hands = VoiceMusic(hands[k], sixteenths);
            bars[k].Feet = VoiceMusic(feet[k], sixteenths);
        }
    }

    /// <summary>numpy.percentile (linear between the two nearest).</summary>
    static double Percentile(List<double> values, double p)
    {
        var s = values.Order().ToArray();
        double at = p / 100 * (s.Length - 1);
        int lo = (int)Math.Floor(at), hi = Math.Min(lo + 1, s.Length - 1);
        return s[lo] + (s[hi] - s[lo]) * (at - lo);
    }

    /// <summary>Each band's rise in dB, [band, frame] (step 1).</summary>
    static double[,] Rises(double[] part)
    {
        var spectra = Features.Spectra(part, out _, Hop);
        int frames = spectra.Length;
        var db = new double[3, frames];
        for (int t = 0; t < frames; t++)
        {
            double lo = 0, mi = 0, hi = 0;
            var row = spectra[t];
            for (int k = 0; k < row.Length; k++)
            {
                double f = (double)k * Features.Rate / Size, p = (double)row[k] * row[k];
                if (f < Low) lo += p;
                else if (f >= MidFrom && f < MidTo) mi += p;
                else if (f >= High) hi += p;
            }
            db[0, t] = 10 * Math.Log10(lo + 1e-10);
            db[1, t] = 10 * Math.Log10(mi + 1e-10);
            db[2, t] = 10 * Math.Log10(hi + 1e-10);
        }
        var rise = new double[3, frames];
        for (int b = 0; b < 3; b++)
            for (int t = Gap; t < frames; t++)
            {
                double least = double.PositiveInfinity;
                for (int j = t - Gap; j < t; j++) least = Math.Min(least, db[b, j]);
                rise[b, t] = Math.Max(0, db[b, t] - least);
            }
        return rise;
    }

    /// <summary>One voice of a bar: each hit to the next within its beat, rests between (step 4).</summary>
    static string VoiceMusic(SortedDictionary<int, string> at, int sixteenths)
    {
        var toks = new List<string>();
        var starts = at.Keys.ToList();
        int pos = 0;
        for (int n = 0; n < starts.Count; n++)
        {
            int a = starts[n];
            if (a > pos) toks.AddRange(Bass.Pieces(pos, a - pos).Select(d => "r" + Bass.Text(d)));
            int end = Math.Min(n + 1 < starts.Count ? starts[n + 1] : sixteenths, (a / 4 + 1) * 4);
            var ds = Bass.Pieces(a, end - a);
            toks.AddRange(ds.Select((d, k) => at[a] + Bass.Text(d) + (k < ds.Count - 1 ? "~" : "")));
            pos = end;
        }
        if (pos < sixteenths) toks.AddRange(Bass.Pieces(pos, sixteenths - pos).Select(d => "r" + Bass.Text(d)));
        return string.Join(" ", toks);
    }
}
