using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Orecchiante;

/// <summary>The lyrics put to the voice (Shosa's request 1, step 2; README "The words put to the voice"): the words, in
/// kana (Kana), forced onto the separated vocals by a CTC speech model's probabilities of each kana (Model) and Viterbi,
/// one mora a note (scratch/align/align.py's steps, which say what each one is, with its chosen values: ー a note of its
/// own, っ part of the mora before, ん a note, the onsets 50 ms late, a note ending where the voice stops for 0.12 s
/// or at the next). Words in English (English.Read) go the same way with English.Model: a syllable a note, its
/// letters the targets (README "The words in English").</summary>
static partial class Words
{
    /// <summary>reazon-research/japanese-wav2vec2-base-rs35kh (Apache-2.0) as ONNX, its MatMuls in int8
    /// (tools/d2/reazon_onnx.py): 16 kHz samples in, the log probabilities of the blank and the kana out, 20 ms a
    /// frame; the kana are in the model's metadata ("tokens").</summary>
    public const string Model = "reazon-kana-mm8.onnx";
    public const int Rate = 16000;
    const int Stride = 320;                     // samples a frame
    const double Chunk = 20, Pad = 2;           // seconds a run of the model, and heard on each side of it
    const double Centre = 0.0125, Lag = 0.05;   // a frame's centre after its start; the morae come early (README)
    const double Gate = 5, QuietDb = 30, Hold = 0.12;
    const int HRate = Features.Rate, HHop = 256;

    /// <summary>Lyrics enough to put to the voice: kana for most of them (a song in English is left to the melody alone).</summary>
    public static bool Sung(List<Mora> morae) => morae.Count(m => m.Kana != Kana.Any) >= Math.Max(20, 3 * morae.Count(m => m.Kana == Kana.Any));

    /// <summary>The model's log probabilities, [frames, 1 + kana], and the kana, in chunks of Chunk s heard with Pad s
    /// on each side (the pad's frames dropped).</summary>
    public static (float[,] LogP, string[] Tokens) Emit(InferenceSession model, float[] y)
    {
        var tokens = JsonSerializer.Deserialize<string[]>(model.ModelMetadata.CustomMetadataMap["tokens"])!;
        int n = y.Length, c = (int)(Chunk * Rate), p = (int)(Pad * Rate);
        int Frame(int samples) => (int)Math.Round(samples / (double)Stride, MidpointRounding.ToEven);
        var parts = new List<(float[] Data, int Cols, int From, int To)>();
        int total = 0;
        for (int a = 0; a < n; a += c)
        {
            int lo = Math.Max(a - p, 0), hi = Math.Min(a + c + p, n);
            var x = new DenseTensor<float>(y.AsMemory(lo, hi - lo), [1, hi - lo]);
            using var r = model.Run([NamedOnnxValue.CreateFromTensor("wave", x)]);
            var t = r[0].AsTensor<float>();
            int frames = t.Dimensions[0], cols = t.Dimensions[1];
            int from = Frame(a - lo), to = Math.Min(Frame(Math.Min(a + c, n) - lo), frames);
            parts.Add((t.ToArray(), cols, from, to));
            total += Math.Max(to - from, 0);
        }
        int width = parts[0].Cols;
        var logp = new float[total, width];
        int row = 0;
        foreach (var (data, cols, from, to) in parts)
            for (int f = from; f < to; f++, row++)
                for (int k = 0; k < cols; k++) logp[row, k] = data[f * cols + k];
        return (logp, tokens);
    }

    /// <summary>What the blank's log probability gives up when the words are heard (Heard): fewest kana wrong on RWC-P
    /// 1-50's whole lyrics (0: 0.68 of the kana wrong, as many heard as half the words; -4: 0.52; -6: 0.69).</summary>
    const float HeardBlank = -4;

    /// <summary>The words as the model hears them, with no lyrics (Shosa's request 1 ③, 2026-11-28): the likeliest token
    /// every frame, a repeat once, no blank, cut in morae. Half the kana are wrong (RWC-P 51-80: 0.48 of the kana, 0.30
    /// of the vowels), so they are not written: the alignment of them only places the notes, a syllable each
    /// (README "The words heard"). English: the English model's letters, its word breaks "|", cut in syllables by
    /// the spelling (English.Read).</summary>
    public static List<Mora> Heard((float[,] LogP, string[] Tokens) emitted, bool english = false)
    {
        var (lp, tokens) = emitted;
        int T = lp.GetLength(0), C = lp.GetLength(1);
        var heard = new System.Text.StringBuilder();
        int last = 0;
        for (int t = 0; t < T; t++)
        {
            int best = 0;
            float most = lp[t, 0] + HeardBlank;
            for (int k = 1; k < C; k++) if (lp[t, k] > most) { most = lp[t, k]; best = k; }
            if (best != last && best != 0) heard.Append(tokens[best - 1]);
            last = best;
        }
        if (english) return English.Read([heard.ToString().Replace("|", " ")]);
        return [.. Kana.Morae(heard.ToString().Replace("▁", "")).Select(m => new Mora(m))];
    }

    /// <summary>CTC forced alignment: e [T, C] log probabilities (column 0 the blank), y the targets' columns; each
    /// target's first frame. Ties as align.py's argmax: stay, then from the one before, then from two before.</summary>
    public static int[] Viterbi(double[,] e, int[] y)
    {
        int T = e.GetLength(0), L = y.Length, S = 2 * L + 1;
        var col = new int[S];
        var skip = new bool[S];
        for (int j = 0; j < L; j++) col[2 * j + 1] = y[j];
        for (int s = 3; s < S; s += 2) skip[s] = y[s / 2] != y[s / 2 - 1];
        const double Neg = -1e30;
        var a = new double[S];
        var b = new double[S];
        Array.Fill(a, Neg);
        a[0] = e[0, 0];
        if (S > 1) a[1] = e[0, y[0]];
        var back = new sbyte[T, S];
        for (int t = 1; t < T; t++)
        {
            for (int s = 0; s < S; s++)
            {
                double best = a[s];
                sbyte k = 0;
                double s1 = s >= 1 ? a[s - 1] : Neg;
                if (s1 > best) { best = s1; k = 1; }
                double s2 = s >= 2 && skip[s] ? a[s - 2] : Neg;
                if (s2 > best) { best = s2; k = 2; }
                b[s] = best + e[t, col[s]];
                back[t, s] = k;
            }
            (a, b) = (b, a);
        }
        int st = S - 1 >= 1 && a[S - 1] < a[S - 2] ? S - 2 : S - 1;
        var first = new int[L];
        Array.Fill(first, -1);
        for (int t = T - 1; t >= 0; t--)
        {
            if (st % 2 == 1) first[st / 2] = t;
            st -= back[t, st];
        }
        return first;
    }

    /// <summary>A sung note and what it sings.</summary>
    public sealed record Note(double On, double Off, int Midi, string Syllable);

    /// <summary>The words' notes on the vocals: vocals at Rate for the model, at Features.Rate for the voice's own
    /// pitch, loudness and melody (Melody.Pitch, Melody.NotesOf), as align.py makes them.</summary>
    /// <param name="english">The words are English syllables (English.Read) and the model English's letters: a syllable
    /// is its letters, the word break "|" between words (scratch/align/align_en.py WB=1), no lag; a syllable going on to
    /// the next of its word ends in "-" (Lily#'s `Hap- py`).</param>
    /// <param name="strength">The vocals' onset strength on f0's frames (64 mel bands to 8000 Hz): in English each
    /// syllable's onset moves to where the voice starts near it (Refine).</param>
    /// <param name="emitted">The model's output on these vocals when it is already made (Heard), else made here.</param>
    public static List<Note> Align(InferenceSession model, float[] vocals16, double[] f0, bool[] voiced, double[] rmsDb, List<(double On, double Off, int Midi)> heard, List<Mora> morae, bool english = false, float[]? strength = null, (float[,] LogP, string[] Tokens)? emitted = null)
    {
        var (lp, tokens) = emitted ?? Emit(model, vocals16);
        int T = lp.GetLength(0), K = tokens.Length;
        var index = new Dictionary<string, int>();
        for (int i = 0; i < K; i++) index[tokens[i]] = i + 1;
        index[Kana.Any] = K + 1;                              // any kana: the column appended below
        // the targets: each mora's characters (a kana the model lacks dropped); in English the letters, and "|" before a word
        var y = new List<int>();
        var owner = new List<int>();
        for (int i = 0; i < morae.Count; i++)
        {
            if (english && i > 0 && !morae[i].Joined && index.TryGetValue("|", out int wb)) { y.Add(wb); owner.Add(-1); }
            foreach (char ch in english ? morae[i].Kana.ToUpperInvariant() : morae[i].Kana)
                if (index.TryGetValue(ch.ToString(), out int k)) { y.Add(k); owner.Add(i); }
        }
        if (y.Count == 0 || T < y.Count) return [];
        // where the vocals are QuietDb under their loud frames, every kana costs Gate more than the blank
        double step = (double)Stride / Rate;
        var e = new double[T, K + 2];
        for (int t = 0; t < T; t++)
        {
            double at = t * step + Centre;
            bool quiet = Interp(rmsDb, at * HRate / HHop) < -QuietDb;
            double most = double.NegativeInfinity;
            e[t, 0] = lp[t, 0];
            for (int k = 1; k <= K; k++)
            {
                e[t, k] = lp[t, k] - (quiet ? Gate : 0);
                most = Math.Max(most, e[t, k]);
            }
            e[t, K + 1] = most;
        }
        var first = Viterbi(e, [.. y]);
        var start = new SortedDictionary<int, int>();
        for (int j = 0; j < owner.Count; j++) if (owner[j] >= 0) start.TryAdd(owner[j], first[j]);
        var times = start.Select(p => (Mora: morae[p.Key], At: p.Value * step + Centre + (english ? 0 : Lag),
                                       Goes: english && p.Key + 1 < morae.Count && morae[p.Key + 1].Joined)).ToList();
        if (english && strength != null)
        {
            var moved = Refine([.. times.Select(x => x.At)], strength);
            for (int i = 0; i < times.Count; i++) times[i] = times[i] with { At = moved[i] };
        }
        // which morae are notes: っ joins the mora before; ー sings that mora's vowel again
        var ons = new List<(Mora Mora, double At, string Syllable)>();
        foreach (var (m, at, goes) in times)
        {
            if (english) { ons.Add((m, at, m.Kana + (goes ? "-" : ""))); continue; }
            if (m.Kana == "っ") { if (ons.Count > 0) ons[^1] = ons[^1] with { Syllable = ons[^1].Syllable + "っ" }; continue; }
            string syllable = m.Kana == "ー" ? (ons.Count > 0 ? Kana.Vowel(ons[^1].Syllable) ?? "ー" : "ー") : m.Kana;
            ons.Add((m, at, syllable));
        }
        // the ends: the next note, or Hold of unvoiced frames before it; the pitch, the voiced frames' median
        int Hf(double t) => Math.Clamp((int)Math.Round(t * HRate / HHop, MidpointRounding.ToEven), 0, voiced.Length - 1);
        int hold = Math.Max((int)Math.Round(Hold * HRate / HHop, MidpointRounding.ToEven), 1);
        var notes = new List<Note>();
        for (int k = 0; k < ons.Count; k++)
        {
            var (m, t, syllable) = ons[k];
            double next = k + 1 < ons.Count ? ons[k + 1].At : t + 1.0;
            if (m.Kana == Kana.Any)
            {
                // words not read: the melody's notes in their place, the words one to a note
                var inside = heard.Where(x => t - 0.05 <= x.On && x.On < next - 0.03).ToList();
                var said = (m.Words ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < inside.Count; i++)
                {
                    string s = i < said.Length ? (i == inside.Count - 1 && said.Length > inside.Count ? string.Join("~", said[i..]) : said[i]) : "~";
                    notes.Add(new Note(inside[i].On, Math.Min(inside[i].Off, next), inside[i].Midi, s));
                }
                continue;
            }
            int a = Hf(t), b = Hf(next), end = b, run = 0;
            for (int j = a; j < b; j++)
            {
                run = voiced[j] ? 0 : run + 1;
                if (run >= hold && j - run + 1 > a + 1) { end = j - run + 1; break; }
            }
            var pitches = new List<double>();
            for (int j = a; j < end; j++) if (voiced[j]) pitches.Add(69 + 12 * Math.Log2(f0[j] / 440));
            double midi = pitches.Count > 0 ? Median(pitches) : 60.0;
            // printed one way and sung another (Mora.Shown): the particles with Lily#'s reading, `は[わ]`; a word of
            // Kana.Sung ("Ah") as written, its kana only for the alignment — the singing voice makes it singable (the
            // owner, 2026-11-27: Lily#'s voice to correct it, not the chart; Lily# REQUESTS 29)
            string label = m.Shown == null ? syllable : m.Shown.Any(char.IsAsciiLetter) ? m.Shown : $"{m.Shown}[{syllable}]";
            notes.Add(new Note(t, Math.Max(end * (double)HHop / HRate, t + 0.03), (int)Math.Round(midi, MidpointRounding.ToEven), label));
        }
        return notes;
    }

    const double Reach = 0.25, PeakLate = 0.02, PerSecond = 10;

    /// <summary>English onsets moved to where the voice starts (scratch/align/refine.py, REFINE=0.25 RLAG=-0.02): each
    /// to the best peak of the onset strength within Reach s of it and not past the middle to the next onset, a peak's
    /// worth its strength over the median peak's less PerSecond a second away, peaks under the median left out; the
    /// onset PeakLate before the peak. On RWC's English songs the onsets on the right 16th 0.659 to 0.713 (odd and
    /// even songs alike); in Japanese it does harm (0.831 to 0.81 and less), the alignment being nearer already.</summary>
    static double[] Refine(double[] ons, float[] env)
    {
        var pt = new List<double>();
        var pv = new List<double>();
        var all = new List<double>();
        for (int i = 1; i + 1 < env.Length; i++)
            if (env[i] > env[i - 1] && env[i] >= env[i + 1]) all.Add(env[i]);
        if (all.Count == 0) return ons;
        double med = Median([.. all]);
        for (int i = 1; i + 1 < env.Length; i++)
            if (env[i] > env[i - 1] && env[i] >= env[i + 1] && env[i] / Math.Max(med, 1e-9) >= 1.0)
            {
                pt.Add(i * (double)HHop / HRate);
                pv.Add(env[i] / Math.Max(med, 1e-9));
            }
        var res = new double[ons.Length];
        for (int k = 0; k < ons.Length; k++)
        {
            double t = ons[k];
            double lo = Math.Max(t - Reach, k > 0 ? (ons[k - 1] + t) / 2 : double.NegativeInfinity);
            double hi = Math.Min(t + Reach, k + 1 < ons.Length ? (t + ons[k + 1]) / 2 : double.PositiveInfinity);
            int best = -1;
            double score = double.NegativeInfinity;
            for (int j = LowerBound(pt, lo); j < pt.Count && pt[j] < hi; j++)
            {
                double s = pv[j] - PerSecond * Math.Abs(pt[j] - t);
                if (s > score) { score = s; best = j; }
            }
            res[k] = best >= 0 ? pt[best] - PeakLate : t;
        }
        return res;
    }

    static int LowerBound(List<double> v, double x)
    {
        int lo = 0, hi = v.Count;
        while (lo < hi) { int m = (lo + hi) / 2; if (v[m] < x) lo = m + 1; else hi = m; }
        return lo;
    }

    static double Interp(double[] v, double x)
    {
        if (x <= 0) return v[0];
        if (x >= v.Length - 1) return v[^1];
        int i = (int)x;
        return v[i] + (v[i + 1] - v[i]) * (x - i);
    }

    static double Median(List<double> v)
    {
        v.Sort();
        int n = v.Count;
        return n % 2 == 1 ? v[n / 2] : (v[n / 2 - 1] + v[n / 2]) / 2;
    }

    /// <summary>The notes on the grid of 16ths (Bass.RoundOnto with a 16th off the eighths only when the onset is within
    /// a quarter of a 16th of it, as measured): two notes on one 16th are one note singing both (Lily#'s elision, ~).</summary>
    public static List<(int First, int Last, int Midi, string Syllable)> RoundOnto(double[] slots, List<Note> notes, double pull = 0.25)
    {
        var at = new SortedDictionary<int, (int End, int Midi, string Syllable)>();
        foreach (var n in notes)
        {
            int i = Bass.Nearest(slots, n.On);
            if (i % 2 == 1 && Math.Abs(n.On - slots[i]) > pull * (slots[Math.Min(i + 1, slots.Length - 1)] - slots[i]))
                i = n.On < slots[i] || i + 1 >= slots.Length ? i - 1 : i + 1;
            int e = Math.Max(Bass.Nearest(slots, n.Off), i + 1);
            if (!at.TryGetValue(i, out var had)) at[i] = (e, n.Midi, n.Syllable);
            else at[i] = (Math.Max(had.End, e), had.End - i < e - i ? n.Midi : had.Midi,
                          had.Syllable.EndsWith('-') ? had.Syllable[..^1] + n.Syllable : had.Syllable + "~" + n.Syllable);   // two syllables of a word: one
        }
        var keys = at.Keys.ToList();
        var res = new List<(int, int, int, string)>();
        for (int k = 0; k < keys.Count; k++)
        {
            var (e, p, s) = at[keys[k]];
            if (k + 1 < keys.Count)
            {
                int next = keys[k + 1];
                if (next - e <= 1) e = next;
                e = Math.Min(e, next);
            }
            res.Add((keys[k], e, p, s));
        }
        return res;
    }

    [GeneratedRegex(@"^(.*?)(\d*)$")] private static partial Regex NameNumber();

    /// <summary>Each time a sung section comes round again, a section of its own: the words (and the voice's notes)
    /// differ from one time to the next, and a section is written as it is first played (Writer). Named on from the
    /// section's letter as the sections played differently already are (A, A2, ...: the next number free).</summary>
    public static void Unfold(Chart chart)
    {
        var times = new List<List<Bar>>();
        foreach (var bar in chart.Bars)
        {
            if (times.Count == 0 || bar.BarOfSection == 1 || bar.Section != times[^1][0].Section) times.Add([]);
            times[^1].Add(bar);
        }
        if (times.Count != chart.Form.Count || times.Where((t, i) => t[0].Section != chart.Form[i]).Any()) return;    // not as expected: left as it is
        var used = new Dictionary<string, int>();
        foreach (var name in chart.Form.Distinct())
        {
            var m = NameNumber().Match(name);
            int n = m.Groups[2].Value == "" ? 1 : int.Parse(m.Groups[2].Value);
            used[m.Groups[1].Value] = Math.Max(used.GetValueOrDefault(m.Groups[1].Value), n);
        }
        var sung = times.GroupBy(t => t[0].Section!).Where(g => g.Count() > 1 && g.Any(t => t.Any(b => !string.IsNullOrEmpty(b.Lyrics))))
                        .Select(g => g.Key).ToHashSet();
        var seen = new HashSet<string>();
        for (int i = 0; i < times.Count; i++)
        {
            string name = times[i][0].Section!;
            if (!sung.Contains(name) || seen.Add(name)) continue;
            string letter = NameNumber().Match(name).Groups[1].Value;
            string fresh = letter + (used[letter] += 1);
            foreach (var bar in times[i]) bar.Section = fresh;
            chart.Form[i] = fresh;
        }
    }

    [GeneratedRegex(@"^\[(ti|ar|al|au|by|length|offset|re|ve|#):.*\]$", RegexOptions.IgnoreCase)] private static partial Regex LrcTag();
    [GeneratedRegex(@"\[\d+:\d+(?:[.:]\d+)?\]")] private static partial Regex LrcTime();
    [GeneratedRegex(@"<\d+:\d+(?:[.:]\d+)?>")] private static partial Regex LrcWordTime();

    /// <summary>The sung lines of lyrics: a .txt a line a line, an .lrc without its tags and times (a line with several
    /// times is sung that many times, in the order of its times among the others).</summary>
    public static List<string> LinesOf(string path)
    {
        var raw = File.ReadAllLines(path).Select(l => l.Replace('　', ' ').Trim()).ToList();
        if (!path.EndsWith(".lrc", StringComparison.OrdinalIgnoreCase)) return [.. raw.Where(l => l.Length > 0)];
        var timed = new List<(double At, int Order, string Text)>();
        var plain = new List<string>();
        foreach (var line in raw)
        {
            if (line.Length == 0 || LrcTag().IsMatch(line)) continue;
            var times = LrcTime().Matches(line);
            string text = LrcWordTime().Replace(LrcTime().Replace(line, ""), "").Trim();
            if (times.Count == 0) { if (text.Length > 0) plain.Add(text); continue; }
            if (text.Length == 0) continue;
            foreach (Match m in times) timed.Add((Seconds(m.Value), timed.Count, text));
        }
        return timed.Count > 0 ? [.. timed.OrderBy(t => t.At).ThenBy(t => t.Order).Select(t => t.Text)] : plain;
    }

    /// <summary>[mm:ss.xx] in seconds (or [mm:ss:xx], which LrcTime takes too: until 0.18.0 its hundredths were lost).</summary>
    internal static double Seconds(string tag)
    {
        var p = tag.Trim('[', ']', '<', '>').Split(':');
        return int.Parse(p[0]) * 60 + double.Parse(string.Join('.', p[1..]), System.Globalization.CultureInfo.InvariantCulture);
    }
}
