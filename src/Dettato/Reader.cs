namespace Dettato;

/// <summary>A chord as the reader names it before the key says how it is spelt: the root and the bass as pitch
/// classes (no bass note of its own: -1), the quality as its place in <see cref="Reader.Qualities"/>.</summary>
readonly record struct Symbol(int Root, int Quality, int Bass);

sealed class Chord
{
    public double Start, End, Fit;
    public Symbol Symbol;
    public double? Sure;                    // how likely its root and family are right
    public (int Root, int Quality)? Other;  // the chord offered in its place
    public string Name = "";                // as the chart writes it, once the key is known
    public string? OtherName;
}

sealed class Bar
{
    public int Number, Pickup, BarOfSection;    // Pickup: the beats of a short bar before the first (0: a whole bar)
    public double Start, End;
    public List<double> Beats = [];             // where each of its beats begins, the first at Start
    public List<Chord> Chords = [];
    public string? Section;
}

/// <summary>What is read of a recording: docs/SIDECAR.md says what each of these is.</summary>
sealed class Chart
{
    public string Audio = "", TempoNote = "4", Time = "4/4", Key = "C major";
    public double Duration;
    public double Bpm;                          // to a tenth (Lily# writes `tempo 4 = 131.8`)
    public double? Doubt;
    /// <summary>The tempo as Python's json and f-string write it: a whole number bare, else its repr.</summary>
    public string BpmText => Bpm == Math.Floor(Bpm) ? ((long)Bpm).ToString(System.Globalization.CultureInfo.InvariantCulture) : Np.Repr(Bpm);
    public int? Tuning;                         // in cents from A = 440 Hz
    /// <summary>The parts the recording was separated into (Voice.Parts order), each the way to its file as `audio` is;
    /// null when none were written (dettato read, and so read_chords.py, write none).</summary>
    public List<(string Part, string Path)>? Parts;
    public List<string> Form = [];
    public List<Bar> Bars = [];
}

/// <summary>The rules: from the bar lines a beat tracker found, what a chord model trained on recordings says and
/// what is heard beside them (Features), the chart. This is tools/read_chords.py's read() as it runs with --ears,
/// step for step and in its order of arithmetic, so that the two can be compared file by file (tools/read_check.py).
/// Its reader with nothing learnt and the one with its own model stay in Python: they are what synthetic songs are
/// measured against, not what ships.</summary>
static class Reader
{
    const double SR = 22050, HOP = 512;
    public static readonly string[] Qualities = ["", "m", "7", "maj7", "m7", "sus4", "dim", "m7(b5)"];
    static readonly int[][] Intervals = [[0, 4, 7], [0, 3, 7], [0, 4, 7, 10], [0, 4, 7, 11], [0, 3, 7, 10], [0, 5, 7], [0, 3, 6], [0, 3, 6, 10]];
    static readonly double[] QualityCost = [0.0, 0.0, 0.02, 0.05, 0.0, 0.08, 0.04, 0.05];
    /// <summary>The family of a quality: major, minor, suspended, diminished.</summary>
    public static readonly int[] Kind = [0, 1, 0, 0, 1, 2, 3, 3];
    const int Chords = 96;                  // a chord is quality * 12 + root

    const double BeatLag = 2.0;             // frames by which the onset strength peaks after the sound begins
    const double SlashRatio = 2.0;          // a bass note that is not the root has to be this much louder than it
    // The prices of the path that go with a model trained on recordings (read_chords.py's use_ears)
    const double StayCost = 0.1, MidCost = 0.1, ElseCost = 0.25, EmitScale = 0.2;
    const double LogFloor = -8.0;
    static readonly double[] SureOf = [8.0, -2.0, 0.7, 5.5, -7.5];
    public const double Doubt = 0.84;       // a chord less sure than this is marked for a look
    const double Hush = 0.01;               // a unit this much quieter than the song's middle one is silence
    const double NoChord = 0.5;             // a unit whose "no chord" is likelier than this has no chord to name
    const double Chorded = 0.25;            // a bar at either end is in the chart when this much of it sounds a chord
    const double RingOut = 0.2;             // a bar past the last one found and this much quieter than the one before
    public static readonly Dictionary<int, string> Partial = new() { [1] = "4", [2] = "2", [3] = "2.", [4] = "1", [6] = "1." };
    const double EvenTempo = 0.1;          // the tempo is from the mean of the bars within this share of the middle one
    const double TinyBar = 0.25;            // a bar shorter than this share of the middle one is not a bar (糸: 0.08 s)
    static readonly (double Lo, double Hi) HalfBars = (0.35, 0.7);   // bars this share of the middle one are halves (PairHalves)
    const double SlowTwo = 1.5;            // a bar of two beats longer than this is a bar of four

    static readonly string[] MajorKeys = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
    static readonly string[] MinorKeys = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "G#", "A", "Bb", "B"];
    static readonly string[] SharpRoots = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    static readonly string[] FlatRoots = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];
    static readonly string[] FlatMinors = ["C", "G", "D", "F"];

    static readonly double[][] Templates = new double[Chords][];    // [chord][pitch class], of length 1
    static readonly double[][] StayAlike = new double[Chords][];    // what a bar line costs between two chords
    static readonly double[] Costs = new double[Chords];
    static readonly int[][] Same = new int[Chords][];               // the chords of the same root and family
    static readonly int[][] EarsOf = new int[Chords][];             // the chord model's chords that are this one
    static readonly int[][] EarsNone = [[169]];                     // its "N": no chord

    static Reader()
    {
        for (int k = 0; k < Chords; k++)
        {
            var t = new double[12];
            foreach (int i in Intervals[k / 12]) t[(k % 12 + i) % 12] = 1.0;
            double norm = Math.Sqrt(Intervals[k / 12].Length);
            Templates[k] = [.. t.Select(v => v / norm)];
            Costs[k] = QualityCost[k / 12];
            Same[k] = [.. Enumerable.Range(0, Chords).Where(j => j % 12 == k % 12 && Kind[j / 12] == Kind[k / 12])];
        }
        for (int a = 0; a < Chords; a++)
        {
            StayAlike[a] = new double[Chords];
            for (int b = 0; b < Chords; b++)
            {
                double alike = 0;
                for (int i = 0; i < 12; i++) alike += Templates[a][i] * Templates[b][i];
                StayAlike[a][b] = StayCost * alike;
            }
        }
        // The model names 14 qualities on each root, then "X" and "N"; an augmented chord, X and N are no chord of ours.
        string?[] earAs = ["m", "", "dim", null, "m", "", "m7", "m", "maj7", "7", "dim", "m7(b5)", "sus4", "sus4"];
        var ears = Enumerable.Range(0, Chords).Select(_ => new List<int>()).ToArray();
        for (int i = 0; i < 168; i++)
            if (earAs[i % 14] is string q) ears[Array.IndexOf(Qualities, q) * 12 + i / 14].Add(i);
        for (int k = 0; k < Chords; k++) EarsOf[k] = [.. ears[k]];
    }

    /// <summary>Bar lines found by a beat tracker (a line a beat: its seconds, a tab, its number in the bar): the
    /// lines in seconds, the end of the last bar too, and the beats in a bar. With what the chord model says
    /// (<paramref name="logp"/>), bars half as long as the rest are paired (PairHalves).</summary>
    public static (List<double> Lines, int Beats) LinesOf(string text, float[]? logp = null, int frames = 0, double step = 0)
    {
        var rows = text.Split('\n').Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Where(r => r.Length > 0).ToArray();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var times = rows.Select(r => double.Parse(r[0], inv)).ToArray();
        var numbers = rows.Select(r => (int)double.Parse(r[1], inv)).ToArray();
        var downs = Enumerable.Range(0, numbers.Length).Where(i => numbers[i] == 1).ToArray();
        if (downs.Length < 2) throw new InvalidDataException("fewer than two bar lines found");
        var counts = new List<(int Per, int Count)>();
        for (int i = 1; i < downs.Length; i++)
        {
            int d = downs[i] - downs[i - 1], at = counts.FindIndex(c => c.Per == d);
            if (at < 0) counts.Add((d, 1)); else counts[at] = (d, counts[at].Count + 1);
        }
        int per = counts.MaxBy(c => c.Count).Per;       // the first of the commonest
        // a first beat counted in the middle of a bar: two short bars that make one whole are one
        var kept = new List<int> { downs[0] };
        for (int i = 1; i < downs.Length; i++)
        {
            if (i + 1 < downs.Length && downs[i] - kept[^1] < per && downs[i + 1] - kept[^1] == per) i++;
            kept.Add(downs[i]);
        }
        downs = [.. kept];
        // a bar far shorter than the rest is two lines for one (Lily# REQUESTS 10, 2026-11-16, 糸): one goes, the shorter
        // neighbour's; as tools/read_chords.py's lines_of
        var list = downs.ToList();
        List<double> Lengths() => [.. Enumerable.Range(0, list.Count - 1).Select(k => times[list[k + 1]] - times[list[k]])];
        double middle = Np.Median(Lengths());
        while (list.Count > 2)
        {
            var s = Lengths();
            int k = s.FindIndex(x => x < TinyBar * middle);
            if (k < 0) break;
            list.RemoveAt(k == 0 ? 1 : k == s.Count - 1 || s[k - 1] <= s[k + 1] ? k : k + 1);
        }
        if (logp != null && list.Count > 3) list = PairHalves(times, list, logp, frames, step);
        downs = [.. list];
        double beat = Np.Median(Np.Diff(times));
        int last = downs[^1] + per;
        double end = last < times.Length ? times[last] : Math.Min(times[^1] + beat, times[downs[^1]] + per * beat);
        var lines = downs.Select(i => times[i]).ToList();
        if (end > times[downs[^1]] + 0.5 * per * beat) lines.Add(end);
        return (lines, per);
    }

    /// <summary>The key of what the chord model says, before any reading: FindKey on the likeliest chord of each
    /// frame that names one (read_chords.py's key_heard).</summary>
    static (int Tonic, bool Major)? KeyHeard(float[] logp, int frames, double step)
    {
        int width = logp.Length / frames;
        var chords = new List<(double Seconds, int Root, int Quality)>();
        var e = new double[width];
        var row = new double[Chords];
        for (int t = 0; t < frames; t++)
        {
            for (int i = 0; i < width; i++) e[i] = Math.Exp(logp[t * width + i]);
            int best = 0;
            for (int k = 0; k < Chords; k++)
            {
                double sum = 0;
                foreach (int i in EarsOf[k]) sum += e[i];
                row[k] = sum;
                if (sum > row[best]) best = k;
            }
            if (!(Np.Sum(row) > 0.5)) continue;
            int root = best % 12, quality = best / 12;
            if (chords.Count > 0 && chords[^1].Root == root && chords[^1].Quality == quality)
                chords[^1] = (chords[^1].Seconds + step, root, quality);
            else chords.Add((step, root, quality));
        }
        return chords.Count > 0 ? FindKey(chords) : null;
    }

    /// <summary>Bars half as long as the rest, one after the other, are the halves of bars: the tracker took the beats
    /// twice as fast for a while (糸). Two by two they are one bar: an even run from its first, as the whole bars
    /// around it hold their lines; an odd one has a half left over, at its first or its last, where the key says (the
    /// bar lines are where the home chord comes, the more so after its dominant). A whole bar between two halves is
    /// two halves, cut at its beat nearest the middle. As read_chords.py's pair_halves.</summary>
    static List<int> PairHalves(double[] times, List<int> downs, float[] logp, int frames, double step)
    {
        double lo = HalfBars.Lo, hi = HalfBars.Hi;
        var lengths = Enumerable.Range(0, downs.Count - 1).Select(k => times[downs[k + 1]] - times[downs[k]]).ToList();
        double middle = Np.Median(lengths);
        double Span(int k) => times[downs[k + 1]] - times[downs[k]];
        bool Half(double x) => lo * middle <= x && x < hi * middle;
        for (int k = 1; k < downs.Count - 2; k++)
        {
            int a = downs[k], b = downs[k + 1];
            if (Half(Span(k)) || !Half(Span(k - 1)) || !Half(Span(k + 1)) || b - a < 2) continue;
            double mid = (times[a] + times[b]) / 2;
            int cut = a + 1;
            for (int i = a + 2; i < b; i++) if (Math.Abs(times[i] - mid) < Math.Abs(times[cut] - mid)) cut = i;
            if (Half(times[cut] - times[a]) && Half(times[b] - times[cut])) downs.Insert(k + 1, cut);
        }
        var key = KeyHeard(logp, frames, step);
        if (key is not var (tonic, major)) return downs;
        int homeKind = major ? 0 : 1, fifth = (tonic + 7) % 12;
        var gone = new HashSet<int>();
        int r = 0;
        while (r < downs.Count - 1)
        {
            if (!Half(Span(r))) { r++; continue; }
            int end = r;
            while (end + 1 < downs.Count - 1 && Half(Span(end + 1))) end++;
            int n = end - r + 1;
            if (n >= 2)
            {
                bool odd = false;
                if (n % 2 == 1)
                {
                    double beat = (times[downs[end + 1]] - times[downs[r]]) / n / 2;
                    var s = new double[n + 1];
                    for (int j = 0; j <= n; j++)
                    {
                        double t = times[downs[r + j]];
                        var said = SaidIn(logp, frames, step, [t - beat, t, t + beat], EarsOf);
                        double home = 0, dominant = 0;
                        for (int k = 0; k < Chords; k++)
                        {
                            if (k % 12 == tonic && Kind[k / 12] == homeKind) home += Math.Exp(said[1][k]);
                            if (k % 12 == fifth && Kind[k / 12] is 0 or 2) dominant += Math.Exp(said[0][k]);
                        }
                        s[j] = home * (1.0 + dominant);
                    }
                    odd = Np.Mean(s.Where((_, j) => j % 2 == 1).ToArray()) > Np.Mean(s.Where((_, j) => j % 2 == 0).ToArray());
                }
                for (int j = 1; j < n; j++) if (j % 2 == (odd ? 0 : 1)) gone.Add(downs[r + j]);
            }
            r = end + 1;
        }
        return [.. downs.Where(d => !gone.Contains(d))];
    }

    /// <summary>Where the <paramref name="n"/> beats of a bar begin: the bar cut evenly, the first at its line. (The beat
    /// tracker's own beats inside the bar are no nearer RWC's annotated beats.)</summary>
    static List<double> BeatsOf(double start, double end, int n)
    {
        double each = (end - start) / n;
        var beats = new List<double> { start };
        for (int i = 1; i < n; i++) beats.Add(Np.Round(start + i * each, 4));
        return beats;
    }

    /// <summary>Do a bar's onsets fall in fours? The bars are laid over each other in 24 steps: quarters and eighths
    /// of 4/4 fall on steps that a bar in three never uses, and the other way round.</summary>
    static bool InFour(List<double> lines, float[] oenv)
    {
        var profile = new double[24];
        for (int i = 0; i + 1 < lines.Count; i++)
        {
            double a = lines[i], b = lines[i + 1];
            for (int k = 0; k < 24; k++)
            {
                int f = (int)Math.Round(a + (b - a) * k / 24);
                if (f >= oenv.Length) continue;
                float most = float.NegativeInfinity;
                for (int j = Math.Max(0, f - 1); j < Math.Min(f + 2, oenv.Length); j++) most = Math.Max(most, oenv[j]);
                profile[k] += most;
            }
        }
        double four = (profile[3] + profile[6] + profile[9] + profile[15] + profile[18] + profile[21]) / 6;
        double three = (profile[4] + profile[8] + profile[16] + profile[20]) / 4;
        return four >= three;
    }

    /// <summary>The frames a stretch from a to b (in frames, not whole) is heard in: at least one.</summary>
    /// <summary>numpy's percentile(…, 95) of float32 (linear, and in float32).</summary>
    static float Percentile95(float[] values)
    {
        var sorted = (float[])values.Clone();
        Array.Sort(sorted);
        double index = 0.95 * (sorted.Length - 1);
        int lo = (int)Math.Floor(index), hi = Math.Min(lo + 1, sorted.Length - 1);
        float t = (float)(index - lo), a = sorted[lo], b = sorted[hi], d = b - a;
        return t >= 0.5f ? b - d * (1f - t) : a + d * t;
    }

    static (int From, int To) Over(double a, double b, int frames)
    {
        int i = Math.Max((int)Math.Round(a), 0);
        int j = Math.Max(i + 1, (int)Math.Round(b));
        return (Math.Min(i, frames), Math.Min(j, frames));
    }

    static void Unit(float[] v)
    {
        Span<float> squares = stackalloc float[v.Length];
        for (int i = 0; i < v.Length; i++) squares[i] = v[i] * v[i];
        float norm = MathF.Sqrt(Np.Sum(squares));
        if (!(norm > 0)) return;
        for (int i = 0; i < v.Length; i++) v[i] /= norm;
    }

    /// <summary>What the chord model says of each unit: the log of its mean probability of each of our chords
    /// between one edge (in seconds) and the next. A frame counts for as much of it as lies inside the unit.</summary>
    static double[][] SaidIn(float[] logp, int frames, double step, double[] edges, int[][] ears)
    {
        int width = logp.Length / frames, chords = ears.Length;
        var p = new double[frames][];
        var run = new double[frames + 1][];
        run[0] = new double[chords];
        var e = new double[width];
        for (int t = 0; t < frames; t++)
        {
            for (int i = 0; i < width; i++) e[i] = Math.Exp(logp[t * width + i]);
            var row = p[t] = new double[chords];
            var sums = run[t + 1] = new double[chords];
            for (int k = 0; k < chords; k++)
            {
                double sum = 0;
                foreach (int i in ears[k]) sum += e[i];
                row[k] = sum;
                sums[k] = run[t][k] + sum;
            }
        }
        var x = new double[edges.Length];
        var upto = new double[edges.Length][];
        for (int u = 0; u < edges.Length; u++)
        {
            x[u] = Math.Clamp(edges[u] / step, 0.0, frames);
            int i = Math.Min((int)x[u], frames - 1);
            var row = upto[u] = new double[chords];
            for (int k = 0; k < chords; k++) row[k] = run[i][k] + (x[u] - i) * p[i][k];
        }
        var said = new double[edges.Length - 1][];
        for (int u = 0; u + 1 < edges.Length; u++)
        {
            double wide = Math.Max(x[u + 1] - x[u], 1e-6);
            var row = said[u] = new double[chords];
            for (int k = 0; k < chords; k++) row[k] = Math.Log(Math.Max((upto[u + 1][k] - upto[u][k]) / wide, 1e-12));
        }
        return said;
    }

    /// <summary>The best path of (chord, place in a bar of <paramref name="length"/> units): a chord may change
    /// freely at a bar line, where staying costs as much as the two chords are alike; at a price in the middle of
    /// a bar; at a higher one elsewhere. The first unit is at the place <paramref name="first"/>.</summary>
    static (int[] Place, int[] Chord) Decode(double[][] emit, int length, int first)
    {
        int count = emit.Length;
        int half = length % 2 == 0 ? length / 2 : -1;
        var worth = new double[length][];
        var next = new double[length][];
        for (int place = 0; place < length; place++)
        {
            worth[place] = place == first ? (double[])emit[0].Clone() : [.. Enumerable.Repeat(double.NegativeInfinity, Chords)];
            next[place] = new double[Chords];
        }
        var back = new byte[(long)count * length * Chords];
        for (int t = 1; t < count; t++)
        {
            var heard = emit[t];
            for (int place = 0; place < length; place++)
            {
                var before = worth[(place - 1 + length) % length];
                var now = next[place];
                int at = (t * length + place) * Chords;
                if (place == 0)
                {
                    for (int k = 0; k < Chords; k++)
                    {
                        double best = double.NegativeInfinity;
                        int source = 0;
                        for (int j = 0; j < Chords; j++)
                        {
                            double reach = before[j] - StayAlike[j][k];
                            if (reach > best) { best = reach; source = j; }
                        }
                        now[k] = best + heard[k];
                        back[at + k] = (byte)source;
                    }
                    continue;
                }
                int top = 0, second = -1;                       // the two best chords so far
                for (int k = 1; k < Chords; k++) if (before[k] >= before[top]) top = k;
                for (int k = 0; k < Chords; k++) if (k != top && (second < 0 || before[k] >= before[second])) second = k;
                double cost = place == half ? MidCost : ElseCost;
                for (int k = 0; k < Chords; k++)
                {
                    int other = k == top ? second : top;        // the best chord that is not this one
                    double moved = before[other] - cost;
                    bool take = moved > before[k];
                    now[k] = (take ? moved : before[k]) + heard[k];
                    back[at + k] = (byte)(take ? other : k);
                }
            }
            (worth, next) = (next, worth);
        }
        int endPlace = 0, endChord = 0;
        for (int place = 0; place < length; place++)
            for (int k = 0; k < Chords; k++)
                if (worth[place][k] > worth[endPlace][endChord]) (endPlace, endChord) = (place, k);
        var places = new int[count];
        var chords = new int[count];
        for (int t = count - 1; t >= 0; t--)
        {
            (places[t], chords[t]) = (endPlace, endChord);
            endChord = back[(t * length + endPlace) * Chords + endChord];
            endPlace = (endPlace - 1 + length) % length;
        }
        return (places, chords);
    }

    static readonly (int Degree, int Kind)[] MajorChords = [(0, 0), (2, 1), (4, 1), (5, 0), (7, 0), (7, 2), (9, 1), (11, 3)];
    static readonly (int Degree, int Kind)[] MinorChords = [(0, 1), (2, 3), (3, 0), (5, 1), (7, 1), (7, 0), (7, 2), (8, 0), (10, 0)];
    const double HomeWeight = 0.5, EndWeight = 0.1;

    /// <summary>The key whose chords the reading is made of. The notes heard cannot tell a key from its relative
    /// or its dominant; which chords are there, which of them is stayed on longest, and which one ends the piece, can.</summary>
    static (int Tonic, bool Major) FindKey(List<(double Seconds, int Root, int Quality)> chords)
    {
        double whole = 0;
        foreach (var c in chords) whole += c.Seconds;
        (double Score, int Tonic, bool Major) best = (double.NegativeInfinity, 0, true);
        foreach (bool major in new[] { true, false })
        {
            var members = major ? MajorChords : MinorChords;
            var home = (0, major ? 0 : 1);
            for (int tonic = 0; tonic < 12; tonic++)
            {
                double score = 0;
                foreach (var (seconds, root, quality) in chords)
                {
                    var degree = (((root - tonic) % 12 + 12) % 12, Kind[quality]);
                    if (Array.IndexOf(members, degree) >= 0) score += seconds;
                    if (degree == home) score += HomeWeight * seconds;
                }
                var (_, lastRoot, lastQuality) = chords[^1];
                if ((((lastRoot - tonic) % 12 + 12) % 12, Kind[lastQuality]) == home) score += EndWeight * whole;
                if (score > best.Score) best = (score, tonic, major);
            }
        }
        return (best.Tonic, best.Major);
    }

    /// <param name="audio">The recording's file name.</param>
    /// <param name="lineSeconds">The bar lines a beat tracker found, in seconds, the end of the last bar too.</param>
    /// <param name="beats">The beats it counted in a bar.</param>
    /// <param name="logp">The chord model's log probabilities, [frame * its chords + chord], a frame every <paramref name="step"/> seconds.</param>
    public static Chart Read(string audio, Features.Heard f, List<double> lineSeconds, int beats, float[] logp, int frames, double step, float[]? voice = null)
    {
        float[] oenv = f.Onsets, rms = f.Loudness, chroma = f.Chroma, bass = f.Bass;
        int n = oenv.Length, chromaFrames = chroma.Length / 12, bassFrames = bass.Length / 12;
        var marks = lineSeconds.Select(s => s * SR / HOP + BeatLag).Where(m => m < n).ToList();
        if (marks.Count < 2) throw new InvalidDataException("fewer than two bar lines in the recording");

        // two beats are two quarters or, when the sound between them falls in threes, the two dotted quarters of 6/8
        bool six = beats == 6 || (beats == 2 && !InFour(marks, oenv));
        if (beats == 2 && !six && Np.Median(Np.Diff(marks)) * HOP / SR > SlowTwo) beats = 4;    // the tracker counted the halves of a bar of four
        int units = six || beats == 3 ? 6 : 2 * beats;

        // The music may begin before the first line found and go on after the last: the bars are carried on, as long
        // as the first and the last, to both ends of the recording. What of them is in the chart is settled below.
        var gaps = Np.Diff(marks);
        if (gaps.Count > 4 && gaps[0] < 0.8 * Np.Median(gaps.GetRange(1, 4)))
        {
            marks.RemoveAt(0);          // a first line less than a bar before the second begins a pickup, not a bar
            gaps.RemoveAt(0);
        }
        double before = Np.Median(gaps.Take(4)) / units;
        double after = Np.Median(gaps.Count > 1 ? gaps.Skip(Math.Max(gaps.Count - 5, 0)).Take(Math.Min(4, gaps.Count - 1)) : gaps) / units;
        bool stopped = gaps[^1] < 0.75 * after * units;     // the last line is where the beats stopped, not a bar's end
        if (stopped)
        {
            double whole = marks[^2] + after * units;
            marks.RemoveAt(marks.Count - 1);
            if (whole < n) marks.Add(whole);
        }
        int led = (int)Np.FloorDiv(Math.Max(marks[0] - BeatLag, 0.0), before);
        var edgeList = new List<double>();
        for (int k = led; k > 0; k--) edgeList.Add(marks[0] - before * k);
        for (int i = 0; i + 1 < marks.Count; i++)
        {
            double part = (marks[i + 1] - marks[i]) / units;
            for (int k = 0; k < units; k++) edgeList.Add(part == 0 ? marks[i] : k * part + marks[i]);
        }
        edgeList.Add(marks[^1]);
        int more = (int)Np.FloorDiv(n - 1 - marks[^1], after);
        for (int k = 1; k <= more; k++) edgeList.Add(marks[^1] + after * k);
        var edges = edgeList.ToArray();
        int count = edges.Length - 1;
        if (count < 1) throw new InvalidDataException("no bar is heard");
        int carried = led + (gaps.Count - (stopped ? 1 : 0)) * units;       // from this unit on, the bars are past the last one found

        var loud = new double[count];
        var middle = new double[count];     // a unit is as loud as its middle frame: its edges are late by the lag of the onsets
        var seconds = new double[count];
        var treble = new float[count][];
        for (int u = 0; u < count; u++)
        {
            var (a, b) = Over(edges[u], edges[u + 1], rms.Length);
            loud[u] = Np.Mean(rms.AsSpan(a, b - a));
            middle[u] = Np.Median(rms.AsSpan(a, b - a));
            seconds[u] = (edges[u + 1] - edges[u]) * HOP / SR;
            (a, b) = Over(edges[u], edges[u + 1], chromaFrames);
            var v = treble[u] = new float[12];
            for (int c = 0; c < 12; c++) v[c] = Np.Mean(chroma.AsSpan(c * chromaFrames + a, b - a));
            Unit(v);
        }
        // Where a chord may be: not in what is a hundred times quieter than the song, nor where the model says "no chord".
        double quiet = Hush * Np.Median(loud);
        var edgeSeconds = edges.Select(e => (e - BeatLag) * HOP / SR).ToArray();
        var said = SaidIn(logp, frames, step, edgeSeconds, EarsOf);
        var none = SaidIn(logp, frames, step, edgeSeconds, EarsNone);
        var sounds = new bool[count];
        var emit = new double[count][];
        for (int u = 0; u < count; u++)
        {
            sounds[u] = middle[u] >= quiet && Math.Exp(none[u][0]) <= NoChord;
            var row = emit[u] = new double[Chords];
            for (int k = 0; k < Chords; k++)
            {
                said[u][k] = Math.Max(said[u][k], LogFloor);
                row[k] = EmitScale * said[u][k] * seconds[u];
            }
        }

        int length = units;
        var (place, path) = Decode(emit, length, ((-led % length) + length) % length);
        var starts = Enumerable.Range(0, count).Where(t => place[t] == 0).ToList();
        if (starts.Count == 0) throw new InvalidDataException("no bar is heard");
        if (count - starts[^1] >= length) starts.Add(starts[^1] + length);      // the last bar is whole
        if (starts.Count < 2) throw new InvalidDataException("no whole bar is heard");
        string time = six ? "6/8" : $"{beats}/4";
        int tempoBeats = six ? 2 : beats;

        // The bars of the chart: from the first that sounds a chord to the last. Silence before the music, a
        // count-in, the ring-out and the bars carried on past them are not in it.
        var spans = new List<(int First, int After)>();
        for (int i = 0; i + 1 < starts.Count; i++) spans.Add((starts[i], starts[i + 1]));
        if (0 < count - starts[^1] && count - starts[^1] < length) spans.Add((starts[^1], count));     // the recording ends inside a bar
        bool HasChord((int First, int After) span)
        {
            int sounding = 0;
            for (int t = span.First; t < span.After; t++) if (sounds[t]) sounding++;
            return sounding >= Chorded * length;
        }
        while (spans.Count > 0 && !HasChord(spans[0])) spans.RemoveAt(0);
        while (spans.Count > 0 && !HasChord(spans[^1])) spans.RemoveAt(spans.Count - 1);
        if (spans.Count == 0) throw new InvalidDataException("no chord is heard");
        // Past the last bar found, what dies away is the last chord ringing out, not one more bar.
        double Loud((int First, int After) span) => Np.Mean(loud.AsSpan(span.First, span.After - span.First));
        while (spans.Count > 1 && spans[^1].First >= carried && Loud(spans[^1]) < RingOut * Loud(spans[^2]))
            spans.RemoveAt(spans.Count - 1);
        // A pickup: whole beats of chord that lead into the first bar, in a short bar of their own.
        // A first bar that begins with nothing is one, not a bar.
        double per = (double)length / tempoBeats;       // units in a beat
        int still = 0;
        for (int i = 0; i < spans[0].After - spans[0].First; i++) if (sounds[spans[0].First + i]) { still = i; break; }
        if (spans.Count > 1 && still != 0) spans.RemoveAt(0);
        int at = spans[0].First, one = at;
        while (at > 0 && sounds[at - 1] && one - at + 1 < length) at--;
        if (one - at == length - 1)     // all of a bar but its first unit (unheard, or before the grid begins): the first bar
        {
            spans.Insert(0, (Math.Max(one - length, 0), one));
            at = one = spans[0].First;
        }
        int upbeats = Math.Min((int)((one - at) / per + 1e-6), tempoBeats - 1);
        while (upbeats != 0 && time != "6/8" && !Partial.ContainsKey(upbeats)) upbeats--;
        int ledIn = Math.Max(one - (int)Math.Ceiling(upbeats * per - 1e-6), at);

        double Stamp(double frame) => Np.Round(Math.Max(frame - BeatLag, 0.0) * HOP / SR, 4);
        var weighed = new List<(double Mean, double Last, double Agree)>();

        List<Chord> Spell(int first, int end)       // the chords of the units from `first` to `end`
        {
            var chords = new List<Chord>();
            int t = first;
            while (t < end)
            {
                int stop = t, k = path[t], root = k % 12, quality = k / 12;
                while (stop < end && path[stop] == k) stop++;
                int held = stop - t;
                double a = edges[t], b = edges[stop];
                var (lo, hi) = Over(a, a + (b - a) / 4, bassFrames);
                var vb = new float[12];
                for (int c = 0; c < 12; c++) vb[c] = Np.Median(bass.AsSpan(c * bassFrames + lo, hi - lo));
                Unit(vb);
                int lowest = 0;
                for (int c = 1; c < 12; c++) if (vb[c] > vb[lowest]) lowest = c;
                bool tone = Intervals[quality].Any(i => (root + i) % 12 == lowest);
                bool slash = lowest != root && tone && vb[lowest] > (float)SlashRatio * vb[root];

                var fits = new double[Chords][];        // this reader's own hearing of each chord in each unit
                for (int j = 0; j < Chords; j++)
                {
                    var row = fits[j] = new double[held];
                    for (int u = 0; u < held; u++)
                    {
                        double dot = 0;
                        for (int i = 0; i < 12; i++) dot += Templates[j][i] * treble[t + u][i];
                        row[u] = dot;
                    }
                }
                // how sure the model is of it, half beat by half beat, and what else it might be (of its root
                // and whether it is major or minor: a seventh heard or not is a smaller doubt)
                var mine = new double[held];
                for (int u = 0; u < held; u++)
                    foreach (int j in Same[k]) mine[u] += Math.Exp(said[t + u][j]);
                int plain = 0, other = -1;
                double plainBest = double.NegativeInfinity, otherBest = double.NegativeInfinity;
                for (int j = 0; j < Chords; j++)
                {
                    double own = Np.Mean(fits[j]) - Costs[j];
                    if (own > plainBest) (plainBest, plain) = (own, j);
                    if (Array.IndexOf(Same[k], j) >= 0) continue;
                    double sum = 0;
                    for (int u = 0; u < held; u++) sum += said[t + u][j];
                    if (other < 0 || sum / held > otherBest) (otherBest, other) = (sum / held, j);
                }
                double sure = Np.Mean(mine);
                weighed.Add((sure, mine[^1], Array.IndexOf(Same[k], plain) >= 0 ? 1.0 : 0.0));
                chords.Add(new Chord
                {
                    Start = Stamp(a), End = Stamp(b), Symbol = new Symbol(root, quality, slash ? lowest : -1),
                    Fit = Np.Round(Np.Mean(fits[k]), 3), Sure = Np.Round(sure, 3), Other = (other % 12, other / 12),
                });
                t = stop;
            }
            return chords;
        }

        var bars = new List<Bar>();
        if (upbeats != 0 && ledIn < one)
            bars.Add(new Bar { Number = 0, Pickup = upbeats, Start = Stamp(edges[ledIn]), End = Stamp(edges[one]), Chords = Spell(ledIn, one) });
        for (int i = 0; i < spans.Count; i++)
        {
            var (first, end) = spans[i];
            var chords = Spell(first, end);
            double ends = edges[end];
            if (end - first < length && first == 0 && spans.Count > 1)     // the first bar, from where it began
            {
                double begun = Math.Max(edges[0] - (edges[1] - edges[0]), BeatLag);
                bars.Add(new Bar { Number = i + 1, Start = Stamp(begun), End = Stamp(ends), Chords = chords });
                chords[0].Start = Stamp(begun);
                continue;
            }
            if (end - first < length)       // the bar the recording ends in is written whole
            {
                ends = edges[first] + (first >= length ? edges[first] - edges[first - length] : length * (ends - edges[first]) / (end - first));
                chords[^1].End = Stamp(ends);
            }
            bars.Add(new Bar { Number = i + 1, Start = Stamp(edges[first]), End = Stamp(ends), Chords = chords });
        }

        // A model trained on recordings is sure of itself more often than it is right. How likely the root and the
        // family are right is weighed from four things: its mean probability of the chord over its half beats, the
        // same in its last half beat (which weighs the other way: what it says there is already the next chord's),
        // whether our own templates name the same root and family, and the first over the whole song.
        var read = bars.SelectMany(bar => bar.Chords).ToList();
        double song = Np.Mean(weighed.Select(w => w.Mean).ToArray());
        for (int i = 0; i < read.Count; i++)
        {
            double z = SureOf[0] * weighed[i].Mean + SureOf[1] * weighed[i].Last + SureOf[2] * weighed[i].Agree + SureOf[3] * song + SureOf[4];
            read[i].Sure = Np.Round(1.0 / (1.0 + Math.Exp(-z)), 3);
        }
        foreach (var bar in bars) bar.Beats = BeatsOf(bar.Start, bar.End, bar.Pickup != 0 ? bar.Pickup : tempoBeats);
        // The sections, by meaning: what each bar sounds (the notes of the chords the ears hear in it, and how loud
        // it is) tells where the music turns; then the phrases (Form.Phrases).
        var full = bars.Where(bar => bar.Pickup == 0).ToList();
        var unitNotes = new double[count][];
        var begins = new double[count];
        for (int u = 0; u < count; u++)
        {
            var row = unitNotes[u] = new double[12];
            for (int k = 0; k < Chords; k++)
            {
                double p = Math.Exp(said[u][k]);
                foreach (int i in Intervals[k / 12]) row[(k % 12 + i) % 12] += p;
            }
            for (int c = 0; c < 12; c++) row[c] *= seconds[u];
            begins[u] = Stamp(edges[u]);
        }
        var barNotes = new double[full.Count][];
        var barDb = new double[full.Count];
        for (int i = 0; i < full.Count; i++)
        {
            var bar = full[i];
            var sum = barNotes[i] = new double[12];
            for (int u = 0; u < count; u++)
                if (begins[u] >= bar.Start && begins[u] < bar.End)
                    for (int c = 0; c < 12; c++) sum[c] += unitNotes[u][c];
            var (a, b) = Over(bar.Start * SR / HOP + BeatLag, bar.End * SR / HOP + BeatLag, rms.Length);
            barDb[i] = 20.0 * Math.Log10((double)Np.Mean(rms.AsSpan(a, b - a)) + 1e-9);
        }
        var turns = Form.Turns(barNotes, barDb);
        // where singing begins and stops: the share of each bar whose vocals are a quarter of the song's loud end
        var sings = new List<int>();
        if (voice is { Length: > 0 } && (double)Percentile95(voice) >= Form.VoiceFloor * (double)Percentile95(rms))
        {
            float line = (float)(Form.VoiceLoud * (double)Percentile95(voice));
            var sung = new double[full.Count];
            for (int i = 0; i < full.Count; i++)
            {
                int a = (int)(full[i].Start * 100), b = Math.Max((int)(full[i].End * 100), a + 1);
                b = Math.Min(b, voice.Length);
                int heardOf = 0, on = 0;
                for (int k = a; k < b; k++) { heardOf++; if (voice[k] >= line) on++; }
                sung[i] = heardOf == 0 ? double.NaN : (double)on / heardOf;
            }
            sings = Form.VoiceTurns(sung);
        }
        var (tonic, major) = FindKey([.. read.Select(c => (c.End - c.Start, c.Symbol.Root, c.Symbol.Quality))]);
        var keys = full.Select(bar => Enumerable.Range(0, tempoBeats).Select(i =>
        {
            double at = bar.Start + (i + 0.01) * (bar.End - bar.Start) / tempoBeats;
            var c = bar.Chords.FirstOrDefault(c => c.Start <= at && at < c.End) ?? bar.Chords[^1];
            return c.Symbol.Quality * 12 + c.Symbol.Root;
        }).ToArray()).ToArray();
        var (form, places) = Form.Phrases(keys, turns, tonic, major, Form.PhraseBars, sings);
        for (int i = 0; i < full.Count; i++) (full[i].Section, full[i].BarOfSection) = places[i];
        if (bars[0].Pickup != 0)        // a section of its own: Lily# lets only the first one open short
        {
            (bars[0].Section, bars[0].BarOfSection) = ("Pickup", 1);
            form.Insert(0, "Pickup");
        }
        string tonicName = (major ? MajorKeys : MinorKeys)[tonic];
        bool flats = tonicName.Contains('b') || (major ? tonicName == "F" : FlatMinors.Contains(tonicName));
        var roots = flats ? FlatRoots : SharpRoots;
        foreach (var c in read)
        {
            c.Name = roots[c.Symbol.Root] + Qualities[c.Symbol.Quality] + (c.Symbol.Bass >= 0 ? "/" + roots[c.Symbol.Bass] : "");
            if (c.Other is { } other) c.OtherName = roots[other.Root] + Qualities[other.Quality];
        }
        var barGaps = Np.Diff([.. starts.Select(t => edges[t])]);
        double barSeconds = Np.Median(barGaps) * HOP / SR;
        // the lines are a beat tracker's, on its grid of 20 ms: the bars about as long as the middle one, together
        barSeconds = Np.Mean(barGaps.Select(g => g * HOP / SR).Where(g => Math.Abs(g - barSeconds) <= EvenTempo * barSeconds).ToArray());
        return new Chart
        {
            Audio = audio, Duration = Np.Round((n - 1) * HOP / SR, 2),
            TempoNote = six ? "4." : "4", Bpm = Np.Round(60.0 * tempoBeats / barSeconds, 1),
            Time = time, Key = $"{tonicName} {(major ? "major" : "minor")}", Tuning = (int)f.Cents,
            Doubt = read.Any(c => c.Sure != null) ? Doubt : null, Form = form, Bars = bars,
        };
    }
}
