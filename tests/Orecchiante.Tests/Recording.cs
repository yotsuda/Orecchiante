using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Orecchiante.Tests;

/// <summary>A recording made here (a song of 16 bars at 120 beats a minute in C major, C G Am F over and over, with
/// a bass on the roots and drums) read end to end by orecchiante.exe as Lily# runs it, with the models in onnx/
/// (tools/fetch-models.ps1, or ORECCHIANTE_MODELS). The reader is read once for all the tests of the class.</summary>
public sealed class RecordingFixture : IDisposable
{
    public const int Bpm = 120, Bars = 16, Rate = 44100;
    public static readonly string[] Progression = ["C", "G", "Am", "F"];
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), $"orec-e2e-{Guid.NewGuid():N}");
    public int Exit { get; }
    public string Stdout { get; }
    public string Stderr { get; }
    public string Lys => Path.Combine(Folder, "song.lys");
    public string Json => Path.Combine(Folder, "song.orecchiante.json");

    public RecordingFixture()
    {
        Directory.CreateDirectory(Folder);
        Wav.Write(Path.Combine(Folder, "song.wav"), Song(), Rate);
        string models = Environment.GetEnvironmentVariable("ORECCHIANTE_MODELS") is { Length: > 0 } m ? m : Path.Combine(Repo(), "onnx");
        if (!File.Exists(Path.Combine(models, "beat-this-final0-dynamo.onnx")))
            throw new InvalidOperationException($"no models in {models}: run tools/fetch-models.ps1 (or set ORECCHIANTE_MODELS)");
        // the reader as the extension runs it (orecchiante.exe, or orecchiante on macOS), else through dotnet
        string exe = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "orecchiante.exe" : "orecchiante");
        var start = new ProcessStartInfo(File.Exists(exe) ? exe : "dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (!File.Exists(exe)) start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "orecchiante.dll"));
        foreach (var a in new[] { Path.Combine(Folder, "song.wav"), "--models", models }) start.ArgumentList.Add(a);
        using var p = Process.Start(start)!;
        var err = p.StandardError.ReadToEndAsync();
        Stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        Stderr = err.Result;
        Exit = p.ExitCode;
    }

    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "onnx", "models.json"))) return d.FullName;
        throw new InvalidOperationException("the repository (onnx/models.json) not found above " + AppContext.BaseDirectory);
    }

    /// <summary>The song: a piano-like chord a bar (four notes, their harmonics dying away) struck on each beat, a bass
    /// on the root on beats 1 and 3, a bass drum on 1 and 3, a snare on 2 and 4, a hi-hat on the 8ths.</summary>
    static double[] Song()
    {
        int[][] chords = [[48, 52, 55, 60], [43, 47, 50, 55], [45, 48, 52, 57], [41, 45, 48, 53]];
        int[] roots = [36, 31, 33, 29];
        double beat = 60.0 / Bpm;
        int n = (int)((Bars * 4 + 2) * beat * Rate);
        var y = new double[n];
        var rng = new Random(1);
        void Add(double at, double seconds, Func<double, double> f)
        {
            int a = (int)(at * Rate), len = (int)(seconds * Rate);
            for (int i = 0; i < len && a + i < n; i++) y[a + i] += f((double)i / Rate);
        }
        static double Hz(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);
        for (int bar = 0; bar < Bars; bar++)
            for (int b = 0; b < 4; b++)
            {
                double t = (bar * 4 + b) * beat;
                int c = bar % 4;
                foreach (int note in chords[c])
                    Add(t, beat, s => 0.05 * Math.Exp(-3 * s) * Enumerable.Range(1, 5).Sum(h => Math.Sin(2 * Math.PI * Hz(note) * h * s) / h));
                if (b % 2 == 0)
                {
                    Add(t, 2 * beat * 0.9, s => 0.2 * Math.Exp(-1.5 * s) * (Math.Sin(2 * Math.PI * Hz(roots[c]) * s) + 0.3 * Math.Sin(4 * Math.PI * Hz(roots[c]) * s)));
                    Add(t, 0.15, s => 0.5 * Math.Exp(-25 * s) * Math.Sin(2 * Math.PI * (50 + 100 * Math.Exp(-30 * s)) * s));
                }
                else Add(t, 0.15, s => 0.25 * Math.Exp(-20 * s) * (rng.NextDouble() * 2 - 1));
                for (int h = 0; h < 2; h++) Add(t + h * beat / 2, 0.04, s => 0.06 * Math.Exp(-80 * s) * (rng.NextDouble() * 2 - 1));
            }
        return y;
    }

    /// <summary>ORECCHIANTE_KEEP=1 keeps the folder (its path in the test's output) to look at what was written.</summary>
    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable("ORECCHIANTE_KEEP") == "1") { Console.WriteLine("kept " + Folder); return; }
        try { Directory.Delete(Folder, true); } catch (IOException) { }
    }
}

public class RecordingTests(RecordingFixture f) : IClassFixture<RecordingFixture>
{
    string Why => $"exit {f.Exit}\nstderr:\n{f.Stderr}";

    [Fact]
    public void The_chart_and_the_file_beside_it_are_written()
    {
        Assert.True(f.Exit == 0, Why);
        Assert.Equal(f.Lys, f.Stdout.Trim());
        Assert.True(File.Exists(f.Lys), Why);
        Assert.True(File.Exists(f.Json), Why);
        using var json = JsonDocument.Parse(File.ReadAllText(f.Json));
        Assert.True(json.RootElement.GetProperty("bars").GetArrayLength() >= RecordingFixture.Bars - 2);
    }

    [Fact]
    public void The_tempo_and_the_meter_are_heard()
    {
        string lys = File.ReadAllText(f.Lys);
        Assert.Contains("time 4/4", lys);
        var tempo = Regex.Match(lys, @"tempo\s+(?:\S+\s*=\s*)?(\d+(?:\.\d+)?)");
        Assert.True(tempo.Success, lys);
        Assert.InRange(double.Parse(tempo.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), RecordingFixture.Bpm - 3, RecordingFixture.Bpm + 3);
    }

    [Fact]
    public void The_chords_are_heard()
    {
        string lys = File.ReadAllText(f.Lys);
        Assert.Matches(@"key (C major|a minor)", lys);      // C G Am F is either (the same key signature)
        foreach (var chord in RecordingFixture.Progression)
            Assert.Matches($@"(?<![A-Za-z#]){Regex.Escape(chord)}(?![A-Za-z#0-9])", lys);
    }

    [Fact]
    public void The_bass_and_the_drums_are_staves()
    {
        string lys = File.ReadAllText(f.Lys);
        Assert.Contains("steps", lys);
        Assert.Matches(@"\bbass\b", lys);
    }

    [Fact]
    public void The_parts_are_separated()
    {
        string parts = Path.Combine(f.Folder, "song.parts");
        foreach (var part in new[] { "drums", "bass", "other", "vocals", "guitar", "piano" })
            Assert.True(File.Exists(Path.Combine(parts, part + ".flac")), $"{part}.flac\n{Why}");
    }
}
