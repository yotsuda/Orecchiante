namespace Orecchiante;

/// <summary>The bass part read into notes and written as a bass staff (M5, 2026-11-23): tools/bass.py's steps, which
/// say what each one is; tools/bass_check.py compares the two. Not to the letter: the part comes down to 22050 Hz by
/// Resample, not soxr, and the sums are in double where librosa's are in single precision.</summary>
static class Bass
{
    const int Rate = Features.Rate, Hop = 256, Frame = 4096;
    static readonly double Fmin = 440 * Math.Pow(2, (23 - 69) / 12.0), Fmax = 440 * Math.Pow(2, (60 - 69) / 12.0);   // B0, C4
    const int Steady = 10, Again = 3, PerSemitone = 10;
    const double MinNote = 0.06, Pull = 0.25, Tie = 0.4, TieWindow = 0.035, HeardDb = -21;

    /// <summary>Whether a bass is heard (step 6): the bass part's mean square over the recording's, both at 22050 Hz,
    /// HeardDb or more (the drums: Kit.HeardDb).</summary>
    public static bool Heard(double[] part, double[] recording, double heardDb = HeardDb)
    {
        double p = 0, r = 0;
        foreach (double v in part) p += v * v;
        foreach (double v in recording) r += v * v;
        return r > 0 && 10 * Math.Log10(p / part.Length / (r / recording.Length) + 1e-30) >= heardDb;
    }

    /// <summary>Each bar's music (Bar.Bass) from the bass part at 22050 Hz (steps 1 to 5).</summary>
    public static void Read(double[] part, Chart chart)
    {
        var (f0, voiced) = Pyin(part);
        var bars = chart.Bars;
        var strength = Features.Onsets(Features.Spectra(part, out _, Hop), Hop, 64, 2000);
        var notes = NotesOf(f0, voiced, OnsetFrames(strength)).Where(n => bars[0].Start - 0.05 <= n.On && n.On < bars[^1].End).ToList();

        var (slots, where) = SlotsOf(bars);
        var at16 = StrengthAt(strength, slots);
        var lengths = bars.Select(b => b.Beats.Count * 4).ToArray();
        var perBar = bars.Select(_ => new List<(int From, int To, int Midi, bool Tied)>()).ToArray();
        foreach (var (first, last, midi) in RoundOnto(slots, notes))
        {
            var (k, q) = where[first];
            int i = first;
            while (last > i && k < bars.Count)
            {
                int take = Math.Min(lengths[k] - q, last - i);
                bool tied = last - i > take && k + 1 < bars.Count && at16[i + take] < Tie * at16[first];
                perBar[k].Add((q, q + take, midi, tied));
                i += take;
                k++;
                q = 0;
            }
        }
        var names = NamesFor(chart.Key);
        for (int k = 0; k < bars.Count; k++) bars[k].Bass = BarMusic(perBar[k], lengths[k], names);
    }

    // --- 1. librosa.pyin (fill_na nan, center, zeros beyond the ends, transition_min_prob 1e-4) ---

    /// <summary>librosa.pyin(y, fmin=B0, fmax=C4, sr=22050, frame_length=4096, hop_length=256): the f0 of each frame
    /// (NaN where unvoiced) and whether it is voiced.</summary>
    internal static (double[] F0, bool[] Voiced) Pyin(double[] y)
    {
        int minPeriod = (int)Math.Floor(Rate / Fmax), maxPeriod = Math.Min((int)Math.Ceiling(Rate / Fmin), Frame - 1);
        int periods = maxPeriod - minPeriod + 1;
        // 12 * 10 * log2(C4 / B0) is 370 to the last bit in numpy; a hair under it here would lose a bin
        int bins = (int)Math.Floor(12 * PerSemitone * Math.Log2(Fmax / Fmin) + 1e-9) + 1;
        int frames = 1 + y.Length / Hop;
        double[] beta = BetaProbs();
        var obs = new double[frames][];
        var fft = new Fft(2 * Frame);
        Parallel.For(0, frames, () => (new double[2 * Frame], new double[2 * Frame], new double[maxPeriod + 1], new double[periods]), (t, _, b) =>
        {
            var (re, im, d, yin) = b;
            int first = t * Hop - Frame / 2;
            Array.Clear(re); Array.Clear(im);
            for (int i = 0; i < Frame; i++) { int j = first + i; re[i] = j >= 0 && j < y.Length ? y[j] : 0; }
            // the energy's running sum, then the autocorrelation (the power spectrum is real and even: its transform is
            // the inverse's, times the size)
            var energy = new double[Frame];
            double run = 0;
            for (int i = 0; i < Frame; i++) { run += re[i] * re[i]; energy[i] = run; }
            fft.Forward(re, im);
            for (int i = 0; i < 2 * Frame; i++) { re[i] = re[i] * re[i] + im[i] * im[i]; im[i] = 0; }
            fft.Forward(re, im);
            double acf0 = re[0] / (2 * Frame);
            d[0] = 0;
            for (int k = 1; k <= maxPeriod; k++) d[k] = 2 * (acf0 - re[k] / (2 * Frame)) - energy[k - 1];
            double sum = 0;
            var mean = new double[maxPeriod + 1];
            for (int k = 1; k <= maxPeriod; k++) { sum += d[k]; mean[k] = sum / k; }
            for (int p = minPeriod; p <= maxPeriod; p++) yin[p - minPeriod] = d[p] / (mean[p] + 2.2250738585072014e-308);
            obs[t] = Observe(yin, minPeriod, bins, beta);
            return b;
        }, _ => { });
        var states = Viterbi(obs, bins);
        var f0 = new double[frames];
        var voiced = new bool[frames];
        for (int t = 0; t < frames; t++)
        {
            voiced[t] = states[t] < bins;
            f0[t] = voiced[t] ? Fmin * Math.Pow(2, states[t] % bins / (12.0 * PerSemitone)) : double.NaN;
        }
        return (f0, voiced);
    }

    /// <summary>The beta(2, 18) prior over the 100 thresholds 0.01 .. 1: the difference of its distribution function.</summary>
    static double[] BetaProbs()
    {
        static double Cdf(double x) => 1 - Math.Pow(1 - x, 18) * (1 + 18 * x);
        var p = new double[100];
        for (int j = 0; j < 100; j++) p[j] = Cdf(j + 1 == 100 ? 1.0 : (j + 1) * 0.01) - Cdf(j * 0.01);
        return p;
    }

    /// <summary>__pyin_helper for one frame: the probability of each pitch bin voiced, then unvoiced (2 * bins).</summary>
    static double[] Observe(double[] yin, int minPeriod, int bins, double[] beta)
    {
        int n = yin.Length;
        var obs = new double[2 * bins];
        var troughs = new List<int>();
        if (yin[0] < yin[1]) troughs.Add(0);
        for (int i = 1; i < n - 1; i++) if (yin[i] < yin[i - 1] && yin[i] <= yin[i + 1]) troughs.Add(i);
        if (yin[n - 1] < yin[n - 2]) troughs.Add(n - 1);
        if (troughs.Count > 0)
        {
            int m = troughs.Count;
            var probs = new double[m];
            for (int j = 0; j < 100; j++)
            {
                double threshold = j + 1 == 100 ? 1.0 : (j + 1) * 0.01;
                int below = 0;
                foreach (int i in troughs) if (yin[i] < threshold) below++;
                if (below == 0) continue;
                double fact = (1 - Math.Exp(-2.0)) / (1 - Math.Exp(-2.0 * below));
                int pos = 0;
                for (int t = 0; t < m; t++)
                {
                    if (!(yin[troughs[t]] < threshold)) continue;
                    probs[t] += fact * Math.Exp(-2.0 * pos) * beta[j];
                    pos++;
                }
            }
            int lowest = 0;
            for (int t = 1; t < m; t++) if (yin[troughs[t]] < yin[troughs[lowest]]) lowest = t;
            int notBelow = 0;
            for (int j = 0; j < 100; j++) if (!(yin[troughs[lowest]] < (j + 1 == 100 ? 1.0 : (j + 1) * 0.01))) notBelow++;
            double extra = 0;
            for (int j = 0; j < notBelow; j++) extra += beta[j];
            probs[lowest] += 0.01 * extra;
            for (int t = 0; t < m; t++)
            {
                if (probs[t] == 0) continue;
                int i = troughs[t];
                double shift = 0;
                if (i > 0 && i < n - 1)
                {
                    double a = yin[i + 1] + yin[i - 1] - 2 * yin[i], b = 0.5 * (yin[i + 1] - yin[i - 1]);
                    if (Math.Abs(b) < Math.Abs(a)) shift = -b / a;
                }
                double f0 = Rate / (minPeriod + i + shift);
                int bin = (int)Math.Clamp(Math.Round(12 * PerSemitone * Math.Log2(f0 / Fmin)), 0, bins);
                obs[bin] = probs[t];
            }
        }
        double voiced = 0;
        for (int i = 0; i < bins; i++) voiced += obs[i];
        voiced = Math.Clamp(voiced, 0, 1);
        for (int i = bins; i < 2 * bins; i++) obs[i] = (1 - voiced) / bins;
        return obs;
    }

    /// <summary>librosa.sequence.viterbi with pyin's transitions (a triangle 51 bins wide within the voiced and within
    /// the unvoiced, 0.99 to stay voiced or unvoiced), only the transitions of 1e-4 or more, in logs (+ the smallest
    /// double): the most likely state of each frame. Ties go to the lower state, as librosa's loop has them.</summary>
    static int[] Viterbi(double[][] obs, int bins)
    {
        const double tiny = 2.2250738585072014e-308;
        int states = 2 * bins, width = (int)Math.Round(35.92 * 12 * Hop / Rate) * PerSemitone + 1, half = width / 2;
        // transition_local: each row the triangle about its own bin, cut at the ends, the row summed to 1
        var local = new double[bins][];
        for (int i = 0; i < bins; i++)
        {
            var row = local[i] = new double[bins];
            double sum = 0;
            for (int j = Math.Max(0, i - half); j <= Math.Min(bins - 1, i + half); j++)
            {
                row[j] = (half + 1 - Math.Abs(j - i)) / (double)(half + 1);
                sum += row[j];
            }
            for (int j = 0; j < bins; j++) row[j] /= sum;
        }
        double threshold = Math.Log(1e-4 + tiny);
        // the states each state may come from (log of the transition), lower states first
        var from = new (int K, double LogP)[states][];
        for (int j = 0; j < states; j++)
        {
            var list = new List<(int, double)>();
            for (int k = 0; k < states; k++)
            {
                double p = (k / bins == j / bins ? 0.99 : 0.01) * local[k % bins][j % bins];
                double lp = Math.Log(p + tiny);
                if (lp >= threshold) list.Add((k, lp));
            }
            from[j] = [.. list];
        }
        int frames = obs.Length;
        var value = new double[states];
        var next = new double[states];
        var back = new ushort[frames][];
        double init = Math.Log(1.0 / states + tiny);
        for (int j = 0; j < states; j++) value[j] = Math.Log(obs[0][j] + tiny) + init;
        for (int t = 1; t < frames; t++)
        {
            var ptr = back[t] = new ushort[states];
            var o = obs[t];
            var v = value;
            Parallel.For(0, states, j =>
            {
                double best = double.NegativeInfinity;
                int arg = 0;
                foreach (var (k, lp) in from[j])
                {
                    double cost = v[k] + lp;
                    if (cost > best) { best = cost; arg = k; }
                }
                ptr[j] = (ushort)arg;
                next[j] = Math.Log(o[j] + tiny) + best;
            });
            (value, next) = (next, value);
        }
        var path = new int[frames];
        int s = 0;
        for (int j = 1; j < states; j++) if (value[j] > value[s]) s = j;
        path[frames - 1] = s;
        for (int t = frames - 2; t >= 0; t--) path[t] = back[t + 1][path[t + 1]];
        return path;
    }

    // --- the onsets: librosa.onset.onset_detect(onset_envelope, backtrack=True) at 22050 Hz, hop 256 ---

    internal static List<int> OnsetFrames(float[] strength)
    {
        int n = strength.Length;
        double low = strength.Min();
        var x = strength.Select(v => (double)(v - low)).ToArray();
        double high = x.Max() + 1.1754943508222875e-38;     // the smallest single (the strength is single in librosa)
        if (x.Max() == 0) return [];
        for (int i = 0; i < n; i++) x[i] /= high;
        // peak_pick (greedy): pre_max 2, post_max 1, pre_avg 8, post_avg 9, wait 2, delta 0.07
        const int preMax = 2, postMax = 1, preAvg = 8, postAvg = 9, wait = 2;
        const double delta = 0.07;
        double Max(int a, int b) { double m = double.NegativeInfinity; for (int i = a; i < b; i++) m = Math.Max(m, x[i]); return m; }
        double Mean(int a, int b) { double s = 0; for (int i = a; i < b; i++) s += x[i]; return s / (b - a); }
        var peaks = new List<int>();
        int t = 1;
        if (x[0] >= Max(0, Math.Min(postMax, n)) && x[0] >= Mean(0, Math.Min(postAvg, n)) + delta) { peaks.Add(0); t = wait + 1; }
        while (t < n)
        {
            if (x[t] != Max(Math.Max(0, t - preMax), Math.Min(t + postMax, n))) { t++; continue; }
            if (!(x[t] >= Mean(Math.Max(0, t - preAvg), Math.Min(t + postAvg, n)) + delta)) { t++; continue; }
            peaks.Add(t);
            t += wait + 1;
        }
        // backtracked to the nearest preceding minimum of the (normalised) strength, or 0
        var minima = new List<int> { 0 };
        for (int i = 1; i < n - 1; i++) if (x[i] <= x[i - 1] && x[i] < x[i + 1]) minima.Add(i);
        var res = new List<int>();
        foreach (int p in peaks)
        {
            int m = 0;
            foreach (int c in minima) { if (c <= p) m = c; else break; }
            res.Add(m);
        }
        return res;
    }

    // --- 2. notes ---

    internal static List<(double On, double Off, int Midi)> NotesOf(double[] f0, bool[] voiced, List<int> onsets)
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
        var midi = new int[n];
        for (int i = 0; i < n; i++) midi[i] = voiced[i] ? (int)Math.Round(m[i] - tune) : -1;
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
        return [.. cut.Where(x => (x.B - x.A) * ft >= MinNote).Select(x => (x.A * ft, x.B * ft, x.P))];
    }

    // --- 3. the grid ---

    internal static (double[] Slots, (int Bar, int At)[] Where) SlotsOf(List<Bar> bars)
    {
        var slots = new List<double>();
        var where = new List<(int, int)>();
        for (int k = 0; k < bars.Count; k++)
        {
            var beats = bars[k].Beats.Append(bars[k].End).ToList();
            for (int j = 0; j < beats.Count - 1; j++)
                for (int q = 0; q < 4; q++)
                {
                    slots.Add(beats[j] + (beats[j + 1] - beats[j]) * q / 4);
                    where.Add((k, j * 4 + q));
                }
        }
        slots.Add(bars[^1].End);
        where.Add((bars.Count, 0));
        return ([.. slots], [.. where]);
    }

    static double[] StrengthAt(float[] strength, double[] slots)
    {
        double ft = (double)Hop / Rate;
        int w = (int)Math.Round(TieWindow / ft);
        return [.. slots.Select(t =>
        {
            int x = (int)Math.Round(t / ft);
            if (x - w < 0 || x - w >= strength.Length) return 0.0;
            double m = double.NegativeInfinity;
            for (int i = x - w; i <= Math.Min(x + w, strength.Length - 1); i++) m = Math.Max(m, strength[i]);
            return m;
        })];
    }

    static int Nearest(double[] slots, double t)
    {
        int i = Array.BinarySearch(slots, t);
        if (i < 0) i = ~i;
        i = Math.Clamp(i, 1, slots.Length - 1);
        return t - slots[i - 1] < slots[i] - t ? i - 1 : i;
    }

    static List<(int First, int Last, int Midi)> RoundOnto(double[] slots, List<(double On, double Off, int Midi)> notes)
    {
        var at = new SortedDictionary<int, (int End, int Midi)>();
        foreach (var (a, b, p) in notes)
        {
            int i = Nearest(slots, a);
            if (i % 2 == 1 && Math.Abs(a - slots[i]) > Pull * (slots[Math.Min(i + 1, slots.Length - 1)] - slots[i]))
                i = a < slots[i] || i + 1 >= slots.Length ? i - 1 : i + 1;
            int e = Math.Max(Nearest(slots, b), i + 1);
            if (!at.TryGetValue(i, out var had) || had.End - i < e - i) at[i] = (e, p);
        }
        var keys = at.Keys.ToList();
        var res = new List<(int, int, int)>();
        for (int n = 0; n < keys.Count; n++)
        {
            var (e, p) = at[keys[n]];
            if (n + 1 < keys.Count)
            {
                int next = keys[n + 1];
                if (next - e <= 1) e = next;
                e = Math.Min(e, next);
            }
            res.Add((keys[n], e, p));
        }
        return res;
    }

    // --- 5. writing ---

    static readonly (int Sixteenths, string Text)[] Lengths = [(16, "1"), (12, "2."), (8, "2"), (6, "4."), (4, "4"), (3, "8."), (2, "8"), (1, "16")];
    static readonly string[] Sharps = ["c", "cis", "d", "dis", "e", "f", "fis", "g", "gis", "a", "ais", "b"];
    static readonly string[] Flats = ["c", "des", "d", "ees", "e", "f", "ges", "g", "aes", "a", "bes", "b"];

    /// <summary>The spelling of a chart's key ("Bb major"): flats in the keys written with flats.</summary>
    internal static string[] NamesFor(string key)
    {
        var parts = key.Split(' ');
        string tonic = parts[0], mode = parts.Length > 1 ? parts[1] : "major";
        string name = char.ToLowerInvariant(tonic[0]) + (tonic[1..] == "#" ? "is" : tonic[1..] == "b" ? "es" : "");
        bool flats = mode == "major" ? name is "f" or "bes" or "ees" or "aes" or "des" or "ges" or "ces"
                                     : name is "d" or "g" or "c" or "f" or "bes" or "ees" or "aes";
        return flats ? Flats : Sharps;
    }

    internal static List<int> Pieces(int pos, int length)
    {
        var res = new List<int>();
        while (length > 0)
        {
            int d = pos % 4 != 0
                ? Lengths.First(l => l.Sixteenths <= Math.Min(length, 4 - pos % 4)).Sixteenths
                : Lengths.First(l => l.Sixteenths <= length && (l.Sixteenths <= 6 || (l.Sixteenths is 8 or 12 && pos % 8 == 0) || (l.Sixteenths == 16 && pos == 0))).Sixteenths;
            res.Add(d);
            pos += d;
            length -= d;
        }
        return res;
    }

    internal static string Text(int sixteenths) => Lengths.First(l => l.Sixteenths == sixteenths).Text;

    static string PitchName(int midi, string[] names)
    {
        int w = midi + 12, o = (int)Math.Floor(w / 12.0) - 4;
        return names[((w % 12) + 12) % 12] + (o > 0 ? new string('\'', o) : new string(',', -o));
    }

    static string BarMusic(List<(int From, int To, int Midi, bool Tied)> notes, int sixteenths, string[] names)
    {
        var toks = new List<string>();
        int pos = 0;
        foreach (var (a, b, p, tied) in notes)
        {
            if (a > pos) toks.AddRange(Pieces(pos, a - pos).Select(d => "r" + Text(d)));
            var ds = Pieces(a, b - a);
            string name = PitchName(p, names);
            toks.AddRange(ds.Select((d, k) => name + Text(d) + (k < ds.Count - 1 || tied ? "~" : "")));
            pos = b;
        }
        if (pos < sixteenths) toks.AddRange(Pieces(pos, sixteenths - pos).Select(d => "r" + Text(d)));
        return string.Join(" ", toks);
    }
}
