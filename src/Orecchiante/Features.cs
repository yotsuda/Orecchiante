namespace Orecchiante;

/// <summary>What the rules read beside the two models, as tools/read_chords.py's features() takes it from librosa 1.0
/// with nothing taken out of the recording (--ears): the onset strength, the loudness, and two chromas (five octaves
/// from C2, and the two from C1 for the bass), a value every 512 samples at 22050 Hz. One short-time spectrum serves
/// the onset strength and the tuning. tools/net_check.py --feats compares them with librosa's.</summary>
static class Features
{
    public const int Rate = 22050, Hop = 512, Size = 2048, Mels = 128, PerOctave = 36;
    static readonly double C1 = 440 * Math.Pow(2, (24 - 69) / 12.0), C2 = 2 * C1;

    /// <summary>The chromas are [pitch class * frames + frame], frames a twelfth of their length, the loudest pitch
    /// class of a frame at 1. A chroma may have a frame more than the rest, as librosa's has: its recording is
    /// brought down in rate first (to a 32nd for the bass), the length rounded up.</summary>
    /// <param name="Cents">How far the recording is from A = 440 Hz, in cents (-50 .. 49): the same pitches as
    /// <paramref name="Tuning"/>'s, placed among the semitones.</param>
    /// <param name="BassPart">Whether <paramref name="Bass"/> is the separated bass part's (BassOf), not the recording's.</param>
    public sealed record Heard(float[] Onsets, float[] Chroma, float[] Bass, float[] Loudness, double Tuning, double Cents, bool BassPart = false);

    /// <summary>The bass chroma of the separated bass part (at 22050 Hz), as librosa.feature.chroma_cqt of it from C1
    /// over two octaves, its tuning its own (0.12.0: the chords' bass notes are heard in it; `orecchiante bass`).</summary>
    public static float[] BassOf(double[] y)
    {
        var spectra = Spectra(y, out _);
        return Chroma(y, C1, 2, Tuning(spectra).Bins);
    }

    /// <summary><paramref name="f"/> with the bass chroma of the bass part (BassOf), as long as the recording's.</summary>
    public static Heard WithBassPart(Heard f, float[] part)
    {
        int frames = f.Bass.Length / 12, partFrames = part.Length / 12;
        var bass = new float[f.Bass.Length];
        for (int c = 0; c < 12; c++)
            Array.Copy(part, c * partFrames, bass, c * frames, Math.Min(frames, partFrames));
        return f with { Bass = bass, BassPart = true };
    }

    /// <param name="y">The recording at 22050 Hz.</param>
    public static Heard Of(double[] y)
    {
        var spectra = Spectra(y, out var loudness);
        var (tuning, semitones) = Tuning(spectra);
        return new Heard(Onsets(spectra), Chroma(y, C2, 5, tuning), Chroma(y, C1, 2, tuning), loudness, tuning, Math.Round(100 * semitones));
    }

    /// <summary>librosa.stft with its defaults (frames of 2048 under a Hann window, centred every 512 samples, zeros
    /// beyond the ends), magnitudes [frame][bin]; and librosa.feature.rms of the same frames.</summary>
    /// <param name="hop">Samples between frames (the bass reader's onsets: 256).</param>
    internal static float[][] Spectra(double[] y, out float[] loudness, int hop = Hop)
    {
        int n = y.Length, frames = 1 + n / hop, half = Size / 2;
        var window = new double[Size];
        for (int i = 0; i < Size; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / Size);
        var fft = new Fft(Size);
        var spectra = new float[frames][];
        var rms = loudness = new float[frames];
        Parallel.For(0, frames, () => (new double[Size], new double[Size]), (t, _, buffers) =>
        {
            var (re, im) = buffers;
            int first = t * hop - half;
            double power = 0;
            for (int i = 0; i < Size; i++)
            {
                int j = first + i;
                double v = j >= 0 && j < n ? y[j] : 0;
                power += v * v;
                re[i] = v * window[i];
                im[i] = 0;
            }
            rms[t] = (float)Math.Sqrt(power / Size);
            fft.Forward(re, im);
            var row = spectra[t] = new float[half + 1];
            for (int k = 0; k <= half; k++) row[k] = (float)Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
            return buffers;
        }, _ => { });
        return spectra;
    }

    /// <summary>librosa.onset.onset_strength: the power in 128 mel bands, in dB no lower than 80 under the loudest;
    /// how much each band rose since the frame before, averaged over the bands; three frames later (one for the
    /// difference, two for the half frame a centred spectrum looks ahead: at a hop of 256, four).</summary>
    /// <param name="hop">The spectra's hop; <paramref name="mels"/> bands from 0 Hz to <paramref name="fmax"/> (the bass
    /// reader: 64 to 2000 Hz).</param>
    internal static float[] Onsets(float[][] spectra, int hop = Hop, int mels = Mels, double fmax = Rate / 2.0)
    {
        int frames = spectra.Length;
        var bands = MelBands(mels, fmax);
        var db = new float[frames][];
        Parallel.For(0, frames, t =>
        {
            var row = spectra[t];
            var mine = db[t] = new float[mels];
            for (int m = 0; m < mels; m++)
            {
                var (from, weights) = bands[m];
                double sum = 0;
                for (int i = 0; i < weights.Length; i++) sum += weights[i] * ((double)row[from + i] * row[from + i]);
                mine[m] = (float)(10 * Math.Log10(Math.Max(1e-10, sum)));
            }
        });
        float floor = db.Max(row => row.Max()) - 80;
        var onsets = new float[frames];
        int late = Size / (2 * hop) + 1;
        for (int t = late; t < frames; t++)
        {
            float[] now = db[t - late + 1], before = db[t - late];
            double rise = 0;
            for (int m = 0; m < mels; m++) rise += Math.Max(0, Math.Max(now[m], floor) - Math.Max(before[m], floor));
            onsets[t] = (float)(rise / mels);
        }
        return onsets;
    }

    /// <summary>librosa.filters.mel (Slaney's scale and areas, 0 Hz to <paramref name="fmax"/>): each band's first bin and weights.</summary>
    static (int From, double[] Weights)[] MelBands(int mels, double fmax)
    {
        const double step = 200.0 / 3, bend = 1000, bendMel = bend / step;
        double log = Math.Log(6.4) / 27;
        double top = fmax >= bend ? bendMel + Math.Log(fmax / bend) / log : fmax / step;
        var hz = new double[mels + 2];
        for (int i = 0; i < hz.Length; i++)
        {
            double mel = top * i / (mels + 1);
            hz[i] = mel >= bendMel ? bend * Math.Exp(log * (mel - bendMel)) : step * mel;
        }
        var bands = new (int, double[])[mels];
        for (int m = 0; m < mels; m++)
        {
            var weights = new List<double>();
            int from = -1;
            for (int k = 0; k <= Size / 2; k++)
            {
                double f = (double)k * Rate / Size;
                double w = Math.Min((f - hz[m]) / (hz[m + 1] - hz[m]), (hz[m + 2] - f) / (hz[m + 2] - hz[m + 1]));
                if (w <= 0) { if (from >= 0) break; continue; }
                if (from < 0) from = k;
                weights.Add((float)(w * 2 / (hz[m + 2] - hz[m])));
            }
            bands[m] = (Math.Max(from, 0), [.. weights]);
        }
        return bands;
    }

    /// <summary>librosa.estimate_tuning for 36 bins to the octave: how far the recording's pitches are from the bins
    /// of A = 440 Hz, in bins (-0.5 .. 0.49, by hundredths). The pitches are the peaks of each spectrum between
    /// 150 and 4000 Hz over a tenth of its highest (piptrack), each placed by the parabola through its neighbours;
    /// of the louder half of them, the commonest distance from a bin (pitch_tuning). In single precision where
    /// librosa is, so that a pitch falls on the same side of a hundredth. And the same of 12 bins to the octave: in
    /// semitones (-0.5 .. 0.49), what a tape run fast or slow is off by (36 bins cannot tell 20 cents sharp from 13 flat).</summary>
    static (double Bins, double Semitones) Tuning(float[][] spectra)
    {
        const double lowest = 150, highest = 4000;
        double binHz = (double)Rate / Size;
        var found = new List<(float Pitch, float Mag)>[spectra.Length];
        Parallel.For(0, spectra.Length, t =>
        {
            var s = spectra[t];
            var mine = found[t] = [];
            float over = 0.1f * s.Max();
            for (int k = 1; k < s.Length - 1; k++)
            {
                double f = k * binHz;
                if (f < lowest || !(f < highest) || !(s[k] > s[k - 1] && s[k] >= s[k + 1] && s[k] > over)) continue;
                float a = s[k + 1] + s[k - 1] - 2f * s[k], b = 0.5f * (s[k + 1] - s[k - 1]);
                float shift = MathF.Abs(b) < MathF.Abs(a) ? -b / a : 0;
                mine.Add(((float)((k + (double)shift) * binHz), s[k] + 0.5f * b * shift));
            }
        });
        var peaks = found.SelectMany(list => list).ToArray();
        if (peaks.Length == 0) return (0, 0);
        var mags = peaks.Select(p => p.Mag).Order().ToArray();
        float median = mags.Length % 2 == 1 ? mags[mags.Length / 2] : (mags[mags.Length / 2 - 1] + mags[mags.Length / 2]) / 2;
        var loud = peaks.Where(p => p.Mag >= median).Select(p => p.Pitch).ToArray();
        return (Commonest(loud, PerOctave), Commonest(loud, 12));
    }

    /// <summary>librosa.pitch_tuning: of the pitches, the commonest distance from a bin, by hundredths of a bin.</summary>
    static double Commonest(float[] pitches, int perOctave)
    {
        const int hundredths = 100;
        var edges = new double[hundredths + 1];
        for (int i = 0; i < hundredths; i++) edges[i] = i * (1.0 / hundredths) + -0.5;
        edges[hundredths] = 0.5;
        var counts = new int[hundredths];
        foreach (float pitch in pitches)
        {
            float bins = perOctave * MathF.Log2(pitch / (440f / 16));
            float off = bins - MathF.Floor(bins);
            if (off >= 0.5f) off -= 1f;
            int i = Math.Clamp((int)((off + 0.5) * hundredths), 0, hundredths - 1);
            while (i > 0 && off < edges[i]) i--;
            while (i < hundredths - 1 && off >= edges[i + 1]) i++;
            counts[i]++;
        }
        return edges[Array.IndexOf(counts, counts.Max())];
    }

    /// <summary>librosa.feature.chroma_cqt: the constant-Q magnitudes of so many octaves from <paramref name="lowest"/>,
    /// 36 filters to the octave moved by the tuning; the three about each semitone and all its octaves added;
    /// each frame over its largest.</summary>
    static float[] Chroma(double[] y, double lowest, int octaves, double tuning)
    {
        var mag = Cqt.Magnitude(y, Rate, Hop, lowest * Math.Pow(2, tuning / PerOctave), octaves * PerOctave, PerOctave);
        int frames = mag[0].Length;
        var chroma = new float[12 * frames];
        int third = PerOctave / 12;
        Parallel.For(0, frames, t =>
        {
            Span<double> sums = stackalloc double[12];
            for (int k = 0; k < mag.Length; k++) sums[(k + third / 2) / third % 12] += mag[k][t];
            double most = 0;
            foreach (double v in sums) most = Math.Max(most, v);
            if (most < 1.17549435e-38) most = 1;        // numpy's float32 tiny: a silent frame stays at zero
            for (int c = 0; c < 12; c++) chroma[c * frames + t] = (float)(sums[c] / most);
        });
        return chroma;
    }
}
