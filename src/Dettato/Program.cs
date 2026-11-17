// dettato: a recording, read into a chart (docs/ROADMAP.md, D2: the reader moved here from tools/read_chords.py;
// D3: the command a user and Lily# call).
//
//   dettato <recording> [-o <x.lys>] [--force] [--models <folder>] [--threads 8]
//
// writes <name>.lys (a Lily# chord chart) beside the recording, or where -o says, and beside the chart
// <name>.dettato.json (docs/SIDECAR.md), and the recording's six parts in <name>.parts/ (drums, bass, other,
// vocals, guitar and piano .flac, to hear one at a time; SIDECAR.md, `parts`). A chart already there is not written over without --force: it may hold
// corrections. Both files are written at the end, whole or not at all. stderr says what is being done, a line a
// step, and on failure why in its last line; stdout has the chart's path alone. Exit codes: 0 written, 1 the
// recording was not read, 2 the arguments (no such file, a kind not read, a chart in the way), 3 the reader itself.
//
//   dettato --version [--json]
//
// the version, and with --json also the protocol (the arguments, the exit codes and .dettato.json: raised when
// they change incompatibly), the commands and the kinds of recording read (Audio.Inputs).
//
// And for measuring, on one recording or a folder of them, each part to a file:
//
//   dettato read <a recording or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--lines <folder>] [--heard <folder>] [--voice <folder>|none]
//
// the same reading into a folder, written over whatever is there. --lines and --heard take the beats and the
// chords' probabilities from folders of <name>.beats and <name>.npz, as the two commands below and the tools in
// Python write them, in place of running the models (to compare with read_chords.py: tools/read_check.py); --voice
// the vocals' loudness from <name>.voc.npy (none: no singing heard, as read_chords.py without --voice).
//
//   dettato ears <a recording or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--cqt]
//
// writes <name>.beats (as tools/track_beats.py does), <name>.npz (as tools/crema_chords.py does) and <name>.voc.npy
// (the vocals' loudness every 10 ms, as scratch/sep/song.py does with Demucs in torch);
// with --cqt, also <name>.cqt.npy, what the chord model was given. The models are beat-this-final0-dynamo.onnx
// crema-chord.onnx, btc-large-voca.onnx and htdemucs_6s-mm8.onnx (README: how they are made), beside the program unless --models says where.
//
//   dettato features <a recording or a folder of them> --out <folder>
//
// writes <name>.feat.npz: oenv, chroma, bass and rms as read_chords.py's features() makes them for --ears,
// and tuning (tools/measure.py --net reads them in place of its own).
//
//   dettato audio <a recording or a folder of them> --out <folder>
//
// writes <file's name>.heard.wav (song.mp3.heard.wav): the samples as the reader takes them from the file (one channel, 32-bit float, the
// recording's rate), to compare what two kinds of file of one recording give.
using System.Diagnostics;
using System.Text;
using Dettato;
using Microsoft.ML.OnnxRuntime;

const string version = "0.8.6";
const int protocol = 1;
const string usage = "usage: dettato <recording> [-o <x.lys>] [--force] [--models <folder>] [--threads 8]\n"
                   + "       dettato --version [--json]\n"
                   + "       dettato read <a recording or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--lines <folder>] [--heard <folder>] [--voice <folder>|none]\n"
                   + "       dettato ears <a recording or a folder of them> --out <folder> [--models <folder>] [--threads 8] [--cqt]\n"
                   + "       dettato features <a recording or a folder of them> --out <folder>\n"
                   + "       dettato audio <a recording or a folder of them> --out <folder>";
Console.OutputEncoding = new UTF8Encoding(false);
if (args.Length > 0 && args[0] == "--version")
{
    static string List(string[] items) => "[" + string.Join(",", items.Select(i => $"\"{i}\"")) + "]";
    Console.WriteLine(args.Contains("--json")
        ? $"{{\"version\":\"{version}\",\"protocol\":{protocol},\"commands\":{List(["read"])},\"inputs\":{List(Audio.Inputs)}}}"
        : version);
    return 0;
}
if (args.Length == 0 || args[0] is "-h" or "--help" or "-?" or "/?")
{
    Console.Error.WriteLine(usage);
    return args.Length == 0 ? 2 : 0;
}

var started = Stopwatch.StartNew();
double load = 0, mel = 0, beat = 0, cqt = 0, chord = 0, sing = 0, audio = 0, rules = 0;
InferenceSession? beatModel = null, chordModel = null, btcModel = null;
InferenceSession[] voiceModels = [];
string? linesFrom = null, heardFrom = null, voiceFrom = null;     // voiceFrom "none": no singing heard

int Fail(int code, string why) { Console.Error.WriteLine("dettato: " + why); return code; }

void Open(string models, int threads, bool beats, bool chords, bool voice)
{
    // With every core, Beat This! is 2.5 times slower on 24 of them (README).
    var options = new SessionOptions { IntraOpNumThreads = threads };
    if (beats) beatModel = new InferenceSession(Path.Combine(models, "beat-this-final0-dynamo.onnx"), options);
    if (chords) chordModel = new InferenceSession(Path.Combine(models, "crema-chord.onnx"), options);
    if (chords) btcModel = new InferenceSession(Path.Combine(models, "btc-large-voca.onnx"), options);    // the second ear
    // (one session for the segments run at once; without its arena and memory pattern a run holds half the memory:
    // a song of 3.5 min peaked at 8.4 GB with 3 at once, 16.4 GB with them, for 5 s more; the vocals the same)
    if (voice) voiceModels = [new InferenceSession(Path.Combine(models, Voice.Model), new SessionOptions { IntraOpNumThreads = threads, EnableCpuMemArena = false, EnableMemoryPattern = false })];
}

// The chords as the two ears hear them together (Btc.Average): crema at 44100 Hz, BTC at 22050 Hz.
float[] Heard(double[] high, double[] low, out int frames)
{
    var input = Crema.Input(high, out frames);
    var first = Crema.LogP(chordModel!, input, frames);
    input = null!;
    var second = Btc.LogP(btcModel!, Btc.Input(low, out int secondFrames), secondFrames);
    return Btc.Average(first, frames, Crema.Step, second, secondFrames, Btc.Step);
}

// One recording to its chart; `say` is told each step as it begins; with `partsTo`, the separated parts are written
// there too (Voice.Loudness: <part>.wav).
Chart Read(string file, Action<string>? say = null, string? partsTo = null)
{
    string name = Path.GetFileNameWithoutExtension(file);
    var clock = Stopwatch.StartNew();
    say?.Invoke($"listening to {Path.GetFileName(file)}");
    var (mono, rate) = Audio.Read(file);
    load += clock.Elapsed.TotalSeconds; audio += mono.Length / (double)rate; clock.Restart();
    int seconds = (int)(mono.Length / (double)rate);
    say?.Invoke($"  {seconds / 60}:{seconds % 60:D2} at {rate} Hz");
    if (mono.Length < rate * 5L) throw new InvalidDataException("shorter than 5 seconds");
    // (an hour's recording is 1.3 GB at its own rate: each array is let go once read, and collected
    // before the next is made)
    var low = Resample.To(mono, Features.Rate, rate);       // the beat model's rate too
    double[]? high = heardFrom == null || voiceFrom == null ? Resample.To(mono, Crema.Rate, rate) : null;
    mono = null!;
    Collect();
    mel += clock.Elapsed.TotalSeconds; clock.Restart();
    // The singing is most of the time: it is separated while the beats and the chords are read (the same vocals).
    var singing = voiceFrom == null ? Task.Run(() => Voice.Loudness(voiceModels, high!, partsTo)) : null;
    var heard = Features.Of(low);
    cqt += clock.Elapsed.TotalSeconds; clock.Restart();
    say?.Invoke("the beats");
    string tracked = linesFrom != null ? File.ReadAllText(Path.Combine(linesFrom, name + ".beats")) : Beats.Lines(beatModel!, low);
    beat += clock.Elapsed.TotalSeconds; clock.Restart();
    say?.Invoke("the chords");
    float[] logp;
    int frames;
    double step = Crema.Step;
    if (heardFrom != null) (logp, frames, step) = Npy.ReadHeard(Path.Combine(heardFrom, name + ".npz"));
    else logp = Heard(high!, low, out frames);       // in half precision, as `dettato ears` writes them
    chord += clock.Elapsed.TotalSeconds; clock.Restart();
    say?.Invoke("the singing");
    float[]? voice = voiceFrom == "none" ? null
        : voiceFrom != null ? (File.Exists(Path.Combine(voiceFrom, name + ".voc.npy")) ? Npy.ReadFloats(Path.Combine(voiceFrom, name + ".voc.npy")) : null)
        : singing!.GetAwaiter().GetResult();
    high = null;
    low = null!;
    Collect();
    sing += clock.Elapsed.TotalSeconds; clock.Restart();
    var (lines, beats) = Reader.LinesOf(tracked, logp, frames, step);    // the halves of bars are paired as the key heard says
    var chart = Reader.Read(Path.GetFileName(file), heard, lines, beats, logp, frames, step, voice);
    rules += clock.Elapsed.TotalSeconds;
    return chart;
}

// What a recording that cannot be read throws (the rules' own complaints are InvalidDataException).
static bool Unread(Exception e) => e is InvalidDataException or IOException or UnauthorizedAccessException;

if (args[0] is not ("read" or "ears" or "features" or "audio"))
{
    // The user's way in, and Lily#'s.
    string? recording = null, chartPath = null, modelsAt = null;
    bool force = false;
    int threads = 8;
    for (int i = 0; i < args.Length; i++)
    {
        string a = args[i];
        if (a is "-o" or "--models" or "--threads")
        {
            if (++i >= args.Length) return Fail(2, $"{a} wants a value\n{usage}");
            if (a == "-o") chartPath = args[i];
            else if (a == "--models") modelsAt = args[i];
            else if (!int.TryParse(args[i], out threads) || threads < 1) return Fail(2, $"--threads wants a number, not {args[i]}");
        }
        else if (a == "--force") force = true;
        else if (a.StartsWith('-')) return Fail(2, $"unknown option {a}\n{usage}");
        else if (recording != null) return Fail(2, $"one recording at a time: {recording}, {a}");
        else recording = a;
    }
    if (recording == null) return Fail(2, "no recording to read\n" + usage);
    if (!File.Exists(recording)) return Fail(2, $"no file {recording}");
    if (!Audio.Reads(recording)) return Fail(2, $"{Path.GetFileName(recording)}: not a kind of recording this reads ({string.Join(", ", Audio.Inputs)})");
    recording = Path.GetFullPath(recording);
    chartPath = Path.GetFullPath(chartPath ?? Path.ChangeExtension(recording, ".lys"));
    if (!chartPath.EndsWith(".lys", StringComparison.OrdinalIgnoreCase)) return Fail(2, $"-o wants a .lys file, not {chartPath}");
    string sidePath = chartPath[..^4] + ".dettato.json";
    // The parts, to hear one at a time (SIDECAR.md, `parts`): a folder beside the chart, made under another name first.
    string partsPath = chartPath[..^4] + ".parts", partsMade = partsPath + ".part";
    if (File.Exists(chartPath) && !force) return Fail(2, $"{chartPath} is already there (--force to write over it)");
    void Unmake() { try { if (Directory.Exists(partsMade)) Directory.Delete(partsMade, true); } catch (Exception x) when (Unread(x)) { } }

    string lys, json;
    try
    {
        Unmake();
        Directory.CreateDirectory(partsMade);
        modelsAt ??= AppContext.BaseDirectory;
        foreach (var model in new[] { "beat-this-final0-dynamo.onnx", "crema-chord.onnx", "btc-large-voca.onnx", Voice.Model })
            if (!File.Exists(Path.Combine(modelsAt, model))) return Fail(3, $"the model {model} is not in {Path.GetFullPath(modelsAt)} (--models <folder>)");
        try { Open(modelsAt, threads, true, true, true); }
        catch (Exception e) { return Fail(3, $"the models did not open ({e.Message.Trim()})"); }
        var chart = Read(recording, Console.Error.WriteLine, partsMade);
        // The recording as the chart finds it: its file name beside the chart, else the way there from the chart's
        // folder ('/' between folders; the whole path when they are on different drives). SIDECAR.md, `audio`.
        string chartFolder = Path.GetDirectoryName(chartPath)!;
        chart.Audio = Path.GetRelativePath(chartFolder, Path.GetFullPath(recording)).Replace('\\', '/');
        Console.Error.WriteLine("the parts");
        Parallel.ForEach(Voice.Parts, part =>       // a second each, one after another
        {
            Audio.Flac(Path.Combine(partsMade, part + ".wav"), Path.Combine(partsMade, part + ".flac"));
            File.Delete(Path.Combine(partsMade, part + ".wav"));
        });
        chart.Parts = Voice.Parts.Select(p => (p, Path.GetRelativePath(chartFolder, Path.Combine(partsPath, p + ".flac")).Replace('\\', '/'))).ToList();
        lys = Writer.Lys(chart, Path.GetFileNameWithoutExtension(recording), Lysc.Shape);
        json = Writer.Json(chart);
        int marks = lys.Split("@or(").Length - 1;
        Console.Error.WriteLine($"  {chart.Bars.Count} bars in {chart.Time} at {chart.BpmText}, {chart.Key}; {marks} {(marks == 1 ? "chord" : "chords")} with @or");
    }
    catch (Exception e) when (Unread(e))
    {
        Unmake();
        return Fail(1, $"{Path.GetFileName(recording)} was not read: {e.Message}");
    }
    catch (Exception e)
    {
        Unmake();
        Console.Error.WriteLine(e);
        return Fail(3, $"{Path.GetFileName(recording)} was not read: the reader failed ({e.GetType().Name})");
    }
    try
    {
        // Whole or not at all: each under another name first, the parts first and the chart last (a chart there has
        // its side file and its parts).
        Directory.CreateDirectory(Path.GetDirectoryName(chartPath)!);
        File.WriteAllText(sidePath + ".part", json);
        File.WriteAllText(chartPath + ".part", lys);
        if (Directory.Exists(partsPath)) Directory.Delete(partsPath, true);
        Directory.Move(partsMade, partsPath);
        File.Move(sidePath + ".part", sidePath, true);
        File.Move(chartPath + ".part", chartPath, true);
    }
    catch (Exception e) when (Unread(e))
    {
        foreach (var part in new[] { sidePath + ".part", chartPath + ".part" })
            try { File.Delete(part); } catch (Exception x) when (Unread(x)) { }
        Unmake();
        return Fail(1, $"{chartPath} was not written: {e.Message}");
    }
    Console.Error.WriteLine($"written in {started.Elapsed.TotalSeconds:F1} s");
    Console.WriteLine(chartPath);
    return 0;
}

// For measuring.
string? Option(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
if (args.Length < 2) return Fail(2, usage);
string? output = Option("--out");
if (output == null) return Fail(2, "--out <folder>\n" + usage);
if (!Directory.Exists(args[1]) && !File.Exists(args[1])) return Fail(2, $"no file or folder {args[1]}");
var files = Directory.Exists(args[1]) ? Directory.GetFiles(args[1]).Where(Audio.Reads).Order().ToArray() : [args[1]];
Directory.CreateDirectory(output);
int n = files.Length;

if (args[0] == "audio")
{
    foreach (var file in files)
    {
        var (mono, rate) = Audio.Read(file);
        Wav.Write(Path.Combine(output, Path.GetFileName(file) + ".heard.wav"), mono, rate);
        Console.WriteLine($"{Path.GetFileName(file)}: {mono.Length} samples at {rate} Hz");
    }
    return 0;
}

if (args[0] == "features")
{
    foreach (var file in files)
    {
        var clock = Stopwatch.StartNew();
        var (mono, rate) = Audio.Read(file);
        load += clock.Elapsed.TotalSeconds; audio += mono.Length / (double)rate; clock.Restart();
        var low = Resample.To(mono, Features.Rate, rate);
        mel += clock.Elapsed.TotalSeconds; clock.Restart();
        var heard = Features.Of(low);
        Npy.WriteFloats(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".feat.npz"),
            ("oenv", heard.Onsets, [heard.Onsets.Length]), ("chroma", heard.Chroma, [12, heard.Chroma.Length / 12]),
            ("bass", heard.Bass, [12, heard.Bass.Length / 12]), ("rms", heard.Loudness, [1, heard.Loudness.Length]),
            ("tuning", [(float)heard.Tuning], []), ("cents", [(float)heard.Cents], []));
        cqt += clock.Elapsed.TotalSeconds;
    }
    Console.WriteLine($"{n} songs, {audio / n:F0} s of audio each; a song: load {load / n:F2} s, to 22050 Hz {mel / n:F2} s, "
        + $"the features {cqt / n:F2} s");
    Console.WriteLine($"in all, from the start of the process: {started.Elapsed.TotalSeconds:F2} s");
    return 0;
}

if (args[0] == "read") { linesFrom = Option("--lines"); heardFrom = Option("--heard"); voiceFrom = Option("--voice"); }
Open(Option("--models") ?? AppContext.BaseDirectory, int.Parse(Option("--threads") ?? "8"), linesFrom == null, heardFrom == null, voiceFrom == null);
Console.WriteLine($"start and open the models: {started.Elapsed.TotalSeconds:F2} s");

if (args[0] == "read")
{
    int failed = 0;
    foreach (var file in files)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        try
        {
            var chart = Read(file);
            var clock = Stopwatch.StartNew();
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
        + $"the features {cqt / n:F2} s, the beats {beat / n:F2} s, the chords {chord / n:F2} s, the singing {sing / n:F2} s, the rules {rules / n:F2} s");
    Console.WriteLine($"in all, from the start of the process: {started.Elapsed.TotalSeconds:F2} s");
    if (failed > 0) Console.Error.WriteLine($"{failed} of {n} recordings were not read");
    return failed > 0 ? 1 : 0;
}

foreach (var file in files)
{
    string stem = Path.Combine(output, Path.GetFileNameWithoutExtension(file));
    var clock = Stopwatch.StartNew();
    var (mono, rate) = Audio.Read(file);
    load += clock.Elapsed.TotalSeconds; audio += mono.Length / (double)rate; clock.Restart();

    var low = Resample.To(mono, Beats.Rate, rate);
    mel += clock.Elapsed.TotalSeconds; clock.Restart();
    File.WriteAllText(stem + ".beats", Beats.Lines(beatModel!, low));
    beat += clock.Elapsed.TotalSeconds; clock.Restart();

    var high = Resample.To(mono, Crema.Rate, rate);
    if (writeCqt())
    {
        var input = Crema.Input(high, out int inputFrames);
        Npy.Write(stem + ".cqt.npy", input, inputFrames, Crema.Bins, 2);
    }
    cqt += clock.Elapsed.TotalSeconds; clock.Restart();
    Npy.WriteHeard(stem + ".npz", Heard(high, low, out int frames), frames, Crema.Chords, Crema.Step);     // the two ears together
    chord += clock.Elapsed.TotalSeconds; clock.Restart();
    var voice = Voice.Loudness(voiceModels, high);
    Npy.Write(stem + ".voc.npy", voice, voice.Length);
    sing += clock.Elapsed.TotalSeconds;
}
Console.WriteLine($"{n} songs, {audio / n:F0} s of audio each; a song: load {load / n:F2} s, to 22050 Hz {mel / n:F2} s, "
    + $"mel and beat model {beat / n:F2} s, CQT {cqt / n:F2} s, chord model {chord / n:F2} s, the singing {sing / n:F2} s");
Console.WriteLine($"in all, from the start of the process: {started.Elapsed.TotalSeconds:F2} s");
return 0;

bool writeCqt() => args.Contains("--cqt");

static void Collect() => GC.Collect();
