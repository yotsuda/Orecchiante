namespace Orecchiante;

/// <summary>A groove of the drum staff: its name and its one bar as a steps chart (`steps { hh "x.x. …" … }`).</summary>
sealed record Groove(string Name, string Steps);

/// <summary>The drum part read into a kit's hits on the chart's 16ths and written as a drum staff of grooves
/// (2026-11-23), each a steps chart (0.18.0): tools/drums.py's steps, which say what each one is;
/// tools/drums_check.py compares the two. A first try with no truth to score it on.</summary>
static class Kit
{
    const int Hop = 256, Size = Features.Size, Gap = 3, Near = 4, MinBars = 3, Rounds = 3;
    const double Low = 150, MidFrom = 200, MidTo = 3000, High = 6000;
    const double Kick = 0.5, Hat = 6, Snare = 0.7, Wires = 0.85, Floor = 6, ScaleMin = 12;
    static readonly string[] Drums = ["hh", "sn", "bd"];
    /// <summary>Drums heard: the drum part within this many dB of the recording (2026-11-23: 8 songs mixed again
    /// without their drums and separated, -42.6 dB and down; 162 of 165 xsc songs -20.6 dB and up, the other three,
    /// with no drums it seems, -44.5 and down).</summary>
    public const double HeardDb = -30;

    /// <summary>Each bar's drums (Bar.Kit, Bar.Drums) and the chart's grooves (Chart.Grooves) from the drum part at 22050 Hz.</summary>
    public static void Read(double[] part, Chart chart)
    {
        var bars = chart.Bars;
        var (slots, where) = Bass.SlotsOf(bars);
        var hits = Hits(Rises(part), slots);
        var sets = bars.Select(_ => new SortedSet<(int At, string Drum)>()).ToArray();
        for (int i = 0; i < hits.Count; i++)
        {
            var (k, q) = where[i];
            foreach (string d in hits[i]) sets[k].Add((q, d));
        }
        var lengths = bars.Select(b => b.Beats.Count * 4).ToArray();
        bool six = chart.Time == "6/8";
        var (assign, patterns) = Grooves(sets, lengths);
        var named = new List<(string Name, string? Steps, string Silent)>();
        chart.Grooves = [];
        for (int g = 0; g < patterns.Count; g++)
        {
            var rows = LinesOf(patterns[g].Hits, patterns[g].Sixteenths, six);
            named.Add(($"beat{Letters(g)}", rows.Count > 0 ? ChartOf(rows, six) : null, Bass.Rests(patterns[g].Sixteenths, six)));
            if (named[^1].Steps is string steps) chart.Grooves.Add(new Groove(named[^1].Name, steps));
        }
        for (int k = 0; k < bars.Count; k++)
        {
            var raw = LinesOf(sets[k], lengths[k], six);
            bars[k].Drums = raw;
            if (assign[k] >= 0) bars[k].Kit = named[assign[k]].Steps != null ? named[assign[k]].Name : named[assign[k]].Silent;
            else if (raw.Count > 0) bars[k].Kit = ChartOf(raw, six);
            else bars[k].Kit = Bass.Rests(lengths[k], six);
        }
    }

    // --- 1 to 3: the hits ---

    static List<HashSet<string>> Hits(double[,] rise, double[] slots)
    {
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
        var res = new List<HashSet<string>>();
        for (int i = 0; i < n; i++)
        {
            double lo = v[i, 0], mi = v[i, 1], hi = v[i, 2];
            bool kick = lo >= Kick * scale[0];
            var h = new HashSet<string>();
            if (kick) h.Add("bd");
            if (hi >= Hat) h.Add("hh");
            if (mi >= Snare * scale[1] && (hi >= Wires * scale[2] || !kick)) h.Add("sn");
            res.Add(h);
        }
        return res;
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

    // --- 4. the grooves ---

    static string Key(IEnumerable<(int At, string Drum)> s) => string.Join(",", s.OrderBy(x => x.At).ThenBy(x => x.Drum, StringComparer.Ordinal).Select(x => $"{x.At}{x.Drum}"));

    static (int[] Assign, List<(int Sixteenths, HashSet<(int, string)> Hits)> Patterns) Grooves(SortedSet<(int At, string Drum)>[] sets, int[] lengths)
    {
        int n = sets.Length;
        var first = new Dictionary<(int, string), int>();
        var count = new Dictionary<(int, string), int>();
        for (int k = 0; k < n; k++)
        {
            var key = (lengths[k], Key(sets[k]));
            first.TryAdd(key, k);
            count[key] = count.GetValueOrDefault(key) + 1;
        }
        var pats = new List<(int Sixteenths, HashSet<(int, string)> Hits)>();
        foreach (var key in count.Keys.OrderBy(x => -count[x]).ThenBy(x => first[x]))
        {
            var s = sets[first[key]];
            if (!pats.Any(p => p.Sixteenths == key.Item1 && Distance(s, p.Hits) <= Near)) pats.Add((key.Item1, [.. s]));
        }
        int Nearest(int k)
        {
            int best = int.MaxValue, arg = -1;
            for (int g = 0; g < pats.Count; g++)
            {
                if (pats[g].Sixteenths != lengths[k]) continue;
                int d = Distance(sets[k], pats[g].Hits);
                if (d <= Near && d < best) { best = d; arg = g; }
            }
            return arg;
        }
        for (int round = 0; round < Rounds; round++)
        {
            var assign = Enumerable.Range(0, n).Select(Nearest).ToArray();
            var next = new List<(int, HashSet<(int, string)>)>();
            for (int g = 0; g < pats.Count; g++)
            {
                var members = Enumerable.Range(0, n).Where(k => assign[k] == g).ToList();
                if (members.Count == 0) continue;
                var votes = new Dictionary<(int, string), int>();
                foreach (int k in members) foreach (var x in sets[k]) votes[x] = votes.GetValueOrDefault(x) + 1;
                next.Add((pats[g].Sixteenths, [.. votes.Where(v => 2 * v.Value > members.Count).Select(v => v.Key)]));
            }
            pats = next;
        }
        var last = Enumerable.Range(0, n).Select(Nearest).ToArray();
        var keep = Enumerable.Range(0, pats.Count).Where(g => last.Count(a => a == g) >= MinBars).ToList();
        var index = keep.Select((g, i) => (g, i)).ToDictionary(x => x.g, x => x.i);
        return ([.. last.Select(a => index.TryGetValue(a, out int i) ? i : -1)], [.. keep.Select(g => pats[g])]);
    }

    static int Distance(IEnumerable<(int, string)> a, HashSet<(int, string)> b)
    {
        var x = a.ToHashSet();
        return x.Count(e => !b.Contains(e)) + b.Count(e => !x.Contains(e));
    }

    // --- 5. writing ---

    /// <summary>{drum: its row} for the drums struck (tools/drums.py's lines_of).</summary>
    static Dictionary<string, string> LinesOf(IEnumerable<(int At, string Drum)> hits, int sixteenths, bool six)
    {
        var res = new Dictionary<string, string>();
        foreach (string drum in Drums)
        {
            var starts = hits.Where(h => h.Drum == drum).Select(h => h.At).ToList();
            if (starts.Count > 0) res[drum] = StepRow(starts, sixteenths, six);
        }
        return res;
    }

    /// <summary>One drum's row of a bar: a character a 16th (`x` struck, `.` not), a beat a run of four (six in 6/8).</summary>
    static string StepRow(List<int> starts, int sixteenths, bool six)
    {
        var beats = Enumerable.Range(0, sixteenths / 4).Select(_ => new string('.', six ? 6 : 4).ToCharArray()).ToArray();
        foreach (int q in starts) beats[q / 4][six ? Bass.Six[q % 4] : q % 4] = 'x';
        return string.Join(" ", beats.Select(b => new string(b)));
    }

    /// <summary>The steps chart of a bar's rows (Lily#'s `steps`, Orecchiante's request 24; `steps 4.` in 6/8).</summary>
    static string ChartOf(Dictionary<string, string> rows, bool six) =>
        $"steps{(six ? " 4." : "")} {{ " + string.Join(" ", Drums.Where(rows.ContainsKey).Select(d => $"{d} \"{rows[d]}\"")) + " }";

    static string Letters(int i)
    {
        string s = "";
        for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
        return s;
    }
}
