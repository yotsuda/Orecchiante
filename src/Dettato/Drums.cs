namespace Dettato;

/// <summary>What the sections hear of the drums (0.10.0): every 10 ms of the drums Demucs separates, four numbers:
/// the mean square of the frame's 441 samples, and the onset strength in three bands (below 200 Hz, to 4 kHz, from
/// 4 kHz): the positive spectral flux of log(1 + 100 |X|), X a 2048-point frame from the frame's first sample under
/// numpy's (symmetric) Hann window. The first frame, and frames whose window runs past the end, have no flux.
/// The samples come one at a time (Voice.Loudness gives them as it puts the parts together), so an hour's recording
/// is not held. tools/read_chords.py's drum_bars() reads what this writes (&lt;name&gt;.drum.npy).</summary>
sealed class Drums
{
    public const int Rate = 44100, Hop = 441, Size = 2048, Width = 4;
    static readonly double[] Window = Enumerable.Range(0, Size).Select(n => 0.5 - 0.5 * Math.Cos(2 * Math.PI * n / (Size - 1))).ToArray();
    static readonly int[] Band = Enumerable.Range(0, Size / 2 + 1)
        .Select(b => { double f = b * (double)Rate / Size; return f < 200 ? 0 : f < 4000 ? 1 : 2; }).ToArray();

    readonly List<float> _square = [];
    readonly List<float> _flux = [];        // three a frame
    double _sum;
    int _inFrame;
    readonly double[] _buffer = new double[Size * 8];
    int _length;                            // samples in _buffer
    long _first;                            // the recording's sample at _buffer[0]
    long _frame;                            // the next frame whose flux is wanted
    double[]? _before;                      // the last frame's log magnitudes
    readonly Fft _fft = new(Size);
    readonly double[] _re = new double[Size], _im = new double[Size];

    public void Add(double v)
    {
        _sum += v * v;
        if (++_inFrame == Hop) { _square.Add((float)(_sum / Hop)); _sum = 0; _inFrame = 0; }
        if (_length == _buffer.Length)
        {
            int keep = (int)(_frame * Hop - _first);
            Array.Copy(_buffer, keep, _buffer, 0, _length - keep);
            _length -= keep;
            _first += keep;
        }
        _buffer[_length++] = v;
        while (_frame * Hop + Size <= _first + _length) Flux((int)(_frame++ * Hop - _first));
    }

    void Flux(int at)
    {
        for (int i = 0; i < Size; i++) { _re[i] = _buffer[at + i] * Window[i]; _im[i] = 0; }
        _fft.Forward(_re, _im);
        var mag = new double[Size / 2 + 1];
        for (int b = 0; b <= Size / 2; b++) mag[b] = Math.Log(1 + 100 * Math.Sqrt(_re[b] * _re[b] + _im[b] * _im[b]));
        double low = 0, mid = 0, high = 0;
        if (_before != null)
            for (int b = 0; b <= Size / 2; b++)
            {
                double d = mag[b] - _before[b];
                if (d <= 0) continue;
                if (Band[b] == 0) low += d; else if (Band[b] == 1) mid += d; else high += d;
            }
        _flux.Add((float)low); _flux.Add((float)mid); _flux.Add((float)high);
        _before = mag;
    }

    /// <summary>[frames, 4], a frame each 10 ms the samples fill.</summary>
    public float[] Frames(out int frames)
    {
        frames = _square.Count;
        var out_ = new float[frames * Width];
        for (int f = 0; f < frames; f++)
        {
            out_[f * Width] = _square[f];
            if (3 * f + 2 < _flux.Count)
                for (int b = 0; b < 3; b++) out_[f * Width + 1 + b] = _flux[3 * f + b];
        }
        return out_;
    }
}
