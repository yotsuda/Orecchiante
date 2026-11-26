namespace Orecchiante;

/// <summary>The sung melody read into notes and written as a voice staff (2026-11-26, Shosa's request 1):
/// tools/melody.py's steps, which say what each one is; tools/melody_check.py compares the two. pYIN and the onsets are
/// the bass's (Bass.Pyin, Bass.OnsetFrames) with the voice's range and frames.</summary>
static class Melody
{
    const int Rate = Features.Rate, Hop = 256, Frame = 2048;
    static readonly double Fmin = 440 * Math.Pow(2, (36 - 69) / 12.0), Fmax = 440 * Math.Pow(2, (84 - 69) / 12.0);   // C2, C6
    const int Steady = 6, Again = 4, Med = 5;
    const double MinNote = 0.08, Delta = 0.1, Quiet = 8, Lag = -0.02;
    /// <summary>A voice heard: the vocal part within this many dB of the recording (2026-11-26: the quietest sung song
    /// of xsc and RWC-P -24.5 dB, the separated vocals of instrumentals -18 to -38: their lead is read as the melody).</summary>
    public const double HeardDb = -30;

    /// <summary>Each bar's music (Bar.Vocal) and the part's instrument (Chart.Singer) from the vocal part at 22050 Hz.</summary>
    public static void Read(double[] part, Chart chart)
    {
        var (f0, voiced) = Bass.Pyin(part, Frame, Fmin, Fmax);
        var bars = chart.Bars;
        var strength = Features.Onsets(Features.Spectra(part, out _, Hop), Hop, 64, 8000);
        var notes = NotesOf(f0, voiced, Bass.OnsetFrames(strength, Delta), Loudness(part, f0.Length))
            .Where(n => bars[0].Start - 0.05 <= n.On && n.On < bars[^1].End).ToList();
        chart.Singer = Instrument(notes);
        var (slots, where) = Bass.SlotsOf(bars);
        var lengths = bars.Select(b => b.Beats.Count * 4).ToArray();
        var perBar = bars.Select(_ => new List<(int From, int To, int Midi, bool Tied)>()).ToArray();
        foreach (var (first, last, midi) in Bass.RoundOnto(slots, notes, pull: 0))
        {
            var (k, q) = where[first];
            int i = first;
            while (last > i && k < bars.Count)
            {
                int take = Math.Min(lengths[k] - q, last - i);
                perBar[k].Add((q, q + take, midi, last - i > take && k + 1 < bars.Count));    // a voice holds its syllable over the bar line
                i += take;
                k++;
                q = 0;
            }
        }
        var names = Bass.NamesFor(chart.Key);
        for (int k = 0; k < bars.Count; k++) bars[k].Vocal = Bass.BarMusic(perBar[k], lengths[k], names, up: 0);
    }

    /// <summary>The voice with fewer notes out of its range (Lily#'s: tenor C3-C5, soprano C4-C6), tenor on a tie.</summary>
    static string Instrument(List<(double On, double Off, int Midi)> notes) =>
        notes.Count(n => n.Midi < 60 || n.Midi > 84) < notes.Count(n => n.Midi < 48 || n.Midi > 72) ? "soprano" : "tenor";

    /// <summary>librosa.feature.rms(frame_length=2048, hop_length=256) in dB (+1e-6): frames centred, zeros beyond the ends.</summary>
    static double[] Loudness(double[] y, int frames)
    {
        var db = new double[frames];
        Parallel.For(0, frames, t =>
        {
            double s = 0;
            int first = t * Hop - Frame / 2;
            for (int i = Math.Max(0, first); i < Math.Min(y.Length, first + Frame); i++) s += y[i] * y[i];
            db[t] = 20 * Math.Log10(Math.Sqrt(s / Frame) + 1e-6);
        });
        return db;
    }

    /// <summary>numpy.percentile (linear between the two nearest).</summary>
    static double Percentile(double[] values, double p)
    {
        var s = values.Order().ToArray();
        double at = p / 100 * (s.Length - 1);
        int lo = (int)Math.Floor(at), hi = Math.Min(lo + 1, s.Length - 1);
        return s[lo] + (s[hi] - s[lo]) * (at - lo);
    }

    internal static List<(double On, double Off, int Midi)> NotesOf(double[] f0, bool[] voiced, List<int> onsets, double[] db)
    {
        int n = f0.Length;
        var m = new double[n];
        double c = 0, s = 0;
        int count = 0;
        for (int i = 0; i < n; i++)
        {
            m[i] = 69 + 12 * Math.Log2((double.IsNaN(f0[i]) ? 1.0 : f0[i]) / 440);
            if (!voiced[i]) continue;
            double d = m[i] - Math.Round(m[i]);
            c += Math.Cos(2 * Math.PI * d); s += Math.Sin(2 * Math.PI * d); count++;
        }
        double tune = count > 0 ? Math.Atan2(s / count, c / count) / (2 * Math.PI) : 0;
        var raw = new int[n];
        for (int i = 0; i < n; i++) raw[i] = voiced[i] ? (int)Math.Round(m[i] - tune) : -1;
        // the median of Med frames within each voiced run, the run's ends repeated
        var midi = (int[])raw.Clone();
        var window = new int[Med];
        for (int i = 0; i < n;)
        {
            if (raw[i] < 0) { i++; continue; }
            int j = i;
            while (j < n && raw[j] >= 0) j++;
            for (int k = i; k < j; k++)
            {
                for (int w = 0; w < Med; w++) window[w] = raw[Math.Clamp(k - Med / 2 + w, i, j - 1)];
                Array.Sort(window);
                midi[k] = window[Med / 2];
            }
            i = j;
        }
        var on = new HashSet<int>(onsets);
        var cut = new List<(int A, int B, int P)>();
        int cur = -1, start = 0;
        for (int i = 0; i < n; i++)
        {
            int p = midi[i];
            bool steady = p >= 0;
            for (int j = i; steady && j < Math.Min(n, i + Steady); j++) steady = midi[j] == p;
            if (p != cur && (p < 0 || steady))
            {
                if (cur >= 0) cut.Add((start, i, cur));
                cur = p; start = i;
            }
            else if (p == cur && p >= 0 && on.Contains(i) && i - start >= Again)
            {
                cut.Add((start, i, cur));
                start = i;
            }
        }
        if (cur >= 0) cut.Add((start, n, cur));
        double ft = (double)Hop / Rate;
        cut = [.. cut.Where(x => (x.B - x.A) * ft >= MinNote)];
        if (cut.Count > 0)
        {
            var loud = cut.Select(x => { double sum = 0; for (int i = x.A; i < x.B; i++) sum += db[Math.Min(i, db.Length - 1)]; return sum / (x.B - x.A); }).ToArray();
            double floor = Percentile(loud, 75) - Quiet;
            cut = [.. cut.Where((x, i) => loud[i] >= floor)];
        }
        return [.. cut.Select(x => (x.A * ft + Lag, x.B * ft + Lag, x.P))];
    }
}
