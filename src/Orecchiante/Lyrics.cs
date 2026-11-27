// A recording's lyrics from LRCLIB (https://lrclib.net, "a free and open lyrics database"; no key, the client named in
// User-Agent; docs: https://lrclib.net/docs.md), written beside the recording as <name>.lrc, for the voice staff to
// take its syllables from (Shosa's request 1, step 2). The owner, 2026-11-26: the user's lyrics, for what the user does
// with their own recordings; no lyrics site is read, and nothing is fetched unless `orecchiante lyrics` is run.
//
// What is asked: the title, the artist and the length the recording's tags give (Tags.Of), the title from
// the file's name when there is no tag. LRCLIB's /api/get answers only within 2 s of the length it has; when it has
// nothing, /api/search, keeping the songs within 2 s of the recording, those with times on their lines first.
// Requests go one at a time, 300 ms apart, and a 429 or 503 is waited out as its Retry-After says (as LRCLIB asks).
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Orecchiante;

public sealed record Song(long Id, string Title, string Artist, string Album, double Seconds, bool Instrumental, string? Synced, string? Plain);

public static class Lyrics
{
    const string Site = "https://lrclib.net";
    public const double Within = 2;     // seconds: LRCLIB's own tolerance on the length

    public sealed class Unreached(string why) : Exception(why);

    static HttpClient Client(string version)
    {
        var client = new HttpClient { BaseAddress = new Uri(Site), Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Orecchiante/{version} (+https://github.com/yotsuda/Orecchiante)");
        return client;
    }

    /// <summary>Requests to LRCLIB one at a time, 300 ms apart, a 429 or 503 waited out as its Retry-After says; null
    /// for a 404. Throws Unreached when LRCLIB cannot be asked (no network, an answer it should not give).</summary>
    sealed class Asker(string version, Action<string>? say) : IDisposable
    {
        readonly HttpClient client = Client(version);
        bool first = true;

        public JsonElement? Ask(string query)
        {
            if (!first) Thread.Sleep(300);
            first = false;
            say?.Invoke("asking LRCLIB: " + query);
            for (int attempt = 0; ; attempt++)
            {
                HttpResponseMessage answer;
                try { answer = client.GetAsync(query).GetAwaiter().GetResult(); }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
                {
                    throw new Unreached($"LRCLIB was not reached ({e.Message.Trim()})");
                }
                using (answer)
                {
                    if (answer.StatusCode == HttpStatusCode.NotFound) return null;
                    if (answer.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable && attempt < 2)
                    {
                        var after = answer.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
                        if (after > TimeSpan.FromSeconds(60)) throw new Unreached($"LRCLIB asks to wait {after.TotalSeconds:F0} s");
                        after *= 1 + 0.3 * Random.Shared.NextDouble();
                        say?.Invoke($"  LRCLIB is busy: waiting {after.TotalSeconds:F1} s");
                        Thread.Sleep(after);
                        continue;
                    }
                    if (!answer.IsSuccessStatusCode) throw new Unreached($"LRCLIB answered {(int)answer.StatusCode} {answer.ReasonPhrase}");
                    using var json = JsonDocument.Parse(answer.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                    return json.RootElement.Clone();
                }
            }
        }

        /// <summary>The songs a search answers.</summary>
        public List<Song> Songs(string query) =>
            Ask(query) is { ValueKind: JsonValueKind.Array } list ? [.. list.EnumerateArray().Select(Of).OfType<Song>()] : [];

        public void Dispose() => client.Dispose();
    }

    static string E(string s) => Uri.EscapeDataString(s);

    /// <summary>The songs a person may choose among (`orecchiante lyrics --list`): with `query`, LRCLIB's search for
    /// those words alone; else every search Find makes for the title and artist (by both, by both as words, by the
    /// title's words), not cut to the length. Each once; those within Within s of the recording first, then with words,
    /// with times on their lines, nearer in length; at most `most`.</summary>
    public static List<Song> Candidates(string title, string? artist, double seconds, string? query, string version, Action<string>? say = null, int most = 20)
    {
        using var ask = new Asker(version, say);
        var found = new List<Song>();
        if (query != null) found.AddRange(ask.Songs($"/api/search?q={E(query)}"));
        else
        {
            if (artist != null)
            {
                found.AddRange(ask.Songs($"/api/search?track_name={E(title)}&artist_name={E(artist)}"));
                found.AddRange(ask.Songs($"/api/search?q={E(artist + " " + title)}"));
            }
            found.AddRange(ask.Songs($"/api/search?q={E(title)}"));
        }
        return [.. found.DistinctBy(s => s.Id)
            .OrderBy(s => !Fits(s, seconds))
            .ThenBy(s => s.Instrumental || (s.Synced == null && s.Plain == null))
            .ThenBy(s => s.Synced == null)
            .ThenBy(s => seconds > 0 ? Math.Abs(s.Seconds - seconds) : 0)
            .Take(most)];
    }

    /// <summary>The song LRCLIB has under this number (`orecchiante lyrics --id`), or null.</summary>
    public static Song? ById(long id, string version, Action<string>? say = null)
    {
        using var ask = new Asker(version, say);
        return ask.Ask($"/api/get/{id}") is { } one ? Of(one) : null;
    }

    // The lyrics LRCLIB has for this recording, or null; `say` is told each request. Throws Unreached when LRCLIB
    // cannot be asked (no network, an answer it should not give).
    public static Song? Find(string title, string? artist, double seconds, string version, Action<string>? say = null)
    {
        using var asker = new Asker(version, say);
        JsonElement? Ask(string query) => asker.Ask(query);
        string length = seconds > 0 ? $"&duration={Math.Round(seconds):0}" : "";

        // The one LRCLIB matches itself, when the artist is known. Not by the album: with one LRCLIB does not have (a
        // compilation, another release) it finds nothing, though the song and its length are there (2026-11-26).
        if (artist != null)
        {
            string query = $"/api/get?track_name={E(title)}&artist_name={E(artist)}" + length;
            if (Ask(query) is { } one && Of(one) is { } song && Fits(song, seconds)) return song;
        }
        // Else a search: by title and artist, or by the words of the title alone (a file named "Artist - Title" too).
        string search = artist != null ? $"/api/search?track_name={E(title)}&artist_name={E(artist)}" : $"/api/search?q={E(title)}";
        var found = Ask(search) is { ValueKind: JsonValueKind.Array } list ? list.EnumerateArray().Select(Of).OfType<Song>().ToList() : [];
        if (artist != null && !found.Any(s => Fits(s, seconds)))
            found = Ask($"/api/search?q={E(artist + " " + title)}") is { ValueKind: JsonValueKind.Array } more ? more.EnumerateArray().Select(Of).OfType<Song>().ToList() : [];
        return found.Where(s => Fits(s, seconds))
            .OrderBy(s => s.Instrumental)                       // a song with words before one LRCLIB says has none
            .ThenBy(s => s.Synced == null)                      // the lines with their times first
            .ThenBy(s => seconds > 0 ? Math.Abs(s.Seconds - seconds) : 0)
            .FirstOrDefault();
    }

    static bool Fits(Song song, double seconds) => seconds <= 0 || Math.Abs(song.Seconds - seconds) <= Within;

    static Song? Of(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        string? S(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double seconds = e.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0;
        long id = e.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt64() : 0;
        bool instrumental = e.TryGetProperty("instrumental", out var n) && n.ValueKind == JsonValueKind.True;
        static string? Words(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
        return new Song(id, S("trackName") ?? "", S("artistName") ?? "", S("albumName") ?? "", seconds, instrumental, Words(S("syncedLyrics")), Words(S("plainLyrics")));
    }

    // What a chart with these words says at its head, as comments (the owner, 2026-11-26: "an appropriate copyright
    // notice"). Whose words they are is not known here (LRCLIB names the performer, not the writer), so the notice
    // names the song and the performer, gives the words to their writers and publishers, and says where the text came from.
    public static IEnumerable<string> Notice(Song song)
    {
        static string C(string s) => string.Join(' ', s.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        string title = C(song.Title), artist = C(song.Artist), album = C(song.Album);
        yield return "// Lyrics: \"" + (title != "" ? title : "untitled") + "\"" + (artist != "" ? $" as performed by {artist}" : "")
                     + (album != "" ? $" ({album})" : "");
        yield return "// The words are copyright of their writers and publishers, who reserve all rights.";
        yield return (song.Id > 0 ? $"// Text from LRCLIB (https://lrclib.net), entry {song.Id}" : "// Text from the .lrc beside the recording")
                     + ", for personal use only: do not share or publish the words with this chart.";
    }

    // A .lrc as `orecchiante lyrics` writes it, back into the song: the tags and the lines (with their times).
    public static Song FromLrc(string path)
    {
        string title = "", artist = "", album = "";
        double seconds = 0;
        long id = 0;
        var lines = new List<string>();
        foreach (var raw in File.ReadAllLines(path))
        {
            var tag = System.Text.RegularExpressions.Regex.Match(raw, @"^\[(ti|ar|al|length|re):(.*)\]\s*$");
            if (!tag.Success) { lines.Add(raw); continue; }
            string value = tag.Groups[2].Value.Trim();
            switch (tag.Groups[1].Value)
            {
                case "ti": title = value; break;
                case "ar": artist = value; break;
                case "al": album = value; break;
                case "length":
                    var mmss = value.Split(':');
                    if (mmss.Length == 2 && int.TryParse(mmss[0], out int m) && double.TryParse(mmss[1], System.Globalization.CultureInfo.InvariantCulture, out double s)) seconds = m * 60 + s;
                    break;
                case "re":
                    var lrclib = System.Text.RegularExpressions.Regex.Match(value, @"LRCLIB (\d+)");
                    if (lrclib.Success) id = long.Parse(lrclib.Groups[1].Value);
                    break;
            }
        }
        string text = string.Join('\n', lines).Trim('\n');
        bool timed = lines.Any(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\[\d+:\d+"));
        return new Song(id, title, artist, album, seconds, false, timed ? text : null, timed ? null : text);
    }

    // The .lrc: LRC's tags, then the lines with their times, else the words without them.
    public static string Lrc(Song song, string version)
    {
        var lrc = new StringBuilder();
        static string Tag(string s) => s.Replace("]", ")").Replace("\r", " ").Replace("\n", " ").Trim();
        if (song.Title != "") lrc.Append($"[ti:{Tag(song.Title)}]\n");
        if (song.Artist != "") lrc.Append($"[ar:{Tag(song.Artist)}]\n");
        if (song.Album != "") lrc.Append($"[al:{Tag(song.Album)}]\n");
        if (song.Seconds > 0) lrc.Append($"[length:{(int)song.Seconds / 60:D2}:{(int)song.Seconds % 60:D2}]\n");
        lrc.Append($"[re:Orecchiante {version}, LRCLIB {song.Id}]\n");
        foreach (var line in (song.Synced ?? song.Plain ?? "").Replace("\r\n", "\n").Split('\n'))
            lrc.Append(line.TrimEnd()).Append('\n');
        return lrc.ToString();
    }
}

// A recording's title, artists, album and length as Windows' property system reads them from its tags (ID3, MP4,
// FLAC's comments, ASF): what Explorer shows. On macOS, AudioToolbox's (Apple.Tags).
public static class Tags
{
    public sealed record Found(string? Title, string? Artist, string? Album, double Seconds);

    public static Found Of(string path)
    {
        if (OperatingSystem.IsMacOS()) return Apple.Tags(path);
        if (!OperatingSystem.IsWindows()) return new(null, null, null, 0);
        var iid = typeof(IPropertyStore).GUID;
        if (SHGetPropertyStoreFromParsingName(Path.GetFullPath(path), IntPtr.Zero, 0, ref iid, out var store) != 0 || store == null)
            return new(null, null, null, 0);
        try
        {
            string? title = Text(store, Music.Title);
            string? artist = Text(store, Music.Artist) ?? Text(store, Music.AlbumArtist);
            string? album = Text(store, Music.Album);
            double seconds = Number(store, Music.Duration) / 1e7;        // in 100 ns
            return new(title, artist, album, seconds);
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    static class Music
    {
        static readonly Guid Summary = new("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), Track = new("56A3372E-CE9C-11D2-9F0E-006097C686F6"),
                             Media = new("64440490-4C8B-11D1-8B70-080036B11A03");
        public static readonly PropertyKey Title = new(Summary, 2), Artist = new(Track, 2), AlbumArtist = new(Track, 13),
                                           Album = new(Track, 4), Duration = new(Media, 3);
    }

    // A string, or the strings of a list joined ("; " as Explorer joins them); null when there is none.
    static string? Text(IPropertyStore store, PropertyKey key)
    {
        var value = new PropVariant();
        try
        {
            if (store.GetValue(ref key, out value) != 0) return null;
            string? text = value.Type switch
            {
                VtLpwstr => Marshal.PtrToStringUni(value.Pointer),
                VtVector | VtLpwstr => string.Join("; ", Enumerable.Range(0, (int)value.Count)
                    .Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(value.Elements, i * IntPtr.Size)))
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
                _ => null,
            };
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        finally { PropVariantClear(ref value); }
    }

    static double Number(IPropertyStore store, PropertyKey key)
    {
        var value = new PropVariant();
        try { return store.GetValue(ref key, out value) == 0 && value.Type == VtUi8 ? (double)(ulong)value.Long : 0; }
        finally { PropVariantClear(ref value); }
    }

    const ushort VtLpwstr = 31, VtUi8 = 21, VtVector = 0x1000;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct PropertyKey(Guid format, uint id) { public Guid Format = format; public uint Id = id; }

    // PROPVARIANT: the type, three reserved words, then the value (a pointer, a 64-bit number, or a list's count and
    // its elements).
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
        [FieldOffset(8)] public long Long;
        [FieldOffset(8)] public uint Count;
        [FieldOffset(16)] public IntPtr Elements;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr bindContext, int flags, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PropVariant value);
}
