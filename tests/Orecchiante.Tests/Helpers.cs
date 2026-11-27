using Xunit;

namespace Orecchiante.Tests;

/// <summary>The helpers that need neither the models nor a recording: the syllables, the kana, the notes' lengths
/// as written, the .lrc, the file beside the chart's strings.</summary>
public class EnglishTests
{
    [Theory]
    [InlineData("baby", "ba|by")]
    [InlineData("little", "lit|tle")]
    [InlineData("love", "love")]          // a silent e
    [InlineData("loved", "loved")]        // -ed said with the word
    [InlineData("wanted", "wan|ted")]     // -ed after t, a syllable
    [InlineData("going", "go|ing")]
    [InlineData("Rock", "Rock")]          // the spelling kept as written
    public void Split_cuts_a_word_in_syllables_by_its_spelling(string word, string syllables) =>
        Assert.Equal(syllables.Split('|'), English.Split(word));

    [Fact]
    public void Read_joins_the_syllables_after_a_words_first()
    {
        var m = English.Read(["Little Rock!"]);
        Assert.Equal(["Lit", "tle", "Rock"], m.Select(x => x.Kana));
        Assert.Equal([false, true, false], m.Select(x => x.Joined));
    }

    [Fact]
    public void Sung_needs_twenty_words_mostly_not_japanese()
    {
        var few = new List<string> { "one two three" };
        Assert.False(English.Sung(few, English.Read(few)));
        var many = new List<string> { string.Join(' ', Enumerable.Repeat("love", 20)) };
        Assert.True(English.Sung(many, English.Read(many)));
    }
}

public class KanaTests
{
    [Fact]
    public void Morae_keep_a_small_kana_with_the_one_before() =>
        Assert.Equal(["きょ", "う", "は", "ー"], Kana.Morae("きょうはー"));

    [Theory]
    [InlineData("か", "あ")]
    [InlineData("きょ", "お")]
    [InlineData("いぇ", "え")]
    [InlineData("ん", null)]
    public void Vowel_is_the_one_a_mora_ends_on(string mora, string? vowel) => Assert.Equal(vowel, Kana.Vowel(mora));

    [Fact]
    public void Hira_turns_katakana_to_hiragana() => Assert.Equal("かたかなabc", Kana.Hira("カタカナabc"));

    [Fact]
    public void Of_reads_kanji_by_the_dictionary() => Assert.Equal("こよいのつきのように", Kana.Of("今宵の月のように").Kana);

    [Fact]
    public void The_particle_ha_is_sung_wa_and_shown_as_written()
    {
        var m = Kana.Read(["私は"]);
        Assert.Equal(["わ", "た", "し", "わ"], m.Select(x => x.Kana));
        Assert.Equal("は", m[^1].Shown);
        Assert.Null(m[0].Shown);
    }

    [Fact]
    public void English_words_of_the_dictionary_are_one_note_each_shown_as_written()
    {
        var m = Kana.Read(["Oh yeah"]);
        Assert.Equal(["おぅ", "いぇー"], m.Select(x => x.Kana));
        Assert.Equal(["Oh", "yeah"], m.Select(x => x.Shown));
    }

    [Fact]
    public void Words_not_read_are_one_any()
    {
        var m = Kana.Read(["Wheee"]);
        Assert.Single(m);
        Assert.Equal(Kana.Any, m[0].Kana);
        Assert.Equal("Wheee", m[0].Words);
    }
}

public class BassTests
{
    [Theory]
    [InlineData(5, false, 5)]
    [InlineData(4, true, 6)]      // the second dotted quarter
    [InlineData(5, true, 8)]
    [InlineData(7, true, 10)]
    public void Written_puts_the_grid_among_the_dotted_quarters_six_in_six_eight(int q, bool six, int written) =>
        Assert.Equal(written, Bass.Written(q, six));

    [Theory]
    [InlineData(16, false, "r1")]
    [InlineData(12, false, "r2.")]           // 3/4
    [InlineData(12, true, "r2. r4.")]        // 6/8: a dotted half, not a half
    public void Rests_fill_the_bar(int sixteenths, bool six, string rests) =>
        Assert.Equal(rests, Bass.Rests(sixteenths, six));

    [Fact]
    public void Pieces_do_not_cross_a_beat_from_off_it() => Assert.Equal([2, 6], Bass.Pieces(2, 8));

    [Fact]
    public void BarMusic_writes_notes_rests_and_ties_in_the_bass_players_octave()
    {
        var names = Bass.NamesFor("C major");
        Assert.Equal("e4 r4 g2~", Bass.BarMusic([(0, 4, 40, false), (8, 16, 43, true)], 16, names));
    }

    [Theory]
    [InlineData("F major", "bes")]
    [InlineData("A minor", "ais")]
    [InlineData("D minor", "bes")]
    public void NamesFor_spells_with_flats_in_the_flat_keys(string key, string tenth) => Assert.Equal(tenth, Bass.NamesFor(key)[10]);
}

public class WordsTests
{
    [Theory]
    [InlineData("[01:02.50]", 62.5)]
    [InlineData("<00:03.25>", 3.25)]
    [InlineData("[00:07:10]", 7.1)]
    public void Seconds_reads_an_lrc_time(string tag, double seconds) => Assert.Equal(seconds, Words.Seconds(tag), 6);

    [Fact]
    public void LinesOf_an_lrc_drops_the_tags_and_sings_a_line_at_each_of_its_times()
    {
        string path = Path.Combine(Path.GetTempPath(), $"orec-test-{Guid.NewGuid():N}.lrc");
        File.WriteAllLines(path, ["[ti:Song]", "[00:10.00][00:30.00]chorus", "[00:20.00]<00:20.00>verse", "[00:25.00]"]);
        try { Assert.Equal(["chorus", "verse", "chorus"], Words.LinesOf(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RoundOnto_makes_two_syllables_of_a_word_on_one_16th_one()
    {
        double[] slots = [.. Enumerable.Range(0, 17).Select(i => i * 0.125)];
        var notes = new List<Words.Note> { new(0.0, 0.2, 60, "Lit-"), new(0.01, 0.24, 60, "tle"), new(0.5, 0.7, 62, "Rock") };
        var r = Words.RoundOnto(slots, notes);
        Assert.Equal(["Little", "Rock"], r.Select(x => x.Syllable));
        Assert.Equal(4, r[1].First);
    }
}

public class LyricsTests
{
    [Fact]
    public void An_lrc_written_reads_back_as_the_song()
    {
        var song = new Song(123, "T", "A", "Al", 125.4, false, "[00:01.00]hello\n[00:02.00]world", null);
        string path = Path.Combine(Path.GetTempPath(), $"orec-test-{Guid.NewGuid():N}.lrc");
        File.WriteAllText(path, Lyrics.Lrc(song, "0.0.0"));
        try
        {
            var back = Lyrics.FromLrc(path);
            Assert.Equal((123L, "T", "A", "Al", 125.0), (back.Id, back.Title, back.Artist, back.Album, back.Seconds));
            Assert.Equal("[00:01.00]hello\n[00:02.00]world", back.Synced);
            Assert.Null(back.Plain);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Notice_says_where_the_words_came_from()
    {
        var lines = Lyrics.Notice(new Song(42, "T", "A", "", 0, false, null, "x")).ToList();
        Assert.Equal(3, lines.Count);
        Assert.All(lines, l => Assert.StartsWith("// ", l));
        Assert.Contains("entry 42", lines[2]);
    }
}

public class WriterTests
{
    [Theory]
    [InlineData("今宵の月のように", true)]
    [InlineData("ｶﾀｶﾅ", true)]
    [InlineData("We're An American Band", false)]
    public void Japanese_titles_have_kana_or_kanji(string title, bool japanese) => Assert.Equal(japanese, Writer.Japanese(title));

    [Fact]
    public void Quoted_escapes_as_json() => Assert.Equal("\"a\\\"b\\\\c\\n\\u3042\"", Writer.Quoted("a\"b\\c\nあ"));

    [Fact]
    public void A_pickup_is_written_a_chord_a_beat()
    {
        // three beats of 4/4 before the first bar, G for two and G7 for one: Lily# counts a pickup's slots by its own beats
        // (Lily#'s request 3, c9f060dc8; before it, Orecchiante wrote the last chord alone)
        static Chord C(string name, double start, double end) => new() { Name = name, Start = start, End = end };
        var chart = new Chart
        {
            Audio = "x.wav", Bpm = 120, Form = ["Pickup", "A"],
            Bars =
            [
                new Bar { Number = 0, Pickup = 3, Section = "Pickup", BarOfSection = 1, Start = 0, End = 1.5, Chords = [C("G", 0, 1), C("G7", 1, 1.5)] },
                new Bar { Number = 1, Section = "A", BarOfSection = 1, Start = 1.5, End = 3.5, Chords = [C("C", 1.5, 3.5)] },
            ],
        };
        string lys = Writer.Lys(chart, "t", _ => null);
        Assert.Contains("  partial 2.\n", lys);
        Assert.Contains("\n    G . G7 |\n", lys);
    }
}

public class WavTests
{
    [Fact]
    public void A_wav_written_reads_back()
    {
        double[] y = [.. Enumerable.Range(0, 4410).Select(i => 0.5 * Math.Sin(2 * Math.PI * 440 * i / 44100.0))];
        string path = Path.Combine(Path.GetTempPath(), $"orec-test-{Guid.NewGuid():N}.wav");
        Wav.Write(path, y, 44100);
        try
        {
            var (mono, rate) = Wav.Read(path);
            Assert.Equal(44100, rate);
            Assert.Equal(y.Length, mono.Length);
            Assert.True(y.Zip(mono).Max(p => Math.Abs(p.First - p.Second)) < 1e-3);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Resample_halves_the_length()
    {
        var x = new double[44100];
        Assert.InRange(Resample.To(x, 1, 2).Length, 22049, 22051);
    }
}
