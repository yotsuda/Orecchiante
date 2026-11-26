using System.Text.RegularExpressions;

namespace Orecchiante;

/// <summary>Lyrics in English cut in syllables by their spelling alone (scratch/align/syl.py, which says the rules;
/// 97.9% of the words RWC-P's English songs sing have CMUdict's number of syllables): each syllable a Mora whose Kana
/// is the syllable as written, Joined when it goes on the word before it. Put to the voice by Words.Align with an
/// English letters model (Model), one syllable a note.</summary>
static partial class English
{
    /// <summary>facebook/wav2vec2-base-960h (Apache-2.0) as ONNX, its MatMuls in int8 (tools/d2/english_onnx.py): 16 kHz
    /// samples in, the log probabilities of the blank, "'", the letters and the word break "|" out; the tokens in the
    /// model's metadata. Optional: with none, words in English are sung on "la" as before.</summary>
    public const string Model = "w2v-english-mm8.onnx";

    static readonly HashSet<char> Vowels = [.. "aeiou"];
    static readonly string[] Digraphs = ["ch", "sh", "th", "ph", "wh", "ck", "ng", "gh", "qu"];
    static readonly HashSet<string> Two = ["io", "ia", "eo", "ua", "uo", "iu"];
    [GeneratedRegex(@"[A-Za-z']+")] private static partial Regex Word();

    /// <summary>The words of the lines, each cut in syllables.</summary>
    public static List<Mora> Read(List<string> lines)
    {
        var res = new List<Mora>();
        foreach (var line in lines)
            foreach (Match m in Word().Matches(line))
            {
                string w = m.Value.Trim('\'');
                if (w.Length == 0) continue;
                var parts = Split(w);
                for (int i = 0; i < parts.Count; i++) res.Add(new Mora(parts[i], Joined: i > 0));
            }
        return res;
    }

    /// <summary>Lyrics enough to put to the voice in English: 20 words and more, few of them in kana or kanji.</summary>
    public static bool Sung(List<string> lines, List<Mora> syllables)
    {
        int words = syllables.Count(s => !s.Joined);
        int other = lines.Sum(l => l.Count(c => c is >= '぀' and <= 'ヿ' or >= '一' and <= '鿿'));
        return words >= 20 && other <= words;
    }

    static bool VowelAt(string w, int i)
    {
        char c = w[i];
        if (Vowels.Contains(c)) return !(c == 'u' && i > 0 && w[i - 1] == 'q');
        return c == 'y' && i > 0 && !Vowels.Contains(w[i - 1]);      // y after a consonant (my, baby), not first (you) nor after a vowel (boy)
    }

    /// <summary>The vowel groups, [start, end): less a silent e, more where a group is said as two.</summary>
    static List<(int A, int B)> Groups(string w)
    {
        var found = new List<(int, int)>();
        for (int i = 0; i < w.Length;)
        {
            if (!VowelAt(w, i)) { i++; continue; }
            int j = i;
            while (j + 1 < w.Length && VowelAt(w, j + 1)) j++;
            found.Add((i, j + 1));
            i = j + 1;
        }
        var res = new List<(int A, int B)>();
        foreach (var (a, b) in found)
        {
            string g = w[a..b];
            if (b - a >= 2 && g.EndsWith('i') && b + 2 <= w.Length && w.Substring(b, 2) == "ng") { res.Add((a, b - 1)); res.Add((b - 1, b)); }   // go-ing, be-ing
            else if (b - a == 2 && Two.Contains(g) && !(g == "ua" && a > 0 && w[a - 1] == 'q')) { res.Add((a, a + 1)); res.Add((a + 1, b)); }   // li-on
            else res.Add((a, b));
        }
        if (res.Count >= 2)
        {
            var (a, b) = res[^1];
            string tail = w[a..];
            char before = a > 0 ? w[a - 1] : ' ';
            bool silent = (tail == "e" && !(before == 'l' && a >= 2 && !Vowels.Contains(w[a - 2])))      // love, not lit-tle
                       || (tail == "es" && !"sxzhcg".Contains(before) && !w.EndsWith("les"))             // loves, not kiss-es
                       || (tail == "ed" && !"td".Contains(before));                                       // loved, not want-ed
            if (silent && w[a..b] == "e") res.RemoveAt(res.Count - 1);
        }
        return res;
    }

    /// <summary>A word's syllables as written: the consonants between two vowel groups go one to each side, one to the
    /// next (ba-by), digraphs kept whole.</summary>
    public static List<string> Split(string word)
    {
        string w = word.ToLowerInvariant();
        var g = Groups(w);
        if (g.Count <= 1) return [word];
        var cuts = new List<int>();
        for (int i = 0; i + 1 < g.Count; i++)
        {
            int b = g[i].B, c = g[i + 1].A, k = c - b;
            if (k <= 1) { cuts.Add(b); continue; }
            int cut = b + 1;
            string first = w.Substring(b, 2), last = w.Substring(c - 2, 2);
            if (Digraphs.Contains(first)) cut = first is "ck" or "ng" or "gh" || k > 2 ? b + 2 : b;
            else if (Digraphs.Contains(last) && last is not ("ck" or "ng" or "gh")) cut = c - 2;
            cuts.Add(Math.Max(Math.Min(cut, c), b));
        }
        var res = new List<string>();
        int prev = 0;
        foreach (int c in cuts) { res.Add(word[prev..c]); prev = c; }
        res.Add(word[prev..]);
        return [.. res.Where(s => s.Length > 0)];
    }
}
