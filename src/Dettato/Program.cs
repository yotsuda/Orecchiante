// dettato: a recording, read into a chart. So far the ears only (docs/ROADMAP.md, D2: the reader is being
// moved here from tools/read_chords.py):
//
//   dettato ears <a .wav or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--cqt]
//
// writes <name>.beats (as tools/track_beats.py does) and <name>.npz (as tools/crema_chords.py does);
// with --cqt, also <name>.cqt.npy, what the chord model was given. The models are beat-this-final0-dynamo.onnx
// and crema-chord.onnx (README: how they are made), beside the program unless --models says where.
using System.Diagnostics;
using Dettato;
using Microsoft.ML.OnnxRuntime;

if (args.Length < 2 || args[0] != "ears")
{
    Console.Error.WriteLine("usage: dettato ears <a .wav or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--cqt]");
    return 2;
}
string? Option(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
string output = Option("--out") ?? throw new ArgumentException("--out <folder>");
string models = Option("--models") ?? AppContext.BaseDirectory;
bool writeCqt = args.Contains("--cqt");

var started = Stopwatch.StartNew();
var files = Directory.Exists(args[1]) ? Directory.GetFiles(args[1], "*.wav").Order().ToArray() : [args[1]];
Directory.CreateDirectory(output);
// With every core, Beat This! is 2.5 times slower on 24 of them (README).
var options = new SessionOptions { IntraOpNumThreads = int.Parse(Option("--threads") ?? "8") };
using var beatModel = new InferenceSession(Path.Combine(models, "beat-this-final0-dynamo.onnx"), options);
using var chordModel = new InferenceSession(Path.Combine(models, "crema-chord.onnx"), options);
Console.WriteLine($"start and open the two models: {started.Elapsed.TotalSeconds:F2} s");

double load = 0, mel = 0, beat = 0, cqt = 0, chord = 0, audio = 0;
foreach (var file in files)
{
    string stem = Path.Combine(output, Path.GetFileNameWithoutExtension(file));
    var clock = Stopwatch.StartNew();
    var (mono, rate) = Wav.Read(file);
    load += clock.Elapsed.TotalSeconds; audio += mono.Length / (double)rate; clock.Restart();

    var low = Resample.To(mono, Beats.Rate, rate);
    mel += clock.Elapsed.TotalSeconds; clock.Restart();
    File.WriteAllText(stem + ".beats", Beats.Lines(beatModel, low));
    beat += clock.Elapsed.TotalSeconds; clock.Restart();

    var input = Crema.Input(Resample.To(mono, Crema.Rate, rate), out int frames);
    cqt += clock.Elapsed.TotalSeconds; clock.Restart();
    if (writeCqt) Npy.Write(stem + ".cqt.npy", input, frames, Crema.Bins, 2);
    Npy.WriteHeard(stem + ".npz", Crema.LogP(chordModel, input, frames), frames, Crema.Chords, Crema.Step);
    chord += clock.Elapsed.TotalSeconds;
}
int n = files.Length;
Console.WriteLine($"{n} songs, {audio / n:F0} s of audio each; a song: load {load / n:F2} s, to 22050 Hz {mel / n:F2} s, "
    + $"mel and beat model {beat / n:F2} s, CQT {cqt / n:F2} s, chord model {chord / n:F2} s");
Console.WriteLine($"in all, from the start of the process: {started.Elapsed.TotalSeconds:F2} s");
return 0;
