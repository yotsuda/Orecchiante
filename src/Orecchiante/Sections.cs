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

    sealed record Model(double Lam, double Off, double Mu, int Neighbours, double Bias, double[] Weights, Dictionary<int, int> Lengths, bool Drums);

    static readonly Lazy<Model> model = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("sections_dp.json")
            ?? throw new InvalidOperationException("sections_dp.json is not in the program");
        using var doc = JsonDocument.Parse(stream);
        var r = doc.RootElement;
        return new Model(r.GetProperty("lam").GetDouble(), r.GetProperty("off").GetDouble(), r.GetProperty("mu").GetDouble(),
            r.GetProperty("neighbours").GetInt32(), r.GetProperty("bias").GetDouble(),
            [.. r.GetProperty("weights").EnumerateArray().Select(w => w.GetDouble())],
            r.GetProperty("lengths").EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => p.Value.GetInt32()),
            r.TryGetProperty("drums", out var d) && d.GetBoolean());
    });

    /// <summary>The cuts (bars 1 .. n-1). <paramref name="fam"/>: each beat's root * 4 + family; <paramref name="why"/>:
    /// the rule each of the rules' cuts came from; the rest as Form.Phrases was given them.</summary>
    public static List<int> Cuts(int[][] fam, int tonic, int home, double[]? sung, Dictionary<int, string> why,
        HashSet<int> sings, HashSet<int> begins, HashSet<int> turns, double[] db, double[][] notes, (double[] Db, double[][] Pattern)? drums = null)
    {
        var m = model.Value;
        var rows = Rows(fam, tonic, home, sung, why, sings, begins, turns, db, notes, drums, m.Neighbours);
        var score = new double[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            double s = 0;
            for (int k = 0; k < rows[i].Length; k++) s += rows[i][k] * m.Weights[k];
            score[i] = Math.Round((s + m.Bias) * 1e9, MidpointRounding.ToEven) / 1e9;   // numpy's round(x, 9)
        }
        return Choose(score, m.Lengths, m.Lam, m.Off, PlayedAgain(fam), m.Mu);
    }

    static double[][] Rows(int[][] fam, int tonic, int home, double[]? sung, Dictionary<int, string> why,
        HashSet<int> sings, HashSet<int> begins, HashSet<int> turns, double[] db, double[][] notes, (double[] Db, double[][] Pattern)? drums, int nb)
    {
        int n = fam.Length;
        var cues = Cues(fam, tonic, home, sung, why, sings, begins, turns);
        var audio = Audio(db, notes, fam);
        var drummed = Drummed(drums, n);
        var x = new double[n][];
        for (int i = 0; i < n; i++) x[i] = [.. cues[i], .. audio[i], .. drummed[i]];
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
    static double[][] Drummed((double[] Db, double[][] Pattern)? drums, int n)
    {
        var rows = new double[n][];
        if (!model.Value.Drums) { for (int i = 0; i < n; i++) rows[i] = []; return rows; }
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

    static List<int> Choose(double[] score, Dictionary<int, int> lengths, double lam, double off, double[][] again, double mu)
    {
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
