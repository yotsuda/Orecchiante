using System.Text;
using System.Text.RegularExpressions;
using NMeCab.Specialized;

namespace Orecchiante;

/// <summary>A mora of the lyrics: its kana, or Kana.Any for words not read (Words, the words themselves). In English
/// (English.Read) a syllable as written, Joined when it goes on the word before it. Shown: printed so with the kana
/// as its reading (Lily#'s `Ah[あぁ]`, `は[わ]`): a word of Kana.Sung, or the particles は and へ.</summary>
public sealed record Mora(string Kana, string? Words = null, bool Joined = false, string? Shown = null);

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

    /// <summary>Words a Japanese song sings that the dictionary does not read (English, mostly), and how a Japanese voice
    /// sings them (the owner, 2026-11-27: "Ah" failed the singing voice; write `Ah[あぁ]`, `Oh[おぅ]`). Looked up by the
    /// word in lower case without marks; each a note of its own, printed as written with the kana as its reading.</summary>
    static readonly Dictionary<string, string> Sung = new()
    {
        ["ah"] = "あぁ", ["aah"] = "あぁ", ["ahh"] = "あぁ", ["uh"] = "あぁ", ["oh"] = "おぅ", ["ohh"] = "おぅ", ["o"] = "おぅ",
        ["ooh"] = "うぅ", ["oooh"] = "うぅ", ["ooo"] = "うぅ", ["woo"] = "うぅ", ["wow"] = "わお", ["whoa"] = "うぉー",
        ["yeah"] = "いぇー", ["yeh"] = "いぇー", ["yea"] = "いぇー", ["ya"] = "や", ["hey"] = "へい", ["ha"] = "は", ["hoo"] = "ふぅ",
        ["la"] = "ら", ["na"] = "な", ["da"] = "だ", ["doo"] = "どぅ", ["shoo"] = "しゅ", ["bop"] = "ばっぷ",
        ["baby"] = "べいびー", ["love"] = "らぶ", ["my"] = "まい", ["you"] = "ゆー", ["i"] = "あい", ["me"] = "みー",
        ["yes"] = "いぇす", ["no"] = "のー", ["go"] = "ごー", ["come"] = "かむ", ["on"] = "おん", ["say"] = "せい",
        ["good"] = "ぐっど", ["bye"] = "ばい", ["night"] = "ないと", ["dream"] = "どりーむ", ["dance"] = "だんす",
    };

    /// <summary>A line of lyrics as pieces: kana to be cut in morae; a word printed one way and sung another (Shown,
    /// one mora however long: a word of Sung, the particles は and へ); or Kana.Any for a run of words not read
    /// (Dropped, the run).</summary>
    static List<(string Kana, string? Shown, string? Dropped)> Pieces(string text)
    {
        tagger ??= MeCabIpaDicTagger.Create(Path.Combine(AppContext.BaseDirectory, "IpaDic"));
        var res = new List<(string Kana, string? Shown, string? Dropped)>();
        foreach (var part in RubySplit().Split(text))
        {
            if (part.Length == 0) continue;
            var r = Ruby().Match(part);
            if (r.Success) { res.Add((Hira(r.Groups[1].Value), null, null)); continue; }
            foreach (var node in tagger.Parse(part))
            {
                string surface = node.Surface;
                if (Marks().IsMatch(surface)) continue;
                if (node.PartsOfSpeech == "助詞" && surface is "は" or "へ")     // sung わ and え
                {
                    res.Add((surface == "は" ? "わ" : "え", surface, null));
                    continue;
                }
                string? reading = node.Reading;
                if (string.IsNullOrEmpty(reading) || reading == "*")
                {
                    if (AllKana().IsMatch(surface)) reading = surface;
                    else if (Sung.TryGetValue(surface.Trim('\'', '’').ToLowerInvariant(), out var said))
                    {
                        res.Add((said, surface, null));
                        continue;
                    }
                    else
                    {
                        if (res.Count > 0 && res[^1].Kana == Any) res[^1] = res[^1] with { Dropped = res[^1].Dropped + " " + surface };
                        else res.Add((Any, null, surface));
                        continue;
                    }
                }
                res.Add((Hira(reading), null, null));
            }
        }
        return res;
    }

    /// <summary>A line of lyrics in hiragana with ー (the particles は and へ as sung, わ and え; a word of Sung as its
    /// kana), a "＊" for each run of words not read, and those runs.</summary>
    public static (string Kana, List<string> Dropped) Of(string text)
    {
        var pieces = Pieces(text);
        return (string.Concat(pieces.Select(p => p.Kana)), [.. pieces.Where(p => p.Dropped != null).Select(p => p.Dropped!)]);
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
            foreach (var (kana, shown, dropped) in Pieces(line))
            {
                if (dropped != null) all.Add(new Mora(Any, dropped));
                else if (shown != null) all.Add(new Mora(kana, Shown: shown));
                else all.AddRange(Morae(kana).Select(m => new Mora(m)));
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
