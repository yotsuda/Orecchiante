namespace Dettato;

/// <summary>The sections of a chart, by meaning: where the music turns, and the phrases between. tools/read_chords.py's
/// turns_of() and phrase_sections(), step for step (its --old-sections, find_form and fold, stays in Python).</summary>
static class Form
{
    const int KeyTurn = 2;          // a key region this many accidentals from the one before turns the music
    const double KeyPrice = 1.5;    // what a change of key signature costs, against a bar's evidence
    const int KeyRegion = 4;        // bars: a shorter run in one key is not a region of its own
    const double LoudTurn = 6.0;    // dB: the two bars after a bar line this much louder or quieter than the two before
    public const int PhraseBars = 4;    // a phrase is this many bars, whatever the tempo
    const double BeginShare = 0.67; // on the grid, a cut where the phrase is played elsewhere after another bar at this share of the places
    const int PairHeard = 2;        // on the grid, a cut after every pair of phrases where the phrase after is played this many times
                                    // elsewhere and does not begin the pair as the one before did (a b a c is one period)
    const bool GridAnchor = true;   // a block's grid counts from where singing last began or stopped, or a block played again whole begins or ends, not from a turn
    const int WholeBlock = 2;      // phrases: a block no longer than this is one section
    const string Letters = "ABCDEFGHIJKLMNOPQSTUVWXYZ";   // section names; no R: R2 reads in Lily# as a whole-bar rest of a half
    const double PhraseAlike = 0.75;    // two phrases are the same when this share of their beats have the same root and family
    public const double VoiceLoud = 0.25;   // a moment sings when the vocals are this share of the song's loud end (their 95th percentile)
    public const double VoiceFloor = 0.05;  // a song sings at all when the vocals' loud end is this share of the recording's
    const double VoiceShare = 0.45; // a bar sings when this share of it sings
    const int VoiceRun = 2;         // bars: singing, or its silence, has to last this long to turn the music
    const int VoiceGap = 3;         // bars: a silence shorter than this between singing is a breath
    const int TurnNear = 2;         // bars: a turn of key or loudness this near where singing begins or stops is that one

    /// <summary>Where singing begins or stops and goes on (<paramref name="sung"/>: the share of each bar that sings):
    /// the bars a section begins at. A silence shorter than VoiceGap bars between singing is a breath; then singing
    /// shorter than VoiceRun bars is none. read_chords.py's voice_turns().</summary>
    public static List<int> VoiceTurns(double[] sung)
    {
        var on = sung.Select(s => s >= VoiceShare).ToArray();
        List<(int I, int J, bool V)> Runs()
        {
            var runs = new List<(int, int, bool)>();
            for (int i = 0; i < on.Length;)
            {
                int j = i;
                while (j < on.Length && on[j] == on[i]) j++;
                runs.Add((i, j, on[i]));
                i = j;
            }
            return runs;
        }
        var found = Runs();
        for (int k = 0; k < found.Count; k++)
        {
            var (i, j, v) = found[k];
            if (!v && j - i < VoiceGap && 0 < k && k < found.Count - 1) for (int x = i; x < j; x++) on[x] = true;
        }
        foreach (var (i, j, v) in Runs())
            if (v && j - i < VoiceRun) for (int x = i; x < j; x++) on[x] = false;
        return [.. Runs().Skip(1).Select(r => r.I)];
    }

    /// <summary>Where the music turns: the bars a section begins at. <paramref name="notes"/>: each bar's notes heard
    /// (12 each: the chords' tones, weighed by how likely and how long); <paramref name="loud"/>: each bar in dB.</summary>
    public static HashSet<int> Turns(double[][] notes, double[] loud)
    {
        int n = loud.Length;
        var turns = new HashSet<int>();
        if (n < 2 * KeyRegion) return turns;
        var fits = new double[n][];
        for (int i = 0; i < n; i++)
        {
            double total = Math.Max(Np.Sum(notes[i]), 1e-9);
            var row = fits[i] = new double[12];
            for (int t = 0; t < 12; t++)
            {
                double inScale = 0;
                for (int pc = 0; pc < 12; pc++) if (InScale(t, pc)) inScale += notes[i][pc];
                row[t] = 4.0 * Math.Log(Math.Max(inScale / total, 1e-3));
            }
        }
        var score = (double[])fits[0].Clone();
        var back = new List<int[]>();
        for (int i = 1; i < n; i++)
        {
            int top = ArgMax(score);
            double best = score[top];
            var came = new int[12];
            for (int t = 0; t < 12; t++)
            {
                bool stay = score[t] >= best - KeyPrice;
                came[t] = stay ? t : top;
                score[t] = (stay ? score[t] : best - KeyPrice) + fits[i][t];
            }
            back.Add(came);
        }
        var path = new List<int> { ArgMax(score) };
        for (int i = back.Count - 1; i >= 0; i--) path.Add(back[i][path[^1]]);
        path.Reverse();
        var regions = new List<(int First, int Signature)>();
        int start = 0;
        for (int i = 1; i <= n; i++)
        {
            if (i == n || path[i] != path[start])
            {
                if (i - start >= KeyRegion) regions.Add((start, ((path[start] * 7 + 6) % 12) - 6));
                start = i;
            }
        }
        for (int r = 1; r < regions.Count; r++)
        {
            int d = Math.Abs(regions[r - 1].Signature - regions[r].Signature);
            if (Math.Min(d, 12 - d) >= KeyTurn) turns.Add(regions[r].First);
        }
        var jump = new double[n];
        for (int i = 2; i < n - 1; i++) jump[i] = (loud[i] + loud[i + 1]) / 2 - (loud[i - 2] + loud[i - 1]) / 2;
        for (int i = 2; i < n - 1; i++)
        {
            double most = 0;
            for (int j = Math.Max(i - 2, 0); j < Math.Min(i + 3, n); j++) most = Math.Max(most, Math.Abs(jump[j]));
            if (Math.Abs(jump[i]) >= LoudTurn && Math.Abs(jump[i]) >= most) turns.Add(i);
        }
        return turns;
    }

    static bool InScale(int tonic, int pc) => ((pc - tonic) % 12 + 12) % 12 is 0 or 2 or 4 or 5 or 7 or 9 or 11;

    static int ArgMax(double[] a)
    {
        int k = 0;
        for (int i = 1; i < a.Length; i++) if (a[i] > a[k]) k = i;
        return k;
    }

    /// <summary>The sections, as the owner cut 糸 and My Life: blocks between turns, blocks played again whole elsewhere,
    /// and inside a long block a grid of phrases cut where a phrase said twice has ended, where the dominant goes
    /// home across the bar line, after a pair of phrases where a new pair begins, or where the phrase is one played
    /// elsewhere after another bar. A section that plays
    /// one before it again is named after it (A, A2). <paramref name="keys"/>: the chord (quality * 12 + root) on each
    /// beat of each bar. Returns the form and, for each bar, its section and which bar of it it is.</summary>
    public static (List<string> Form, List<(string, int)> Places) Phrases(int[][] keys, HashSet<int> turns, int tonic, bool major, int phrase, IReadOnlyList<int> sings)
    {
        int n = keys.Length;
        var fam = keys.Select(row => row.Select(k => k % 12 * 4 + Reader.Kind[k / 12]).ToArray()).ToArray();
        int home = tonic * 4 + (major ? 0 : 1);

        int Same(int a, int b, int length, out int all)
        {
            int same = 0;
            all = 0;
            for (int i = 0; i < length; i++)
                for (int j = 0; j < Math.Min(fam[a + i].Length, fam[b + i].Length); j++) { all++; if (fam[a + i][j] == fam[b + i][j]) same++; }
            return same;
        }
        bool Alike(int a, int b, int length)
        {
            int same = Same(a, b, length, out int all);
            return all > 0 && same >= PhraseAlike * all;
        }
        bool Cadence(int g)
        {
            int last = fam[g - 1][^1], first = fam[g][0];
            return last / 4 == (tonic + 7) % 12 && (last % 4 == 0 || last % 4 == 2) && first == home;
        }
        bool BeginsAgain(int g)
        {
            if (g <= 0 || g + phrase > n) return false;
            for (int h = 0; h < n - phrase + 1; h++)
                if (Math.Abs(h - g) >= phrase && Alike(g, h, phrase) && !(h > 0 && Alike(g - 1, h - 1, 1))) return true;
            return false;
        }
        bool AlwaysBegins(int g)
        {
            if (g <= 0 || g + phrase > n) return false;
            var heard = Enumerable.Range(1, Math.Max(n - phrase, 0)).Where(h => Math.Abs(h - g) >= phrase && Alike(g, h, phrase)).ToList();
            return heard.Count > 0 && heard.Count(h => !Alike(g - 1, h - 1, 1)) >= BeginShare * heard.Count;
        }

        // a turn a bar after where a phrase played elsewhere begins is that phrase's
        var moved = turns.Where(t => 0 < t && t < n).ToDictionary(t => t, t => BeginsAgain(t - 1) && !BeginsAgain(t) ? t - 1 : t);
        // where singing begins or stops is where the section does, and another turn this near it is the same one, a bar
        // off; but not a turn where a phrase played elsewhere begins
        var kept = new HashSet<int>();
        var others = new SortedSet<int>(moved.Keys);
        foreach (int s in sings.Order())
        {
            var near = others.Where(t => Math.Abs(t - s) <= TurnNear).ToList();
            var strong = near.Select(t => moved[t]).Where(BeginsAgain).ToList();
            kept.Add(strong.Count > 0 ? strong[0] : s);
            foreach (int t in near) others.Remove(t);
            kept.UnionWith(strong);
        }
        var edges = new SortedSet<int>(kept.Concat(others.Select(t => moved[t])).Where(t => 0 < t && t < n)) { 0, n }.ToList();
        var cuts = new HashSet<int>(edges);
        var blocks = edges.Zip(edges.Skip(1)).Where(p => p.Second - p.First >= phrase).ToList();

        List<int> HeardAt(int a, int b)
        {
            var found = new List<int>();
            int h = 0;
            while (h + (b - a) <= n)
            {
                if ((h + (b - a) <= a || h >= b) && Alike(h, a, b - a)) { found.Add(h); h += b - a; }
                else h++;
            }
            return found;
        }
        // a block the turns close in, played again whole elsewhere, is a section there too; played again right after
        // itself, one section; not a block that is a piece of a longer one played elsewhere, nor where it falls inside
        // another block but at that block's start
        var played = blocks.ToDictionary(blk => blk, blk => HeardAt(blk.First, blk.Second));
        var playedCuts = new HashSet<int>();   // the ends of blocks played again whole: the grid may count from them
        foreach (var (a, b) in blocks)
        {
            int length = b - a;
            if (blocks.Any(cd => cd.Second - cd.First > length && played[cd].Any(h => h <= a && a + length <= h + cd.Second - cd.First))) continue;
            var runs = new List<int[]>();
            foreach (int h in played[(a, b)])
            {
                if (runs.Count > 0 && runs[^1][1] == h) runs[^1][1] = h + length;
                else runs.Add([h, h + length]);
            }
            foreach (var run in runs)
            {
                int h = run[0], e = run[1];
                if (h == b) { cuts.Add(e); playedCuts.Add(a); playedCuts.Add(e); }
                else if (edges.Zip(edges.Skip(1)).First(p => p.First <= h && h < p.Second).First == h)
                {
                    cuts.Add(h); cuts.Add(e);
                    playedCuts.UnionWith([a, b, h, e]);
                }
            }
        }
        edges = [.. cuts.Where(c => 0 <= c && c <= n).OrderBy(c => c)];
        // a turn of key or loudness cuts, but the grid goes on from where singing last began or stopped, or a block
        // played again whole begins or ends
        var onlyTurns = new HashSet<int>(others.Select(t => moved[t]));
        onlyTurns.ExceptWith(kept);
        onlyTurns.ExceptWith(playedCuts);
        for (int i = 0; i + 1 < edges.Count; i++)
        {
            int a = edges[i], b = edges[i + 1];
            if (b - a <= WholeBlock * phrase) continue;
            int p = GridAnchor ? (((edges.Where(k => k <= a && !onlyTurns.Contains(k)).Max() - a) % phrase) + phrase) % phrase : 0;
            for (int g = a + (p != 0 ? p : phrase); g < b - 1; g += phrase)
            {
                bool middle = g - phrase >= a && g + phrase <= b && Alike(g - phrase, g, phrase);
                bool ended = g - 2 * phrase >= a && Alike(g - 2 * phrase, g - phrase, phrase) && !(g + phrase <= b && Alike(g - phrase, g, phrase));
                bool pair = PairHeard != 0 && g - a - p >= 2 * phrase && (g - a - p) % (2 * phrase) == 0 && g + phrase <= b && !Alike(g - 2 * phrase, g, phrase)
                    && Enumerable.Range(0, n - phrase + 1).Count(h => Math.Abs(h - g) >= phrase && Alike(g, h, phrase)) >= PairHeard;
                if (!middle && (ended || Cadence(g) || pair || (BeginShare != 0 && g + phrase <= b && AlwaysBegins(g)))) cuts.Add(g);
            }
        }
        var sorted = cuts.OrderBy(c => c).ToList();
        var bases = new List<(int, int)>();
        var names = new List<string>();
        var count = new Dictionary<string, int>();
        var places = new List<(string, int)>();
        for (int p = 0; p + 1 < sorted.Count; p++)
        {
            int a = sorted[p], b = sorted[p + 1], k = -1, bestSame = -1;
            for (int j = 0; j < bases.Count; j++)
            {
                var (c, d) = bases[j];
                int length = Math.Min(b - a, d - c);
                if (length >= Math.Min(phrase, b - a) && Alike(a, c, length))
                {
                    int same = Same(a, c, length, out _);
                    if (k < 0 || same > bestSame) (k, bestSame) = (j, same);
                }
            }
            if (k < 0) { bases.Add((a, b)); k = bases.Count - 1; }
            string letter = k < Letters.Length ? Letters[k].ToString() : $"S{k + 1}";
            count[letter] = count.GetValueOrDefault(letter) + 1;
            string name = count[letter] == 1 ? letter : $"{letter}{count[letter]}";
            names.Add(name);
            for (int i = 0; i < b - a; i++) places.Add((name, i + 1));
        }
        return (names, places);
    }
}
