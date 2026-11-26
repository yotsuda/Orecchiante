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

    /// <summary>What puts the words to the voice (Words.Align): the model, the vocals at Words.Rate, the words (English:
    /// English syllables and English.Model).</summary>
    public sealed record Sung(Microsoft.ML.OnnxRuntime.InferenceSession Model, float[] Vocals, List<Mora> Morae, bool English = false);

    /// <summary>Each bar's music (Bar.Vocal) and the part's instrument (Chart.Singer) from the vocal part at 22050 Hz;
    /// with words, the notes are the words' (Words.Align) and each bar's syllables are Bar.Lyrics.</summary>
    /// <summary>The voice's pitch (pYIN), loudness (dB) and notes as heard (NotesOf), from the vocal part at 22050 Hz.</summary>
    static (double[] F0, bool[] Voiced, double[] Db, List<(double On, double Off, int Midi)> Heard, float[] Strength) Hear(double[] part)
    {
        var (f0, voiced) = Bass.Pyin(part, Frame, Fmin, Fmax);
        var strength = Features.Onsets(Features.Spectra(part, out _, Hop), Hop, 64, 8000);
        var db = Loudness(part, f0.Length);
        return (f0, voiced, db, NotesOf(f0, voiced, Bass.OnsetFrames(strength, Delta), db), strength);
    }

    /// <summary>The notes as heard without words (NotesOf), in seconds: for measuring (`orecchiante words --none`).</summary>
    internal static List<(double On, double Off, int Midi)> HeardOf(double[] part) => Hear(part).Heard;

    /// <summary>The words' notes on the vocal part (Words.Align), in seconds: for measuring (`orecchiante words`) and Read.</summary>
    internal static List<Words.Note> WordsOf(double[] part, Sung sung) => WordsOf(Hear(part), sung);

    static List<Words.Note> WordsOf((double[] F0, bool[] Voiced, double[] Db, List<(double On, double Off, int Midi)> Heard, float[] Strength) h, Sung sung)
    {
        double loud = Percentile(h.Db, 95);
        return Words.Align(sung.Model, sung.Vocals, h.F0, h.Voiced, [.. h.Db.Select(d => d - loud)], h.Heard, sung.Morae, sung.English, h.Strength);
    }

    public static void Read(double[] part, Chart chart, Sung? sung = null)
    {
        var h = Hear(part);
        var heard = h.Heard;
        var bars = chart.Bars;
        bool InBars(double on) => bars[0].Start - 0.05 <= on && on < bars[^1].End;
        var (slots, where) = Bass.SlotsOf(bars);
        List<(int First, int Last, int Midi, string? Syllable)> placed;
        if (sung != null)
        {
            var words = WordsOf(h, sung).Where(n => InBars(n.On)).ToList();
            chart.Singer = Instrument([.. words.Select(n => (n.On, n.Off, n.Midi))]);
            placed = [.. Words.RoundOnto(slots, words).Select(x => (x.First, x.Last, x.Midi, (string?)x.Syllable))];
        }
        else
        {
            var notes = heard.Where(n => InBars(n.On)).ToList();
            chart.Singer = Instrument(notes);
            placed = [.. Bass.RoundOnto(slots, notes, pull: 0).Select(x => (x.First, x.Last, x.Midi, (string?)null))];
        }
        var lengths = bars.Select(b => b.Beats.Count * 4).ToArray();
        var perBar = bars.Select(_ => new List<(int From, int To, int Midi, bool Tied)>()).ToArray();
        var syllables = bars.Select(_ => new List<string>()).ToArray();
        foreach (var (first, last, midi, syllable) in placed)
        {
            var (k, q) = where[first];
            if (syllable != null && k < bars.Count) syllables[k].Add(syllable);   // a tie holds it: the syllable is the first note's
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
        for (int k = 0; k < bars.Count; k++)
        {
            bars[k].Vocal = Bass.BarMusic(perBar[k], lengths[k], names, up: 0, six: chart.Time == "6/8");
            if (sung != null) bars[k].Lyrics = string.Join(" ", syllables[k]);
        }
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
