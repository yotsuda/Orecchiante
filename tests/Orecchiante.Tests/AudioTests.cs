using System.Diagnostics;
using Xunit;

namespace Orecchiante.Tests;

/// <summary>Where a recording's sound begins, as the system's decoders give it (Audio.Read: Media Foundation on
/// Windows, AudioToolbox on macOS) against a player's: the bars' seconds are to be the player's (README "A recording
/// as it comes"). Data/chirp.wav is 1.5 s (44100 Hz, 16 bits, one channel) silent but for one chirp at sample 44100:
/// 882 samples rising linearly from 200 to 5000 Hz, a Hann window, 16000 at most; the other files are it encoded by ffmpeg 7
/// (2026-11-27: libmp3lame 128k with LAME's note, the same without it (-write_xing 0), ffmpeg's AAC in .m4a, FLAC).
/// Expected is where ffmpeg's own decoding puts the chirp (as a browser does, measured when the files were made).</summary>
public class AudioTests
{
    static string Data(string name) => Path.Combine(AppContext.BaseDirectory, "Data", name);

    // the samples by which the chirp comes later than in chirp.wav, in ffmpeg's decoding of each file
    [Theory]
    [InlineData("chirp.wav", 0)]
    [InlineData("chirp.flac", 0)]
    [InlineData("chirp.mp3", 0)]
    [InlineData("chirp-no-note.mp3", 1105)]
    [InlineData("chirp.m4a", 0)]
    public void The_sound_begins_where_a_player_begins_it(string file, int expected)
    {
        var (template, _) = Wav.Read(Data("chirp.wav"));
        var (mono, rate) = Audio.Read(Data(file));
        Assert.Equal(44100, rate);
        int lag = Lag(template, mono);
        Assert.True(Math.Abs(lag - expected) <= 1, $"{file}: the chirp {lag} samples late, a player's {expected} ({Environment.OSVersion})");
    }

    [Fact]
    public void Ffmpeg_puts_the_chirp_where_the_test_expects()
    {
        // only where ffmpeg is installed (this PC; not needed on the CI): the expected numbers above are ffmpeg's
        var (template, _) = Wav.Read(Data("chirp.wav"));
        foreach (var (file, expected) in new[] { ("chirp.flac", 0), ("chirp.mp3", 0), ("chirp-no-note.mp3", 1105), ("chirp.m4a", 0) })
        {
            if (Ffmpeg(Data(file)) is not { } decoded) return;
            Assert.True(Math.Abs(Lag(template, decoded) - expected) <= 1, $"{file}: ffmpeg {Lag(template, decoded)}");
        }
    }

    /// <summary>The lag (samples, later positive) at which the chirp of the template best fits y.</summary>
    internal static int Lag(double[] template, double[] y)
    {
        const int At = 44100, Length = 882, Most = 3000;
        int best = 0;
        double bestDot = double.MinValue;
        for (int k = -Most; k <= Most; k++)
        {
            double dot = 0;
            for (int i = At; i < At + Length; i++)
                if (i + k >= 0 && i + k < y.Length) dot += template[i] * y[i + k];
            if (dot > bestDot) { bestDot = dot; best = k; }
        }
        return best;
    }

    static double[]? Ffmpeg(string path)
    {
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "-v", "error", "-i", path, "-f", "f32le", "-ac", "1", "-" }) start.ArgumentList.Add(a);
        Process p;
        try { p = Process.Start(start)!; }
        catch (System.ComponentModel.Win32Exception) { return null; }
        using (p)
        {
            _ = p.StandardError.ReadToEndAsync();
            using var ms = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(ms);
            p.WaitForExit();
            var bytes = ms.ToArray();
            var f = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, f, 0, f.Length * 4);
            return [.. f.Select(v => (double)v)];
        }
    }
}
