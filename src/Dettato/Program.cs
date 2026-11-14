// dettato: a recording, read into a chart (docs/ROADMAP.md, D2: the reader moved here from tools/read_chords.py).
//
//   dettato read <a .wav or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--lines <folder>] [--heard <folder>]
//
// writes <name>.lys (a Lily# chord chart) and <name>.dettato.json (docs/SIDECAR.md). --lines and --heard take the
// beats and the chords' probabilities from folders of <name>.beats and <name>.npz, as the two commands below and
// the tools in Python write them, in place of running the models (to compare with read_chords.py: tools/read_check.py).
//
// And the parts of it, each to a file:
//
//   dettato ears <a .wav or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--cqt]
//
// writes <name>.beats (as tools/track_beats.py does) and <name>.npz (as tools/crema_chords.py does);
// with --cqt, also <name>.cqt.npy, what the chord model was given. The models are beat-this-final0-dynamo.onnx
// and crema-chord.onnx (README: how they are made), beside the program unless --models says where.
//
//   dettato features <a .wav or a folder of them> --out <folder>
//
// writes <name>.feat.npz: oenv, chroma, bass and rms as read_chords.py's features() makes them for --ears,
// and tuning (tools/measure.py --net reads them in place of its own).
using System.Diagnostics;
using Dettato;
using Microsoft.ML.OnnxRuntime;

const string usage = "usage: dettato read <a .wav or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--lines <folder>] [--heard <folder>]\n"
                   + "       dettato ears <a .wav or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--cqt]\n"
                   + "       dettato features <a .wav or a folder of them> --out <folder>";
if (args.Length < 2 || args[0] is not ("read" or "ears" or "features"))
{
    Console.Error.WriteLine(usage);
    return 2;
}
string? Option(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
string output = Option("--out") ?? throw new ArgumentException("--out <folder>");

var started = Stopwatch.StartNew();
var files = Directory.Exists(args[1]) ? Directory.GetFiles(args[1], "*.wav").Order().ToArray() : [args[1]];
Directory.CreateDirectory(output);
int n = files.Length;
double load = 0, mel = 0, beat = 0, cqt = 0, chord = 0, audio = 0;

if (args[0] == "features")
{
    foreach (var file in files)
    {
        var clock = Stopwatch.StartNew();
        var (mono, rate) = Wav.Read(file);
        load += clock.Elapsed.TotalSeconds; audio += mono.Length / (double)rate; clock.Restart();
        var low = Resample.To(mono, Features.Rate, rate);
        mel += clock.Elapsed.TotalSeconds; clock.Restart();
        var heard = Features.Of(low);
        Npy.WriteFloats(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".feat.npz"),
            ("oenv", heard.Onsets, [heard.Onsets.Length]), ("chroma", heard.Chroma, [12, heard.Chroma.Length / 12]),
            ("bass", heard.Bass, [12, heard.Bass.Length / 12]), ("rms", heard.Loudness, [1, heard.Loudness.Length]),
            ("tuning", [(float)heard.Tuning], []));
        cqt += clock.Elapsed.TotalSeconds;
    }
    Console.WriteLine($"{n} songs, {audio / n:F0} s of audio each; a song: load {load / n:F2} s, to 22050 Hz {mel / n:F2} s, "
        + $"the features {cqt / n:F2} s");
    Console.WriteLine($"in all, from the start of the process: {started.Elapsed.TotalSeconds:F2} s");
    return 0;
}

string models = Option("--models") ?? AppContext.BaseDirectory;
bool writeCqt = args.Contains("--cqt");
// With every core, Beat This! is 2.5 times slower on 24 of them (README).
var options = new SessionOptions { IntraOpNumThreads = int.Parse(Option("--threads") ?? "8") };
string? linesFrom = args[0] == "read" ? Option("--lines") : null, heardFrom = args[0] == "read" ? Option("--heard") : null;
using var beatModel = linesFrom == null ? new InferenceSession(Path.Combine(models, "beat-this-final0-dynamo.onnx"), options) : null;
using var chordModel = heardFrom == null ? new InferenceSession(Path.Combine(models, "crema-chord.onnx"), options) : null;
Console.WriteLine($"start and open the models: {started.Elapsed.TotalSeconds:F2} s");

if (args[0] == "read")
{
    int failed = 0;
    double rules = 0;
    foreach (var file in files)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        try
        {
            var clock = Stopwatch.StartNew();
            var (mono, rate) = Wav.Read(file);
            load += clock.Elapsed.TotalSeconds; audio += mono.Length / (double)rate; clock.Restart();
            var low = Resample.To(mono, Features.Rate, rate);       // the beat model's rate too
            mel += clock.Elapsed.TotalSeconds; clock.Restart();
            var heard = Features.Of(low);
            cqt += clock.Elapsed.TotalSeconds; clock.Restart();
            var (lines, beats) = Reader.LinesOf(linesFrom != null ? File.ReadAllText(Path.Combine(linesFrom, name + ".beats")) : Beats.Lines(beatModel!, low));
            beat += clock.Elapsed.TotalSeconds; clock.Restart();
            float[] logp;
            int frames;
            double step = Crema.Step;
            if (heardFrom != null) (logp, frames, step) = Npy.ReadHeard(Path.Combine(heardFrom, name + ".npz"));
            else
            {
                logp = Crema.LogP(chordModel!, Crema.Input(Resample.To(mono, Crema.Rate, rate), out frames), frames);
                for (int i = 0; i < logp.Length; i++) logp[i] = (float)(Half)logp[i];       // as `dettato ears` writes them
            }
            chord += clock.Elapsed.TotalSeconds; clock.Restart();
            var chart = Reader.Read(Path.GetFileName(file), heard, lines, beats, logp, frames, step);
            File.WriteAllText(Path.Combine(output, name + ".dettato.json"), Writer.Json(chart));
            File.WriteAllText(Path.Combine(output, name + ".lys"), Writer.Lys(chart, name));
            rules += clock.Elapsed.TotalSeconds;
        }
        catch (Exception e) when (e is InvalidDataException or IOException or InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            failed++;       // one recording that cannot be read does not stop the rest
            Console.Error.WriteLine($"{Path.GetFileName(file)}: {e.Message}");
        }
    }
    Console.WriteLine($"{n} songs, {audio / n:F0} s of audio each; a song: load {load / n:F2} s, to 22050 Hz {mel / n:F2} s, "
        + $"the features {cqt / n:F2} s, the beats {beat / n:F2} s, the chords {chord / n:F2} s, the rules {rules / n:F2} s");
    Console.WriteLine($"in all, from the start of the process: {started.Elapsed.TotalSeconds:F2} s");
    if (failed > 0) Console.Error.WriteLine($"{failed} of {n} recordings were not read");
    return failed > 0 ? 1 : 0;
}

foreach (var file in files)
{
    string stem = Path.Combine(output, Path.GetFileNameWithoutExtension(file));
    var clock = Stopwatch.StartNew();
    var (mono, rate) = Wav.Read(file);
    load += clock.Elapsed.TotalSeconds; audio += mono.Length / (double)rate; clock.Restart();

    var low = Resample.To(mono, Beats.Rate, rate);
    mel += clock.Elapsed.TotalSeconds; clock.Restart();
    File.WriteAllText(stem + ".beats", Beats.Lines(beatModel!, low));
    beat += clock.Elapsed.TotalSeconds; clock.Restart();

    var input = Crema.Input(Resample.To(mono, Crema.Rate, rate), out int frames);
    cqt += clock.Elapsed.TotalSeconds; clock.Restart();
    if (writeCqt) Npy.Write(stem + ".cqt.npy", input, frames, Crema.Bins, 2);
    Npy.WriteHeard(stem + ".npz", Crema.LogP(chordModel!, input, frames), frames, Crema.Chords, Crema.Step);
    chord += clock.Elapsed.TotalSeconds;
}
Console.WriteLine($"{n} songs, {audio / n:F0} s of audio each; a song: load {load / n:F2} s, to 22050 Hz {mel / n:F2} s, "
    + $"mel and beat model {beat / n:F2} s, CQT {cqt / n:F2} s, chord model {chord / n:F2} s");
Console.WriteLine($"in all, from the start of the process: {started.Elapsed.TotalSeconds:F2} s");
return 0;
