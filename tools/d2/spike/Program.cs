// D2: the beats of a recording in .NET: wav -> 22050 Hz -> log mel -> Beat This! (ONNX) -> peaks -> <name>.beats,
// as tools/track_beats.py writes it. A trial of what a .NET command would have to do, not the command.
//
//   Spike <a .wav or a folder of them> <beat-this.onnx> <out folder> [threads]
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

var started = Stopwatch.StartNew();
var files = Directory.Exists(args[0]) ? Directory.GetFiles(args[0], "*.wav").Order().ToArray() : [args[0]];
Directory.CreateDirectory(args[2]);
var options = new SessionOptions { IntraOpNumThreads = args.Length > 3 ? int.Parse(args[3]) : 8 };
using var session = new InferenceSession(args[1], options);
Console.WriteLine($"start and open the model: {started.Elapsed.TotalSeconds:F2} s");

double load = 0, mel = 0, model = 0, audio = 0;
foreach (var file in files)
{
    var clock = Stopwatch.StartNew();
    var (mono, rate) = Wav.Read(file);
    load += clock.Elapsed.TotalSeconds; audio += mono.Length / (double)rate; clock.Restart();
    if (rate == 44100) mono = Mel.Halve(mono);
    else if (rate != 22050) throw new NotSupportedException($"{rate} Hz: the trial halves 44100 Hz only");
    var spect = Mel.LogMel(mono, out int frames);
    mel += clock.Elapsed.TotalSeconds; clock.Restart();
    var (beat, down) = Beats.Logits(session, spect, frames);
    var beats = Beats.Peaks(beat);
    var downs = Beats.Peaks(down).Select(d => beats.Length == 0 ? d : beats.MinBy(b => Math.Abs(b - d))).Distinct().Order().ToArray();
    model += clock.Elapsed.TotalSeconds;
    var lines = new List<string>();
    int number = 0;
    foreach (var t in beats)
    {
        bool isDown = downs.Length > 0 && downs.Min(d => Math.Abs(d - t)) < 0.035;
        number = isDown ? 1 : number > 0 ? number + 1 : 0;
        lines.Add(string.Create(CultureInfo.InvariantCulture, $"{t:F3}\t{number}"));
    }
    File.WriteAllText(Path.Combine(args[2], Path.GetFileNameWithoutExtension(file) + ".beats"), string.Join("\n", lines) + "\n");
}
int n = files.Length;
Console.WriteLine($"{n} songs, {audio / n:F0} s of audio each; a song: load {load / n:F2} s, resample+mel {mel / n:F2} s, beat model {model / n:F2} s");
Console.WriteLine($"in all, from the start of the process: {started.Elapsed.TotalSeconds:F2} s");

static class Wav
{
    /// <summary>A PCM (16 or 24 bit) or float .wav, its channels averaged.</summary>
    public static (double[] Mono, int Rate) Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int at = 12, channels = 0, rate = 0, bits = 0, format = 0;
        while (at + 8 <= bytes.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(bytes, at, 4);
            int size = BitConverter.ToInt32(bytes, at + 4);
            if (id == "fmt ")
            {
                format = BitConverter.ToUInt16(bytes, at + 8);
                channels = BitConverter.ToUInt16(bytes, at + 10);
                rate = BitConverter.ToInt32(bytes, at + 12);
                bits = BitConverter.ToUInt16(bytes, at + 22);
                if (format == 0xFFFE) format = BitConverter.ToUInt16(bytes, at + 32);
            }
            else if (id == "data")
            {
                size = Math.Min(size, bytes.Length - at - 8);
                int width = bits / 8, count = size / (width * channels);
                var mono = new double[count];
                int p = at + 8;
                for (int i = 0; i < count; i++)
                {
                    double sum = 0;
                    for (int c = 0; c < channels; c++, p += width)
                        sum += (format, bits) switch
                        {
                            (1, 16) => BitConverter.ToInt16(bytes, p) / 32768.0,
                            (1, 24) => ((bytes[p] | bytes[p + 1] << 8 | (sbyte)bytes[p + 2] << 16)) / 8388608.0,
                            (3, 32) => BitConverter.ToSingle(bytes, p),
                            _ => throw new NotSupportedException($"wav format {format}, {bits} bits"),
                        };
                    mono[i] = sum / channels;
                }
                return (mono, rate);
            }
            at += 8 + size + (size & 1);
        }
        throw new InvalidDataException("no data chunk: " + path);
    }
}

static class Mel
{
    const int Fft = 1024, Hop = 441, Bands = 128, Bins = Fft / 2 + 1;
    static readonly double[] Window = [.. Enumerable.Range(0, Fft).Select(i => 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / Fft))];
    static readonly float[] Bank = MakeBank();      // [bin * Bands + band]
    static readonly double[] Half = MakeHalf();

    static double ToMel(double hz) => hz >= 1000 ? 15 + Math.Log(hz / 1000) / (Math.Log(6.4) / 27) : hz * 3 / 200;
    static double ToHz(double mel) => mel >= 15 ? 1000 * Math.Exp(Math.Log(6.4) / 27 * (mel - 15)) : mel * 200 / 3;

    static float[] MakeBank()
    {
        double low = ToMel(30), high = ToMel(11000);
        var points = Enumerable.Range(0, Bands + 2).Select(i => ToHz(low + (high - low) * i / (Bands + 1))).ToArray();
        var bank = new float[Bins * Bands];
        for (int k = 0; k < Bins; k++)
        {
            double f = 11025.0 * k / (Bins - 1);
            for (int m = 0; m < Bands; m++)
            {
                double down = (f - points[m]) / (points[m + 1] - points[m]), up = (points[m + 2] - f) / (points[m + 2] - points[m + 1]);
                bank[k * Bands + m] = (float)Math.Max(0, Math.Min(down, up));
            }
        }
        return bank;
    }

    /// <summary>A low-pass at a quarter of the rate (windowed sinc, Kaiser), for halving the rate.</summary>
    static double[] MakeHalf()
    {
        const int taps = 511; const double beta = 14.0, cutoff = 0.4875;    // of the rate, in units of half of it
        static double I0(double x) { double sum = 1, term = 1; for (int k = 1; k < 60; k++) { term *= x / (2 * k) * (x / (2 * k)); sum += term; } return sum; }
        var h = new double[taps]; int mid = taps / 2; double total = 0;
        for (int i = 0; i < taps; i++)
        {
            double x = i - mid, sinc = x == 0 ? cutoff : Math.Sin(Math.PI * cutoff * x) / (Math.PI * x);
            double r = x / mid;
            h[i] = sinc * I0(beta * Math.Sqrt(Math.Max(0, 1 - r * r))) / I0(beta);
            total += h[i];
        }
        for (int i = 0; i < taps; i++) h[i] /= total;
        return h;
    }

    public static double[] Halve(double[] x)
    {
        int mid = Half.Length / 2, count = (x.Length + 1) / 2;
        var y = new double[count];
        Parallel.For(0, count, i =>
        {
            int centre = 2 * i, from = Math.Max(0, centre - mid), to = Math.Min(x.Length - 1, centre + mid);
            double sum = 0;
            for (int j = from; j <= to; j++) sum += x[j] * Half[j - centre + mid];
            y[i] = sum;
        });
        return y;
    }

    /// <summary>Beat This!'s input: frames * 128, log(1 + 1000 * mel) of the magnitudes of centred, reflected frames.</summary>
    public static float[] LogMel(double[] signal, out int frames)
    {
        int pad = Fft / 2, n = signal.Length;
        int count = frames = 1 + n / Hop;
        var spect = new float[count * Bands];
        Parallel.For(0, count, () => (new Complex[Fft], new float[Bins]), (t, _, buffers) =>
        {
            var (z, mag) = buffers;
            for (int i = 0; i < Fft; i++)
            {
                int j = t * Hop + i - pad;
                if (j < 0) j = -j; else if (j >= n) j = 2 * (n - 1) - j;
                z[i] = new Complex((float)signal[j] * Window[i], 0);
            }
            Transform(z);
            for (int k = 0; k < Bins; k++) mag[k] = (float)(z[k].Magnitude / 32.0);
            for (int m = 0; m < Bands; m++)
            {
                float sum = 0;
                for (int k = 0; k < Bins; k++) sum += mag[k] * Bank[k * Bands + m];
                spect[t * Bands + m] = MathF.Log(1 + 1000 * sum);
            }
            return buffers;
        }, _ => { });
        return spect;
    }

    static void Transform(Complex[] a)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / len);
            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (int k = 0; k < len / 2; k++, w *= step)
                {
                    Complex u = a[i + k], v = a[i + k + len / 2] * w;
                    a[i + k] = u + v; a[i + k + len / 2] = u - v;
                }
            }
        }
    }
}

static class Beats
{
    const int Size = 1500, Border = 6, Bands = 128;

    /// <summary>beat_this.inference.split_predict_aggregate, keep_first: 30 s chunks, 6 frames of each edge dropped.</summary>
    public static (float[] Beat, float[] Down) Logits(InferenceSession session, float[] spect, int frames)
    {
        var starts = new List<int>();
        for (int s = -Border; s < frames - Border; s += Size - 2 * Border) starts.Add(s);
        if (frames > Size - 2 * Border) starts[^1] = frames - (Size - Border);
        var beat = Enumerable.Repeat(-1000f, frames).ToArray();
        var down = Enumerable.Repeat(-1000f, frames).ToArray();
        foreach (int start in Enumerable.Reverse(starts))
        {
            int from = Math.Max(start, 0), to = Math.Min(start + Size, frames);
            int left = Math.Max(0, -start), right = Math.Max(0, Math.Min(Border, start + Size - frames));
            int length = left + (to - from) + right;
            var chunk = new DenseTensor<float>(new[] { 1, length, Bands });
            spect.AsSpan(from * Bands, (to - from) * Bands).CopyTo(chunk.Buffer.Span[(left * Bands)..]);
            using var results = session.Run([NamedOnnxValue.CreateFromTensor("spect", chunk)]);
            var b = results.First(r => r.Name == "beat").AsEnumerable<float>().ToArray();
            var d = results.First(r => r.Name == "downbeat").AsEnumerable<float>().ToArray();
            for (int i = Border; i < length - Border; i++)
            {
                int at = start + i;
                if (at < 0 || at >= frames) continue;
                beat[at] = b[i]; down[at] = d[i];
            }
        }
        return (beat, down);
    }

    /// <summary>The largest within 3 frames either side and above 0; neighbouring peaks averaged; in seconds (50 frames a second).</summary>
    public static double[] Peaks(float[] logit)
    {
        var found = new List<int>();
        for (int i = 0; i < logit.Length; i++)
        {
            if (logit[i] <= 0) continue;
            bool top = true;
            for (int j = Math.Max(0, i - 3); j <= Math.Min(logit.Length - 1, i + 3) && top; j++) top = logit[j] <= logit[i];
            if (top) found.Add(i);
        }
        var result = new List<double>();
        if (found.Count == 0) return [];
        double p = found[0]; int c = 1;
        foreach (int q in found.Skip(1))
        {
            if (q - p <= 1) { c++; p += (q - p) / c; }
            else { result.Add(p); p = q; c = 1; }
        }
        result.Add(p);
        return [.. result.Select(f => f / 50.0)];
    }
}
