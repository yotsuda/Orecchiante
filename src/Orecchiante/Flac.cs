using System.Security.Cryptography;
using NAudio.Wave;

namespace Orecchiante;

/// <summary>A FLAC writer of its own (the parts, 16 bits): until 0.18.0 the parts were written by Windows' FLAC encoder
/// (Media Foundation), which macOS has not, and which refused now and then when several started at once. Lossless and
/// nothing put before the first sample, as before. Kept simple: blocks of 4096 samples, each channel on its own, each
/// subframe the best of FLAC's fixed predictors of order 0 to 4 with Rice-coded residuals in up to 2^6 partitions (or
/// the samples as they are, or one value); the STREAMINFO with the samples' MD5, which `flac -t` checks.</summary>
static class Flac
{
    const int Block = 4096;

    /// <summary>A .wav of 16-bit PCM as a .flac.</summary>
    public static void Encode(string wav, string flac)
    {
        using var reader = new WaveFileReader(wav);
        var f = reader.WaveFormat;
        if (f.Encoding != WaveFormatEncoding.Pcm || f.BitsPerSample != 16) throw new NotSupportedException($"{wav}: FLAC is written from 16-bit PCM only");
        var bytes = new byte[reader.Length];
        reader.ReadExactly(bytes);
        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
        Write(flac, samples, f.Channels, f.SampleRate);
    }

    /// <summary>Interleaved 16-bit samples as a .flac.</summary>
    public static void Write(string path, short[] interleaved, int channels, int rate)
    {
        if (channels is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(channels));
        long frames = interleaved.Length / channels;
        using var file = new BufferedStream(File.Create(path), 1 << 16);
        var md5 = new byte[16];
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5))
        {
            var le = new byte[frames * channels * 2];
            Buffer.BlockCopy(interleaved, 0, le, 0, le.Length);
            if (!BitConverter.IsLittleEndian) for (int i = 0; i < le.Length; i += 2) (le[i], le[i + 1]) = (le[i + 1], le[i]);
            hash.AppendData(le);
            md5 = hash.GetHashAndReset();
        }
        var head = new Bits();
        head.Put(0x664C6143, 32);                   // "fLaC"
        head.Put(1, 1); head.Put(0, 7); head.Put(34, 24);       // the last metadata block, STREAMINFO, 34 bytes
        head.Put(Block, 16); head.Put(Block, 16);
        head.Put(0, 24); head.Put(0, 24);           // the frames' sizes: not known
        head.Put((uint)rate, 20); head.Put((uint)channels - 1, 3); head.Put(15, 5);
        head.Put((uint)(frames >> 32), 4); head.Put((uint)frames, 32);
        file.Write(head.Bytes());
        file.Write(md5);

        var x = new int[Block];
        long number = 0;
        for (long at = 0; at < frames; at += Block, number++)
        {
            int n = (int)Math.Min(Block, frames - at);
            var b = new Bits();
            b.Put(0b11111111111110, 14); b.Put(0, 1); b.Put(0, 1);          // sync, reserved, a fixed block size
            b.Put(n == Block ? 12u : 7u, 4);                                  // 4096, or 16 bits at the end of the header
            b.Put(0, 4);                                                      // the rate: STREAMINFO's
            b.Put((uint)channels - 1, 4);                                     // each channel on its own
            b.Put(0b100, 3); b.Put(0, 1);                                     // 16 bits
            Utf8(b, number);
            if (n != Block) b.Put((uint)n - 1, 16);
            b.Put(Crc8(b.Bytes()), 8);
            for (int c = 0; c < channels; c++)
            {
                for (int i = 0; i < n; i++) x[i] = interleaved[(at + i) * channels + c];
                Subframe(b, x.AsSpan(0, n));
            }
            b.Align();
            var frame = b.Bytes();
            file.Write(frame);
            ushort crc = Crc16(frame);
            file.WriteByte((byte)(crc >> 8)); file.WriteByte((byte)crc);
        }
    }

    static void Subframe(Bits b, ReadOnlySpan<int> x)
    {
        int n = x.Length;
        bool constant = true;
        for (int i = 1; i < n && constant; i++) constant = x[i] == x[0];
        if (constant) { b.Put(0, 8); b.PutSigned(x[0], 16); return; }
        int bestOrder = -1;
        long bestBits = 16L * n;                    // verbatim's
        int[]? ps2 = null;
        var r = new int[n];
        for (int order = 0; order <= 4 && order < n; order++)
        {
            Residual(x, order, r);
            var (bits, ps) = Rice(r.AsSpan(order, n - order), n, order);
            bits += 16L * order;
            if (bits < bestBits) { bestBits = bits; bestOrder = order; ps2 = ps; }
        }
        if (ps2 == null)
        {
            b.Put(0b00000010, 8);                   // verbatim
            foreach (int v in x) b.PutSigned(v, 16);
            return;
        }
        b.Put((uint)(0b001000 | bestOrder) << 1, 8);        // 0, fixed (001xxx) of this order, no wasted bits
        for (int i = 0; i < bestOrder; i++) b.PutSigned(x[i], 16);
        Residual(x, bestOrder, r);
        int p = (int)Math.Log2(ps2.Length);
        b.Put(0, 2); b.Put((uint)p, 4);             // Rice, 4-bit parameters, 2^p partitions
        int per = n >> p, k = bestOrder;
        for (int part = 0; part < ps2.Length; part++)
        {
            int param = ps2[part];
            b.Put((uint)param, 4);
            int end = (part + 1) * per;
            for (; k < end; k++)
            {
                uint u = (uint)((r[k] << 1) ^ (r[k] >> 31));
                b.Unary(u >> param);
                if (param > 0) b.Put(u & ((1u << param) - 1), param);
            }
        }
    }

    /// <summary>FLAC's fixed predictors: the order-th difference.</summary>
    static void Residual(ReadOnlySpan<int> x, int order, int[] r)
    {
        for (int i = order; i < x.Length; i++)
            r[i] = order switch
            {
                0 => x[i],
                1 => x[i] - x[i - 1],
                2 => x[i] - 2 * x[i - 1] + x[i - 2],
                3 => x[i] - 3 * x[i - 1] + 3 * x[i - 2] - x[i - 3],
                _ => x[i] - 4 * x[i - 1] + 6 * x[i - 2] - 4 * x[i - 3] + x[i - 4],
            };
    }

    /// <summary>The fewest bits for the residuals (those after the warm-up) over the partition orders the block's
    /// size allows, and each partition's parameter.</summary>
    static (long Bits, int[] Params) Rice(ReadOnlySpan<int> res, int n, int order)
    {
        long bestBits = long.MaxValue;
        int[] bestParams = [];
        for (int p = 0; p <= 6; p++)
        {
            int parts = 1 << p;
            if (n % parts != 0 || (n >> p) <= order) break;
            int per = n >> p;
            var ps = new int[parts];
            long total = 6;
            int k = 0;
            for (int part = 0; part < parts; part++)
            {
                int count = part == 0 ? per - order : per;
                ulong sum = 0;
                for (int i = 0; i < count; i++) { int v = res[k + i]; sum += (uint)((v << 1) ^ (v >> 31)); }
                long partBest = long.MaxValue;
                int param = 0;
                for (int q = 0; q <= 14; q++)
                {
                    long bits = (long)(sum >> q) + (long)count * (q + 1);
                    if (bits < partBest) { partBest = bits; param = q; }
                }
                ps[part] = param;
                total += 4 + partBest;
                k += count;
            }
            if (total < bestBits) { bestBits = total; bestParams = ps; }
        }
        return (bestBits, bestParams);
    }

    static void Utf8(Bits b, long v)
    {
        if (v < 0x80) { b.Put((uint)v, 8); return; }
        int extra = v < 0x800 ? 1 : v < 0x10000 ? 2 : v < 0x200000 ? 3 : v < 0x4000000 ? 4 : 5;
        uint lead = (uint)(0xFF00 >> (extra + 1)) & 0xFF;
        b.Put(lead | (uint)(v >> (6 * extra)), 8);
        for (int i = extra - 1; i >= 0; i--) b.Put(0x80 | (uint)((v >> (6 * i)) & 0x3F), 8);
    }

    static byte Crc8(byte[] data)
    {
        int crc = 0;
        foreach (byte d in data)
        {
            crc ^= d;
            for (int i = 0; i < 8; i++) crc = (crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : crc << 1;
            crc &= 0xFF;
        }
        return (byte)crc;
    }

    static ushort Crc16(byte[] data)
    {
        int crc = 0;
        foreach (byte d in data)
        {
            crc ^= d << 8;
            for (int i = 0; i < 8; i++) crc = (crc & 0x8000) != 0 ? (crc << 1) ^ 0x8005 : crc << 1;
            crc &= 0xFFFF;
        }
        return (ushort)crc;
    }

    /// <summary>Bits written most significant first.</summary>
    sealed class Bits
    {
        readonly List<byte> bytes = [];
        ulong acc;
        int used;

        public void Put(uint v, int count)
        {
            if (count == 0) return;
            if (count < 32) v &= (1u << count) - 1;
            acc = (acc << count) | v;
            used += count;
            while (used >= 8) { used -= 8; bytes.Add((byte)(acc >> used)); }
            acc &= (1UL << used) - 1;
        }

        public void PutSigned(int v, int count) => Put((uint)v, count);

        public void Unary(uint zeros)
        {
            while (zeros >= 24) { Put(0, 24); zeros -= 24; }
            Put(1, (int)zeros + 1);
        }

        public void Align() { if (used > 0) Put(0, 8 - used); }

        /// <summary>The whole bytes so far (the header's CRC is of these).</summary>
        public byte[] Bytes() => [.. bytes];
    }
}
