using System.Text;
using System.Text.RegularExpressions;
using NMeCab.Specialized;

namespace Orecchiante;

/// <summary>A mora of the lyrics: its kana, or Kana.Any for words not read (Words, the words themselves). In English
/// (English.Read) a syllable as written, Joined when it goes on the word before it.</summary>
public sealed record Mora(string Kana, string? Words = null, bool Joined = false);

/// <summary>Lyrics in kana cut in morae (scratch/align/words.py's steps, with IPAdic through NMeCab in place of
/// unidic-lite through fugashi): ruby written after kanji, 誘(いざな), is sung as the ruby; the rest is read by the
/// dictionary's reading as written (は as は: measured on RWC-P 1-50 against the pronunciation, わ, 0.583 to 0.582 of
/// the bars with the truth's number of notes, and unidic's pronunciation 0.585, README "The words put to the voice");
/// a run of words it cannot read (English, mostly) is one Kana.Any that any kana may fill (Words.Align).</summary>
static partial class Kana
{
    public const string Any = "＊";
    static readonly HashSet<char> Small = [.. "ゃゅょぁぃぅぇぉゎ"];
    const string KanjiClass = "[一-鿿々〆ヵヶ]";
    [GeneratedRegex(@"(" + KanjiClass + @"+[(（][ぁ-んァ-ヶー]+[)）])")] private static partial Regex RubySplit();
    [GeneratedRegex(@"^" + KanjiClass + @"+[(（]([ぁ-んァ-ヶー]+)[)）]$")] private static partial Regex Ruby();
    [GeneratedRegex(@"^[\W_]+$")] private static partial Regex Marks();
    [GeneratedRegex(@"^[ぁ-んァ-ヶー]+$")] private static partial Regex AllKana();

    static MeCabIpaDicTagger? tagger;

    /// <summary>Katakana as hiragana (ァ..ヶ down by 0x60), the rest as it is.</summary>
    public static string Hira(string s) => string.Concat(s.Select(c => c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c));

    /// <summary>A line of lyrics in hiragana with ー, a "＊" for each run of words not read, and those runs.</summary>
    public static (string Kana, List<string> Dropped) Of(string text)
    {
        tagger ??= MeCabIpaDicTagger.Create(Path.Combine(AppContext.BaseDirectory, "IpaDic"));
        var dropped = new List<string>();
        var kana = new StringBuilder();
        foreach (var part in RubySplit().Split(text))
        {
            if (part.Length == 0) continue;
            var r = Ruby().Match(part);
            if (r.Success) { kana.Append(Hira(r.Groups[1].Value)); continue; }
            foreach (var node in tagger.Parse(part))
            {
                string surface = node.Surface;
                if (Marks().IsMatch(surface)) continue;
                string? reading = node.Reading;
                if (string.IsNullOrEmpty(reading) || reading == "*")
                {
                    if (AllKana().IsMatch(surface)) reading = surface;
                    else
                    {
                        if (kana.Length > 0 && kana[^1] == Any[0]) dropped[^1] += " " + surface;
                        else { dropped.Add(surface); kana.Append(Any); }
                        continue;
                    }
                }
                kana.Append(Hira(reading));
            }
        }
        return (kana.ToString(), dropped);
    }

    /// <summary>Kana cut in morae: a small kana with the one before it; ー, ゔ and ＊ morae of their own; anything else
    /// (letters, digits) left out.</summary>
    public static List<string> Morae(string kana)
    {
        var morae = new List<string>();
        foreach (char c in kana)
        {
            if (Small.Contains(c) && morae.Count > 0) morae[^1] += c;
            else if (c is >= 'ぁ' and <= 'ん' || c is 'ー' or 'ゔ' || c == Any[0]) morae.Add(c.ToString());
        }
        return morae;
    }

    /// <summary>Lines of lyrics as morae, each Any with the words it stands for.</summary>
    public static List<Mora> Read(IEnumerable<string> lines)
    {
        var all = new List<Mora>();
        foreach (var line in lines)
        {
            var (kana, dropped) = Of(line);
            int d = 0;
            foreach (var m in Morae(kana)) all.Add(m == Any ? new Mora(m, dropped[d++]) : new Mora(m));
        }
        return all;
    }

    /// <summary>The vowel a mora ends on, as a kana (あいうえお), or null (ん, っ, ＊).</summary>
    public static string? Vowel(string mora)
    {
        const string rows = "あいうえおかきくけこがぎぐげごさしすせそざじずぜぞたちつてとだぢづでどなにぬねのはひふへほばびぶべぼぱぴぷぺぽまみむめもやゐゆゑよらりるれろわゐゔゑを";
        const string vowels = "あいうえお";
        string m = mora.TrimEnd('っ');
        if (m.Length == 0) return null;
        char last = m[^1];
        switch (last)
        {
            case 'ゃ' or 'ぁ' or 'ゎ': return "あ";
            case 'ぃ': return "い";
            case 'ゅ' or 'ぅ': return "う";
            case 'ぇ': return "え";
            case 'ょ' or 'ぉ': return "お";
        }
        int i = rows.IndexOf(last);
        if (last == 'や') return "あ";
        if (last == 'ゆ') return "う";
        if (last == 'よ') return "お";
        return i < 0 ? null : vowels[i % 5].ToString();
    }
}
