using System.Reflection;
using System.Text.Json;

namespace Orecchiante;

/// <summary>The sections' cuts chosen whole: each bar line's cues, weighed as the owner cut sections in Transcribe!
/// (sections_dp.json, tools/sections_dp.json embedded), and the owner's section lengths. tools/read_chords.py's
/// section_cues(), audio_cues(), section_rows(), played_again() and choose_cuts(), step for step.</summary>
static class Sections
{
    public static readonly string[] Whys = ["sing", "turn", "played", "ended", "cadence", "pair", "begins"];
    const double PhraseAlike = 0.75;

    /// <param name="Onset">0.13.0: OnsetCues weighed too; <paramref name="Headmatch"/>: HeadCues of the heads of
    /// <paramref name="Pass1"/>'s reading; <paramref name="Align"/>: AlignHeads; <paramref name="Plus1"/>: Choose's one bar more.</param>
    sealed record Model(double Lam, double Off, double Mu, int Neighbours, double Bias, double[] Weights, Dictionary<int, int> Lengths, bool Drums,
        bool Onset, bool Headmatch, bool Align, bool Plus1, double Plus1Cost, Model? Pass1, int AlignWide = 1, double? OffUnheard = null);

    static Model Read(JsonElement r)
    {
        static bool Flag(JsonElement r, string name) => r.TryGetProperty(name, out var v) && v.GetBoolean();
        return new Model(r.GetProperty("lam").GetDouble(), r.GetProperty("off").GetDouble(), r.GetProperty("mu").GetDouble(),
            r.GetProperty("neighbours").GetInt32(), r.GetProperty("bias").GetDouble(),
            [.. r.GetProperty("weights").EnumerateArray().Select(w => w.GetDouble())],
            r.GetProperty("lengths").EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => p.Value.GetInt32()),
            Flag(r, "drums"), Flag(r, "onset"), Flag(r, "headmatch"), Flag(r, "align"), Flag(r, "plus1"),
            r.TryGetProperty("plus1_cost", out var c) ? c.GetDouble() : 0.0,
            r.TryGetProperty("pass1", out var p) ? Read(p) : null,
            r.TryGetProperty("align_wide", out var w) ? w.GetInt32() : 1,
            r.TryGetProperty("off_unheard", out var u) ? u.GetDouble() : null);
    }

    static readonly Lazy<Model> model = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("sections_dp.json")
            ?? throw new InvalidOperationException("sections_dp.json is not in the program");
        using var doc = JsonDocument.Parse(stream);
        return Read(doc.RootElement);
    });

    /// <summary>The cuts (bars 1 .. n-1). <paramref name="fam"/>: each beat's root * 4 + family; <paramref name="keys"/>:
    /// each beat's chord (quality * 12 + root); <paramref name="quarters"/>: the sung share of each quarter of each bar
    /// (null: no singing heard); <paramref name="why"/>: the rule each of the rules' cuts came from; the rest as
    /// Form.Phrases was given them. With a first reading (0.13.0, the model's pass1), its heads are what HeadCues sees.</summary>
    public static List<int> Cuts(int[][] fam, int[][] keys, double[][]? quarters, int tonic, int home, double[]? sung, Dictionary<int, string> why,
        HashSet<int> sings, HashSet<int> begins, HashSet<int> turns, double[] db, double[][] notes, (double[] Db, double[][] Pattern)? drums = null)
    {
        var m = model.Value;
        var again = PlayedAgain(fam);
        List<int> Chosen(Model md, List<int>? heads)
        {
            var rows = Rows(fam, keys, quarters, heads, tonic, home, sung, why, sings, begins, turns, db, notes, drums, md);
            var score = new double[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                double s = 0;
                for (int k = 0; k < rows[i].Length; k++) s += rows[i][k] * md.Weights[k];
                score[i] = Math.Round((s + md.Bias) * 1e9, MidpointRounding.ToEven) / 1e9;   // numpy's round(x, 9)
            }
            // no parts heard (no drums: measuring without the voice, the songs made here): the model's off_unheard,
            // so as many cuts as with them (read_chords.phrase_sections)
            double off = drums == null && md.Drums ? md.OffUnheard ?? md.Off : md.Off;
            List<int> Redo(double[] s) => Choose(s, md, again, off);
            var cuts = Redo(score);
            return md.Align ? AlignHeads(fam, score, cuts, Redo, wide: md.AlignWide) : cuts;
        }
        List<int>? first = m.Pass1 is { } p1 ? [0, .. Chosen(p1, null)] : null;
        return Chosen(m, first);
    }

    static double[][] Rows(int[][] fam, int[][] keys, double[][]? quarters, List<int>? heads, int tonic, int home, double[]? sung, Dictionary<int, string> why,
        HashSet<int> sings, HashSet<int> begins, HashSet<int> turns, double[] db, double[][] notes, (double[] Db, double[][] Pattern)? drums, Model md)
    {
        int n = fam.Length, nb = md.Neighbours;
        var cues = Cues(fam, tonic, home, sung, why, sings, begins, turns);
        var audio = Audio(db, notes, fam);
        var headed = md.Headmatch ? HeadCues(fam, heads ?? []) : null;
        var onset = md.Onset ? OnsetCues(keys, quarters) : null;
        var drummed = Drummed(drums, n, md.Drums);
        var x = new double[n][];
        for (int i = 0; i < n; i++) x[i] = [.. cues[i], .. audio[i], .. (headed?[i] ?? []), .. (onset?[i] ?? []), .. drummed[i]];
        int width = x.Length > 0 ? x[0].Length : 0;
        var rows = new double[n][];
        for (int i = 0; i < n; i++)
        {
            var row = new List<double>(x[i]);
            for (int k = -nb; k <= nb; k++)
            {
                if (k == 0) continue;
                int j = i + k;
                row.AddRange(0 <= j && j < n ? x[j] : new double[width]);
            }
            rows[i] = [.. row];
        }
        return rows;
    }

    static bool Alike(int[][] fam, int a, int b, int length)
    {
        int n = fam.Length;
        if (a < 0 || b < 0 || a + length > n || b + length > n) return false;
        int same = 0, all = 0;
        for (int i = 0; i < length; i++)
            for (int j = 0; j < Math.Min(fam[a + i].Length, fam[b + i].Length); j++) { all++; if (fam[a + i][j] == fam[b + i][j]) same++; }
        return all > 0 && same >= PhraseAlike * all;
    }

    static double[][] Cues(int[][] fam, int tonic, int home, double[]? sung, Dictionary<int, string> why,
        HashSet<int> sings, HashSet<int> begins, HashSet<int> turns)
    {
        int n = fam.Length;
        var s = new double[n];
        if (sung != null) for (int i = 0; i < n; i++) s[i] = double.IsNaN(sung[i]) ? 0.0 : sung[i];
        var rows = new double[n][];
        for (int i = 0; i < n; i++)
        {
            var f = new List<double>();
            string? w = why.GetValueOrDefault(i);
            foreach (string kind in Whys) f.Add(w == kind ? 1.0 : 0.0);
            f.Add(sings.Contains(i) ? 1.0 : 0.0);
            f.Add(begins.Contains(i) ? 1.0 : 0.0);
            f.Add(turns.Contains(i) ? 1.0 : 0.0);
            foreach (int p in (int[])[4, 8])
            {
                f.Add(Alike(fam, i - 2 * p, i - p, p) && !Alike(fam, i - p, i, p) ? 1.0 : 0.0);
                f.Add(Alike(fam, i - p, i, p) ? 1.0 : 0.0);
                var hs = new List<int>();
                if (i + p <= n)
                    for (int h = 1; h < n - p + 1; h++) if (Math.Abs(h - i) >= p && Alike(fam, i, h, p)) hs.Add(h);
                f.Add(Math.Min(hs.Count, 4) / 4.0);
                f.Add(hs.Count > 0 ? (double)hs.Count(h => !Alike(fam, i - 1, h - 1, 1)) / hs.Count : 0.0);
            }
            bool across = 0 < i && i < n;
            int last = across ? fam[i - 1][^1] : -1, first = across ? fam[i][0] : -1;
            bool majOrSus = across && (last % 4 == 0 || last % 4 == 2);
            f.Add(across && last / 4 == (tonic + 7) % 12 && majOrSus && first == home ? 1.0 : 0.0);
            f.Add(across && last == first ? 1.0 : 0.0);
            f.Add(across && first == home ? 1.0 : 0.0);
            f.Add(across && fam[i - 1].Distinct().Count() == 1 && majOrSus && last / 4 == (first / 4 + 7) % 12 ? 1.0 : 0.0);
            double s0 = i > 0 ? s[i - 1] : 0.0, s1 = s[i];
            f.Add(s1 - s0);
            f.Add(Math.Abs(s1 - s0));
            f.Add(s1);
            f.Add(s0);
            rows[i] = [.. f];
        }
        return rows;
    }

    static double[][] Audio(double[] db, double[][] notes, int[][] fam)
    {
        int n = fam.Length;
        double median = Np.Median(db), mean = Np.Mean(db);
        var sq = db.Select(v => (v - mean) * (v - mean)).ToArray();
        double std = Math.Sqrt(Np.Mean(sq));
        var z = db.Select(v => (v - median) / (std + 1e-9)).ToArray();
        var sets = fam.Select(row => row.ToHashSet()).ToArray();
        var rows = new double[n][];
        for (int i = 0; i < n; i++)
        {
            var f = new List<double> { z[i] };
            foreach (int w in (int[])[2, 4])
            {
                if (i - w < 0 || i + w > n) { f.AddRange([0.0, 0.0, 0.0, 0.0]); continue; }
                double after = 0, before = 0;
                for (int k = i; k < i + w; k++) after += z[k];
                for (int k = i - w; k < i; k++) before += z[k];
                double dd = after / w - before / w;
                var u = new double[12];
                var v = new double[12];
                for (int k = i - w; k < i; k++) for (int c = 0; c < 12; c++) u[c] += notes[k][c];
                for (int k = i; k < i + w; k++) for (int c = 0; c < 12; c++) v[c] += notes[k][c];
                double uv = 0, uu = 0, vv = 0;
                for (int c = 0; c < 12; c++) { uv += u[c] * v[c]; uu += u[c] * u[c]; vv += v[c] * v[c]; }
                double cos = uv / (Math.Sqrt(uu) * Math.Sqrt(vv) + 1e-9);
                var heardBefore = new HashSet<int>();
                for (int k = i - w; k < i; k++) heardBefore.UnionWith(sets[k]);
                var heardAfter = new HashSet<int>();
                for (int k = i; k < i + w; k++) heardAfter.UnionWith(sets[k]);
                int fresh = heardAfter.Count(c => !heardBefore.Contains(c));
                f.AddRange([dd, Math.Abs(dd), 1 - cos, (double)fresh / Math.Max(heardAfter.Count, 1)]);
            }
            rows[i] = [.. f];
        }
        return rows;
    }

    /// <summary>How the drums change across each bar line (when the weights were fitted with them): the bar's loudness
    /// against the song's, over 2 and 4 bars how much louder after than before and how far the mean pattern after is
    /// from the one before (cosine), and the bar's pattern against the bar before. None heard: all 0.</summary>
    static double[][] Drummed((double[] Db, double[][] Pattern)? drums, int n, bool fitted)
    {
        var rows = new double[n][];
        if (!fitted) { for (int i = 0; i < n; i++) rows[i] = []; return rows; }
        if (drums is not var (db, pattern)) { for (int i = 0; i < n; i++) rows[i] = new double[6]; return rows; }
        double median = Np.Median(db), mean = Np.Mean(db);
        double std = Math.Sqrt(Np.Mean(db.Select(v => (v - mean) * (v - mean)).ToArray()));
        var z = db.Select(v => (v - median) / (std + 1e-9)).ToArray();
        static double[] Mean(double[][] rows, int from, int to)
        {
            var m = new double[rows[from].Length];
            for (int k = from; k < to; k++) for (int c = 0; c < m.Length; c++) m[c] += rows[k][c];
            for (int c = 0; c < m.Length; c++) m[c] /= to - from;
            return m;
        }
        static double Dot(double[] u, double[] v) { double s = 0; for (int c = 0; c < u.Length; c++) s += u[c] * v[c]; return s; }
        for (int i = 0; i < n; i++)
        {
            var f = new List<double> { z[i] };
            foreach (int w in (int[])[2, 4])
            {
                if (i - w < 0 || i + w > n) { f.AddRange([0.0, 0.0]); continue; }
                double after = 0, before = 0;
                for (int k = i; k < i + w; k++) after += z[k];
                for (int k = i - w; k < i; k++) before += z[k];
                var u = Mean(pattern, i - w, i);
                var v = Mean(pattern, i, i + w);
                f.Add(after / w - before / w);
                f.Add(1 - Dot(u, v) / (Math.Sqrt(Dot(u, u)) * Math.Sqrt(Dot(v, v)) + 1e-9));
            }
            f.Add(i > 0 ? 1 - Dot(pattern[i], pattern[i - 1]) : 0.0);
            rows[i] = [.. f];
        }
        return rows;
    }

    /// <summary>again[a][L]: bars a .. a+L-1 are played again elsewhere, as long, not overlapping.</summary>
    static double[][] PlayedAgain(int[][] fam, int longest = 63)
    {
        int n = fam.Length;
        var diag = new double[n + 1, n + 1];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                int same = 0, len = Math.Min(fam[i].Length, fam[j].Length);
                for (int k = 0; k < len; k++) if (fam[i][k] == fam[j][k]) same++;
                diag[i + 1, j + 1] = (double)same / Math.Max(fam[i].Length, 1) + diag[i, j];
            }
        var again = new double[n + 1][];
        for (int a = 0; a <= n; a++) again[a] = new double[longest + 1];
        for (int length = 2; length <= longest; length++)
            for (int a = 0; a + length <= n; a++)
                for (int h = 0; h + length <= n; h++)
                    if ((h + length <= a || h >= a + length) && (diag[a + length, h + length] - diag[a, h]) / length >= PhraseAlike)
                    {
                        again[a][length] = 1.0;
                        break;
                    }
        return again;
    }

    /// <summary>Singing that comes in halfway through a bar, and where the chords change about it (read_chords.onset_cues).</summary>
    static double[][] OnsetCues(int[][] keys, double[][]? quarters)
    {
        int n = keys.Length;
        var late = new bool[n];
        if (quarters != null)
            for (int i = 0; i < n; i++)
                late[i] = (quarters[i][0] + quarters[i][1]) / 2 < 0.15 && (quarters[i][2] + quarters[i][3]) / 2 > 0.5;
        bool Changes(int j) => 0 < j && j < n && keys[j][0] != keys[j - 1][^1];
        var rows = new double[n][];
        for (int i = 0; i < n; i++)
        {
            bool prevLate = i > 0 && late[i - 1];
            rows[i] = [late[i] && Changes(i) && !Changes(i + 1) ? 1.0 : 0.0, prevLate && Changes(i) && !Changes(i - 1) ? 1.0 : 0.0,
                       late[i] && Changes(i + 1) && !Changes(i) ? 1.0 : 0.0, prevLate && Changes(i - 1) && !Changes(i) ? 1.0 : 0.0];
        }
        return rows;
    }

    /// <summary>How many of a first reading's heads the 8 and 4 bars from here (and the 8 from a bar before and after)
    /// are played as, up to 3 (read_chords.head_cues).</summary>
    static double[][] HeadCues(int[][] fam, List<int> heads)
    {
        int n = fam.Length;
        double Count(int i, int length) => Math.Min(heads.Count(h => Math.Abs(h - i) >= length && Alike(fam, i, h, length)), 3) / 3.0;
        var rows = new double[n][];
        for (int i = 0; i < n; i++) rows[i] = [Count(i, 8), Count(i, 4), Count(i - 1, 8), Count(i + 1, 8)];
        return rows;
    }

    /// <summary>The heads of one tune put in line (read_chords.align_heads), in its order of looking. <paramref name="wide"/>
    /// (0.13.1, the model's align_wide): no tie when the bars are alike at another offset too, out to this many.</summary>
    static List<int> AlignHeads(int[][] fam, double[] score, List<int> cuts, Func<double[], List<int>> redo, int length = 8, int wide = 1)
    {
        int n = fam.Length;
        var heads = new SortedSet<int>(cuts.Prepend(0)).ToList();
        var edges = heads.ToDictionary(h => h, _ => new List<(int To, int By)>());
        foreach (int a in heads)
            foreach (int b in heads)
            {
                if (b <= a || b - a < length) continue;
                var ways = Enumerable.Range(-wide, 2 * wide + 1).Where(o => Alike(fam, a, b + o, length)).ToList();
                if (ways.Count == 1 && Math.Abs(ways[0]) <= 1) { edges[a].Add((b, ways[0])); edges[b].Add((a, -ways[0])); }
            }
        var shift = new Dictionary<int, int>();
        var shiftOrder = new List<int>();
        var seen = new HashSet<int>();
        foreach (int h0 in heads)
        {
            if (seen.Contains(h0) || edges[h0].Count == 0) continue;
            var group = new Dictionary<int, int> { [h0] = 0 };
            var order = new List<int> { h0 };          // as a Python dict keeps them: in the order they came
            var stack = new List<int> { h0 };
            bool ok = true;
            while (stack.Count > 0)
            {
                int u = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                foreach (var (v, o) in edges[u])
                {
                    int want = group[u] + o;
                    if (group.TryGetValue(v, out int had)) ok = ok && had == want;
                    else { group[v] = want; order.Add(v); stack.Add(v); }
                }
            }
            seen.UnionWith(order);
            if (!ok || group.Count < 2) continue;
            double Worth(int c)
            {
                double sum = 0;
                foreach (int h in order)
                {
                    int p = h + group[h] + c;
                    if (p < 0 || p >= n) return double.NegativeInfinity;
                }
                if (group.TryGetValue(0, out int d0) && d0 + c != 0) return double.NegativeInfinity;
                foreach (int h in order)
                {
                    int p = h + group[h] + c;
                    if (p > 0) sum += score[p];
                }
                return sum;
            }
            var options = order.Select(h => -group[h]).Distinct().Order().ToList();
            int best = options[0];
            double top = Worth(best);
            foreach (int c in options.Skip(1)) { double w = Worth(c); if (w > top) (best, top) = (c, w); }
            if (double.IsNegativeInfinity(top)) continue;
            foreach (int h in order) { shift[h] = group[h] + best; shiftOrder.Add(h); }
        }
        var keep = new HashSet<int>(shift.Where(kv => kv.Key + kv.Value > 0).Select(kv => kv.Key + kv.Value));
        var drop = new HashSet<int>(shift.Where(kv => kv.Value != 0).Select(kv => kv.Key));
        if (drop.Count == 0) return cuts;
        var moved = (double[])score.Clone();
        foreach (int k in keep) moved[k] += 100;
        foreach (int k in drop) if (!keep.Contains(k)) moved[k] -= 100;
        return redo(moved);
    }

    static List<int> Choose(double[] score, Model md, double[][] again, double off)
    {
        var (lengths, lam, mu) = (md.Lengths, md.Lam, md.Mu);
        double total = lengths.Values.Sum() + 0.2 * 63;
        var logp = new double[64];
        logp[0] = -1e9;
        for (int k = 1; k < 64; k++) logp[k] = Math.Log((lengths.GetValueOrDefault(k) + 0.2) / total);
        int n = score.Length;
        var best = new double[n + 1];
        Array.Fill(best, -1e18);
        best[0] = 0.0;
        var back = new int[n + 1];
        for (int j = 1; j <= n; j++)
        {
            int lo = Math.Max(0, j - 63), k = -1;
            double top = double.NegativeInfinity;
            for (int a = lo; a < j; a++)
            {
                double cand = best[a] + lam * logp[j - a];
                if (mu != 0) cand += mu * again[a][j - a];
                int L = j - a;
                if (md.Plus1 && L >= 9 && (L - 1) % 8 == 0)     // 8, 16 … bars played elsewhere and one more (read_chords.choose_cuts)
                {
                    double said = Math.Max(again[a][L - 1], again[Math.Max(j - 9, 0)][8]);
                    cand += lam * (logp[L - 1] - logp[L]) * said - md.Plus1Cost * said;
                }
                if (k < 0 || cand > top) (k, top) = (a, cand);
            }
            best[j] = top + (j < n ? score[j] - off : 0.0);
            back[j] = k;
        }
        var cuts = new List<int>();
        for (int j = back[n]; j > 0; j = back[j]) cuts.Add(j);
        cuts.Sort();
        return cuts;
    }
}
