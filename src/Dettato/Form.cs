namespace Dettato;

/// <summary>The sections of a chart: runs of bars said again are written once, and the order they are played in
/// after them. tools/read_chords.py's fold() and find_form(), step for step.</summary>
static class Form
{
    const int SectionBars = 4;      // the shortest run of bars that is a section for being said again

    /// <summary>A count a key, in the order the keys came (Python's Counter): the first of the largest wins.</summary>
    sealed class Tally<T> where T : notnull
    {
        readonly List<T> keys = [];
        readonly Dictionary<T, double> sums = [];
        public bool Any => keys.Count > 0;
        public void Add(T key, double weight)
        {
            if (!sums.TryGetValue(key, out double sum)) keys.Add(key);
            sums[key] = sum + weight;
        }
        public T Most()
        {
            T best = keys[0];
            foreach (T key in keys) if (sums[key] > sums[best]) best = key;
            return best;
        }
    }

    static (int Root, int Kind) KindOf(Symbol s) => (s.Root, Reader.Kind[s.Quality]);

    /// <summary>Finds the sections and makes every playing of a section say the same: on each beat, the chord the
    /// playings are surest of together. Where they differed in root or family, the chord is marked in every
    /// playing, and the chord offered in its place is the other reading. Returns the form.</summary>
    public static List<string> Fold(List<Bar> bars, int beats, double alike, double doubt)
    {
        var on = bars.Select(bar => Enumerable.Range(0, beats).Select(i =>
        {
            double at = bar.Start + (i + 0.01) * (bar.End - bar.Start) / beats;
            return bar.Chords.FirstOrDefault(c => c.Start <= at && at < c.End) ?? bar.Chords[^1];
        }).ToArray()).ToArray();
        var (form, places) = Find([.. on.Select(row => row.Select(c => c.Symbol.Quality * 12 + c.Symbol.Root).ToArray())], alike);
        var said = new Dictionary<(string, int), List<int>>();
        for (int n = 0; n < bars.Count; n++)
        {
            (bars[n].Section, bars[n].BarOfSection) = places[n];
            if (!said.TryGetValue(places[n], out var list)) said[places[n]] = list = [];
            list.Add(n);
        }
        foreach (var playings in said.Values)
        {
            if (playings.Count < 2 || playings.All(n => on[n].Select(c => c.Symbol).SequenceEqual(on[playings[0]].Select(c => c.Symbol)))) continue;
            var agreed = new (Symbol Symbol, double Sure, (int Root, int Quality)? Other)[beats];
            for (int b = 0; b < beats; b++)
            {
                var heard = playings.Select(n => on[n][b]).ToArray();
                var kinds = new Tally<(int, int)>();
                foreach (var c in heard) kinds.Add(KindOf(c.Symbol), c.Sure ?? 1.0);
                var won = kinds.Most();
                var symbols = new Tally<Symbol>();
                var others = new Tally<(int, int)>();
                foreach (var c in heard)
                    if (KindOf(c.Symbol) == won) symbols.Add(c.Symbol, c.Sure ?? 1.0);
                    else others.Add((c.Symbol.Root, c.Symbol.Quality), c.Sure ?? 1.0);
                double sure = Np.Mean(heard.Where(c => KindOf(c.Symbol) == won).Select(c => c.Sure ?? 1.0).ToArray());
                agreed[b] = (symbols.Most(), sure, others.Any ? others.Most() : null);
            }
            foreach (int n in playings)
            {
                var bar = bars[n];
                bool marked = bar.Chords[0].Sure != null;
                if (Enumerable.Range(0, beats).All(b => KindOf(on[n][b].Symbol) == KindOf(agreed[b].Symbol)))
                {
                    for (int b = beats - 1; b >= 0; b--) on[n][b].Symbol = agreed[b].Symbol;    // the same chords, named as agreed (a seventh, a bass note)
                }
                else                                                                            // written again, a chord a beat
                {
                    double step = (bar.End - bar.Start) / beats;
                    var chords = new List<Chord>();
                    for (int b = 0; b < beats; b++)
                    {
                        var (symbol, sure, _) = agreed[b];
                        double end = b < beats - 1 ? Np.Round(bar.Start + (b + 1) * step, 4) : bar.End;
                        if (chords.Count > 0 && chords[^1].Symbol == symbol)
                        {
                            chords[^1].End = end;
                            continue;
                        }
                        var chord = new Chord { Start = Np.Round(bar.Start + b * step, 4), End = end, Symbol = symbol, Fit = on[n][b].Fit };
                        if (marked)
                        {
                            chord.Sure = Np.Round(sure, 3);
                            chord.Other = KindOf(on[n][b].Symbol) == KindOf(symbol) ? on[n][b].Other : (on[n][b].Symbol.Root, on[n][b].Symbol.Quality);
                        }
                        chords.Add(chord);
                    }
                    bar.Chords = chords;
                }
                if (!marked) continue;
                for (int b = 0; b < beats; b++)
                {
                    if (agreed[b].Other is not { } other) continue;
                    double at = bar.Start + (b + 0.01) * (bar.End - bar.Start) / beats;
                    var c = bar.Chords.First(c => c.Start <= at && at < c.End);
                    c.Sure = Math.Min(c.Sure!.Value, Np.Round(doubt - 0.001, 3));
                    c.Other = other;
                }
            }
        }
        return form;
    }

    /// <summary>The sections a chart is made of, from the chord on each beat of each bar: the form (a section's
    /// name for each one played) and, for each bar, its section and which bar of it it is.
    ///
    /// Two runs of bars are the same section when <paramref name="alike"/> of their beats have the same chord. The
    /// run whose saying again saves the most bars becomes a section, the longer run if two save as many; then the
    /// same among the bars left, until nothing is said twice. What is left between sections is a section said
    /// once. Two sections that only ever come one after the other are one.</summary>
    public static (List<string> Form, (string Section, int Bar)[] Places) Find(int[][] keys, double alike = 1.0)
    {
        int n = keys.Length;
        int width = Math.Max(keys.Length == 0 ? 0 : keys.Max(k => k.Length), 1);
        var same = new double[n, n];        // bar against bar: the share of their beats that have the same chord
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                int equal = 0;
                for (int b = 0; b < Math.Min(keys[i].Length, keys[j].Length); b++) if (keys[i][b] == keys[j][b]) equal++;
                same[i, j] = (double)equal / width;
            }
        var close = new SortedDictionary<int, bool[,]>();   // for each length: do the runs from bar i and from bar j say the same?
        var run = new double[n, n];
        for (int length = 1; length <= n / 2; length++)
        {
            int m = n - length + 1;
            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++) run[i, j] += same[i + length - 1, j + length - 1];
            if (length < SectionBars) continue;
            var near = close[length] = new bool[m, m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++) near[i, j] = run[i, j] >= alike * length - 1e-9 && Math.Abs(i - j) >= length;
        }
        var owner = new int?[n];
        var played = new List<(int First, int Bars, int Section)>();    // each section played
        while (true)
        {
            var free = new int[n + 1];          // how many bars from here on belong to no section yet
            for (int i = n - 1; i >= 0; i--) free[i] = owner[i] != null ? 0 : free[i + 1] + 1;
            (int Saved, int Length, List<int>? Kept) best = (0, 0, null);
            foreach (var (length, near) in close)
            {
                int m = near.GetLength(0);
                int least = Math.Max(best.Saved, 1);
                for (int i = 0; i < m; i++)
                {
                    if (free[i] < length) continue;
                    int reach = 0;
                    for (int j = 0; j < m; j++) if (near[i, j] && free[j] >= length) reach++;
                    if (reach * length < least) continue;
                    var kept = new List<int> { i };
                    for (int j = 0; j < m; j++)
                        if (near[i, j] && free[j] >= length && kept.All(k => Math.Abs(j - k) >= length)) kept.Add(j);
                    int saved = (kept.Count - 1) * length;
                    if (saved > best.Saved || (saved == best.Saved && length > best.Length)) best = (saved, length, kept);
                }
            }
            if (best.Saved == 0) break;
            int section = played.Count;
            foreach (int s in best.Kept!)
            {
                played.Add((s, best.Length, section));
                for (int i = s; i < s + best.Length; i++) owner[i] = section;
            }
        }
        for (int i = 0; i < n;)                 // what is said once
        {
            if (owner[i] != null) { i++; continue; }
            int j = i;
            while (j < n && owner[j] == null) j++;
            played.Add((i, j - i, played.Count));
            i = j;
        }
        played.Sort();
        while (true)                            // A B ... A B: one section
        {
            var order = played.Select(p => p.Section).ToList();
            var count = order.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
            var pairs = new List<(int A, int B)>();
            var times = new Dictionary<(int, int), int>();
            for (int i = 0; i + 1 < order.Count; i++)
            {
                var pair = (order[i], order[i + 1]);
                if (!times.TryGetValue(pair, out int k)) pairs.Add(pair);
                times[pair] = k + 1;
            }
            int found = pairs.FindIndex(p => p.A != p.B && times[p] == count[p.A] && count[p.A] == count[p.B]);
            if (found < 0) break;
            int first = pairs[found].A;
            var merged = new List<(int First, int Bars, int Section)>();
            for (int i = 0; i < played.Count; i++)
            {
                if (played[i].Section == first)
                {
                    merged.Add((played[i].First, played[i].Bars + played[i + 1].Bars, first));
                    i++;
                }
                else merged.Add(played[i]);
            }
            played = merged;
        }
        var names = new Dictionary<int, string>();
        foreach (var (_, _, s) in played)
            if (!names.ContainsKey(s)) names[s] = names.Count < 26 ? ((char)('A' + names.Count)).ToString() : $"S{names.Count + 1}";
        var places = new List<(string, int)>();
        foreach (var (_, barCount, s) in played)
            for (int k = 0; k < barCount; k++) places.Add((names[s], k + 1));
        return ([.. played.Select(p => names[p.Section])], [.. places]);
    }
}
