using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.Wave;

namespace Dettato;

/// <summary>How loud the singing is, every 10 ms: the vocals Demucs (htdemucs_6s, MIT) separates from the recording;
/// and, for the user to hear one at a time, the six parts it separates (drums, bass, other, vocals, guitar, piano) as files.
/// Its network is an ONNX file without the STFT and the inverse STFT (tools/d2/demucs_onnx.py), which are done
/// here; the recording is split and put back together as demucs.apply.apply_model does with shifts=0 (7.8 s
/// segments, a quarter overlapping, weighed by a triangle). The channels are one: the recording is read in mono
/// and given to both, so the parts are mono. The sections take where singing begins and stops from it (Form.Phrases).</summary>
static class Voice
{
    // htdemucs_6s (guitar and piano too; 0.8.0, the owner), its matrix products in int8 (64 MB; tools/d2/demucs_onnx.py
    // --model htdemucs_6s; README, "Six parts"); DETTATO_VOICE_MODEL tries another, such as htdemucs-mm8.onnx (the four, 0.6.0 to 0.7.0)
    public static readonly string Model = Environment.GetEnvironmentVariable("DETTATO_VOICE_MODEL") is { Length: > 0 } m ? m : "htdemucs_6s-mm8.onnx";
    // segments at once, on the session's threads: 3 was the fastest (1, 2, or 6 of 4 threads were slower), and each
    // more holds about 2 GB: so as many as the memory takes (a song of 3.5 min peaked at 8.4 GB with 3, 6.6 GB with 2)
    public static readonly int Parallel = Math.Clamp((int)((GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1L << 30) - 4) / 4), 1, 3);      // segments at once, each on the session's 8 threads (2 at once or 6 of 4 threads were slower)
    public const int Rate = 44100;
    public const int Hop10ms = Rate / 100;
    const int Segment = 343980;         // int(44100 * 39 / 5), the length the network was trained on
    const int Nfft = 4096, Hop = 1024, Bins = Nfft / 2;
    const int Frames = (Segment + Hop - 1) / Hop;   // 336
    // the network's order (htdemucs_6s adds guitar and piano after them)
    public static readonly string[] Parts = Model.Contains("_6s") ? ["drums", "bass", "other", "vocals", "guitar", "piano"] : ["drums", "bass", "other", "vocals"];
    const int Vocals = 3, Other = 2;
    const int Batch = 24;               // segments separated before the samples they finish are put together and written
    const double Overlap = 0.25;       // as apply_model (none is a third quicker, but changed the sections of 15 of RWC's 100 songs, right starts lost; README)

    static readonly double[] Window = Enumerable.Range(0, Nfft).Select(n => 0.5 - 0.5 * Math.Cos(2 * Math.PI * n / Nfft)).ToArray();

    /// <summary>The vocals' root mean square every 10 ms of <paramref name="mono"/> (at 44100 Hz); with
    /// <paramref name="partsTo"/>, also each part as &lt;part&gt;.wav there (16 bits, 44100 Hz, one channel).
    /// <paramref name="separated"/> is told (segments done, segments in all) as each segment is separated, from any thread.</summary>
    public static float[] Loudness(IReadOnlyList<InferenceSession> models, double[] mono, string? partsTo = null, Action<int, int>? separated = null)
    {
        int n = mono.Length;
        double mean = 0;
        foreach (double v in mono) mean += v;
        mean /= n;
        double sq = 0;
        foreach (double v in mono) sq += (v - mean) * (v - mean);
        double std = Math.Sqrt(sq / (n - 1));
        var x = new float[n];
        for (int i = 0; i < n; i++) x[i] = (float)((mono[i] - mean) / std);

        int stride = (int)((1 - Overlap) * Segment);
        var weight = new double[Segment];
        for (int i = 0; i < Segment / 2; i++) weight[i] = i + 1;
        for (int i = Segment / 2; i < Segment; i++) weight[i] = Segment - i;
        double top = weight.Max();
        for (int i = 0; i < Segment; i++) weight[i] /= top;

        var offsets = new List<int>();
        for (int o = 0; o < n; o += stride) offsets.Add(o);
        // (a session each for the segments at once was a tenth quicker and held as much memory again each)
        var model = models[0];
        int frames = n / Hop10ms;
        var rms = new float[frames];
        WaveFileWriter[]? writers = partsTo == null ? null
            : Parts.Select(p => new WaveFileWriter(Path.Combine(partsTo, p + ".wav"), new WaveFormat(Rate, 16, 1))).ToArray();
        try
        {
            // A batch of segments at a time; once separated, the samples before the next batch's first segment have all
            // their segments, and are put together (each sample's sum in the segments' order, as all at once did) and let go.
            var live = new List<(int Offset, float[][] Parts)>();
            int done = 0;                // samples put together
            int segmentsDone = 0;
            double square = 0;           // the vocals' squares in the frame being filled
            for (int first = 0; first < offsets.Count; first += Batch)
            {
                int end = Math.Min(offsets.Count, first + Batch);
                var outs = new float[end - first][][];
                System.Threading.Tasks.Parallel.For(first, end, new ParallelOptions { MaxDegreeOfParallelism = Parallel }, k =>
                {
                    outs[k - first] = Segmented(model, x, n, offsets[k], writers != null);
                    separated?.Invoke(Interlocked.Increment(ref segmentsDone), offsets.Count);
                });
                for (int k = first; k < end; k++) live.Add((offsets[k], outs[k - first]));
                int upTo = end < offsets.Count ? offsets[end] : n, length = upTo - done;
                var sum = new double[writers == null ? 1 : Parts.Length][];
                for (int p = 0; p < sum.Length; p++) sum[p] = new double[length];
                var weights = new double[length];
                foreach (var (offset, parts) in live)
                {
                    int from = Math.Max(done, offset), to = Math.Min(upTo, offset + parts[Vocals].Length);     // the vocals are always there
                    for (int j = from; j < to; j++)
                    {
                        int i = j - offset, at = j - done;
                        if (writers == null) sum[0][at] += weight[i] * parts[Vocals][i];
                        else for (int p = 0; p < Parts.Length; p++) sum[p][at] += weight[i] * parts[p][i];
                        weights[at] += weight[i];
                    }
                }
                int vocals = writers == null ? 0 : Vocals;
                for (int j = done; j < upTo; j++)
                {
                    int f = j / Hop10ms;
                    if (f >= frames) break;
                    double v = sum[vocals][j - done] / weights[j - done] * std + mean;
                    square += v * v;
                    if ((j + 1) % Hop10ms == 0) { rms[f] = (float)Math.Sqrt(square / Hop10ms); square = 0; }
                }
                if (writers != null)
                {
                    for (int p = 0; p < Parts.Length; p++)
                        for (int at = 0; at < length; at++) sum[p][at] = sum[p][at] / weights[at] * std + mean;
                    // htdemucs_6s leaves out some of the recording (its six add up to it within 18 to 24 dB, the four
                    // within 32), most of it chords like `other`'s: `other` takes it, so that all but one part are
                    // the recording without that part (Lily# plays the parts' sum once one is off)
                    if (Parts.Length > 4)
                        for (int at = 0; at < length; at++)
                        {
                            double rest = mono[done + at];
                            for (int p = 0; p < Parts.Length; p++) rest -= sum[p][at];
                            sum[Other][at] += rest;
                        }
                    var samples = new float[length];
                    for (int p = 0; p < Parts.Length; p++)
                    {
                        for (int at = 0; at < length; at++) samples[at] = (float)Math.Clamp(sum[p][at], -1.0, 32767.0 / 32768);
                        writers[p].WriteSamples(samples, 0, length);
                    }
                }
                done = upTo;
                live.RemoveAll(s => s.Offset + s.Parts[Vocals].Length <= done);
            }
        }
        finally
        {
            if (writers != null) foreach (var w in writers) w.Dispose();
        }
        return rms;
    }

    /// <summary>The parts of the segment at <paramref name="offset"/>, trimmed to the recording (the vocals alone
    /// unless <paramref name="all"/>; the others null).</summary>
    static float[][] Segmented(InferenceSession model, float[] x, int n, int offset, bool all)
    {
        int length = Math.Min(Segment, n - offset);
        // TensorChunk.padded: centred on the chunk, with the recording around it where there is some
        int delta = Segment - length, start = offset - delta / 2;
        var chunk = new float[Segment];
        for (int i = 0; i < Segment; i++)
        {
            int j = start + i;
            if (j >= 0 && j < n) chunk[i] = x[j];
        }
        var parts = Separate(model, chunk, all);
        var trimmed = new float[Parts.Length][];      // center_trim
        for (int p = 0; p < Parts.Length; p++)
        {
            if (parts[p] == null) continue;
            trimmed[p] = new float[length];
            Array.Copy(parts[p], delta / 2, trimmed[p], 0, length);
        }
        return trimmed;
    }

    /// <summary>One segment of the mix (normalized) to its parts, each with both channels averaged.</summary>
    static float[][] Separate(InferenceSession model, float[] chunk, bool all)
    {
        // HTDemucs._spec: reflect by 3/8 of a window each side (and up to whole hops), then torch.stft centred
        // (reflect by half a window), normalized; the last bin and the first and last two frames are dropped
        int pad = Hop / 2 * 3;
        var padded = Reflect(chunk, pad, pad + Frames * Hop - Segment);
        var centred = Reflect(padded, Nfft / 2, Nfft / 2);
        var mag = new float[4 * Bins * Frames];
        var fft = new Fft(Nfft);
        var re = new double[Nfft];
        var im = new double[Nfft];
        double norm = 1.0 / Math.Sqrt(Nfft);
        for (int t = 0; t < Frames; t++)
        {
            int at = (t + 2) * Hop;
            for (int i = 0; i < Nfft; i++) { re[i] = centred[at + i] * Window[i]; im[i] = 0; }
            fft.Forward(re, im);
            for (int b = 0; b < Bins; b++)
            {
                float r = (float)(re[b] * norm), m = (float)(im[b] * norm);
                for (int c = 0; c < 2; c++)         // [channel 0 real, channel 0 imaginary, channel 1 real, …]
                {
                    mag[((2 * c) * Bins + b) * Frames + t] = r;
                    mag[((2 * c + 1) * Bins + b) * Frames + t] = m;
                }
            }
        }
        var mix = new float[2 * Segment];
        Array.Copy(chunk, 0, mix, 0, Segment);
        Array.Copy(chunk, 0, mix, Segment, Segment);
        using var results = model.Run([
            NamedOnnxValue.CreateFromTensor("mix", new DenseTensor<float>(mix, [1, 2, Segment])),
            NamedOnnxValue.CreateFromTensor("mag", new DenseTensor<float>(mag, [1, 4, Bins, Frames])),
        ]);
        // read straight from the buffers ([1, parts, 4, Bins, Frames] and [1, parts, 2, Segment]), not by the tensors' indexers
        var spec = ((DenseTensor<float>)results.First(r => r.Name == "spec").AsTensor<float>()).Buffer.Span;
        var wave = ((DenseTensor<float>)results.First(r => r.Name == "wave").AsTensor<float>()).Buffer.Span;

        // HTDemucs._ispec of each part: a zero bin on top and two empty frames each side, torch.istft centred and
        // normalized, then the 3/8 of a window each side taken off; the part is that and the time branch's
        int framesOut = Frames + 4;
        var outputs = new float[Parts.Length][];
        for (int part = all ? 0 : Vocals; part < Parts.Length; part++)
        {
            var output = outputs[part] = new float[Segment];
            for (int c = 0; c < 2; c++)
            {
                int full = Nfft + Hop * (framesOut - 1);
                var signal = new double[full];
                var envelope = new double[full];
                for (int t = 0; t < framesOut; t++)
                {
                    int tt = t - 2;
                    if (tt >= 0 && tt < Frames)
                    {
                        int real = ((part * 4 + 2 * c) * Bins) * Frames + tt, imaginary = real + Bins * Frames;
                        for (int b = 0; b < Bins; b++) { re[b] = spec[real + b * Frames]; im[b] = spec[imaginary + b * Frames]; }
                        re[Bins] = 0; im[Bins] = 0;
                        for (int b = 1; b < Bins; b++) { re[Nfft - b] = re[b]; im[Nfft - b] = -im[b]; }
                        // the inverse transform: conjugate, forward, conjugate, over n; normalized: times sqrt(n)
                        for (int i = 0; i < Nfft; i++) im[i] = -im[i];
                        fft.Forward(re, im);
                        double scale = Math.Sqrt(Nfft) / Nfft;
                        for (int i = 0; i < Nfft; i++) signal[t * Hop + i] += re[i] * scale * Window[i];
                    }
                    for (int i = 0; i < Nfft; i++) envelope[t * Hop + i] += Window[i] * Window[i];
                }
                // torch.istft(center=True, length=le): from half a window in, le samples
                for (int i = 0; i < Segment; i++)
                {
                    int j = Nfft / 2 + pad + i;
                    double v = envelope[j] > 1e-11 ? signal[j] / envelope[j] : 0;
                    output[i] += (float)((v + wave[(part * 2 + c) * Segment + i]) / 2);
                }
            }
        }
        return outputs;
    }

    /// <summary>numpy's and torch's reflect padding (the edge sample not repeated).</summary>
    static float[] Reflect(float[] x, int left, int right)
    {
        int n = x.Length;
        var y = new float[n + left + right];
        for (int i = 0; i < y.Length; i++)
        {
            int j = i - left;
            if (j < 0) j = -j;
            if (j >= n) j = 2 * (n - 1) - j;
            y[i] = x[j];
        }
        return y;
    }
}
