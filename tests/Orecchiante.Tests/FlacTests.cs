using System.Diagnostics;
using System.Security.Cryptography;
using Xunit;

namespace Orecchiante.Tests;

/// <summary>Flac (the parts' writer): what it writes is decoded back to the very samples, by a small decoder here that
/// checks the CRCs and the MD5 too, and by ffmpeg where it is installed (a decoder that is not ours).</summary>
public class FlacTests
{
    public static IEnumerable<object[]> Signals()
    {
        var rng = new Random(7);
        short S(double v) => (short)Math.Clamp(Math.Round(v), short.MinValue, short.MaxValue);
        yield return ["sine", Enumerable.Range(0, 44100).Select(i => S(12000 * Math.Sin(2 * Math.PI * 440 * i / 44100.0))).ToArray(), 1];
        yield return ["noise", Enumerable.Range(0, 10000).Select(_ => (short)rng.Next(short.MinValue, short.MaxValue + 1)).ToArray(), 1];
        yield return ["silence and a tail", new short[4096 * 3 + 17], 1];
        yield return ["extremes", Enumerable.Range(0, 9000).Select(i => i % 2 == 0 ? short.MaxValue : short.MinValue).ToArray(), 1];
        yield return ["a click", Enumerable.Range(0, 5000).Select(i => (short)(i == 2500 ? 30000 : 0)).ToArray(), 1];
        yield return ["stereo", Enumerable.Range(0, 2 * 7000).Select(i => S((i % 2 == 0 ? 8000 : -3000) * Math.Sin(i * 0.01))).ToArray(), 2];
        yield return ["one sample", new short[] { -5 }, 1];
    }

    [Theory]
    [MemberData(nameof(Signals))]
    public void Written_and_decoded_gives_the_same_samples(string name, short[] samples, int channels)
    {
        string path = Path.Combine(Path.GetTempPath(), $"orec-flac-{Guid.NewGuid():N}.flac");
        try
        {
            Flac.Write(path, samples, channels, 44100);
            var (back, ch, rate) = Decode(File.ReadAllBytes(path));
            Assert.Equal((channels, 44100), (ch, rate));
            Assert.True(samples.SequenceEqual(back), name);
            if (Ffmpeg(path, channels) is { } theirs) Assert.True(samples.SequenceEqual(theirs), name + " (ffmpeg)");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Smaller_than_the_samples_for_music_like_sound()
    {
        var samples = Enumerable.Range(0, 44100 * 2).Select(i => (short)(6000 * Math.Sin(2 * Math.PI * 220 * i / 44100.0) + 3000 * Math.Sin(2 * Math.PI * 331 * i / 44100.0))).ToArray();
        string path = Path.Combine(Path.GetTempPath(), $"orec-flac-{Guid.NewGuid():N}.flac");
        try
        {
            Flac.Write(path, samples, 1, 44100);
            Assert.InRange(new FileInfo(path).Length, 1, samples.Length * 2 / 2);
        }
        finally { File.Delete(path); }
    }

    /// <summary>ffmpeg's decoding as 16-bit samples, or null without ffmpeg on the PATH.</summary>
    static short[]? Ffmpeg(string flac, int channels)
    {
        var start = new ProcessStartInfo("ffmpeg") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "-v", "error", "-i", flac, "-f", "s16le", "-ac", channels.ToString(), "-" }) start.ArgumentList.Add(a);
        Process p;
        try { p = Process.Start(start)!; }
        catch (System.ComponentModel.Win32Exception) { return null; }
        using (p)
        {
            var err = p.StandardError.ReadToEndAsync();
            using var ms = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(ms);
            p.WaitForExit();
            Assert.True(p.ExitCode == 0 && err.Result.Trim() == "", "ffmpeg: " + err.Result);
            var bytes = ms.ToArray();
            var s = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, s, 0, s.Length * 2);
            return s;
        }
    }

    /// <summary>A FLAC decoder for what Flac writes (fixed blocks, independent channels, constant, verbatim and fixed
    /// subframes, Rice partitions), the frames' CRC-8 and CRC-16 and STREAMINFO's MD5 checked.</summary>
    internal static (short[] Samples, int Channels, int Rate) Decode(byte[] data)
    {
        var r = new Reader(data);
        Assert.Equal(0x664C6143u, r.U(32));
        bool last;
        int rate = 0, channels = 0, bps = 0;
        long total = 0;
        byte[] md5 = [];
        do
        {
            last = r.U(1) == 1;
            uint type = r.U(7), length = r.U(24);
            if (type == 0)
            {
                r.U(16); r.U(16); r.U(24); r.U(24);
                rate = (int)r.U(20); channels = (int)r.U(3) + 1; bps = (int)r.U(5) + 1;
                total = (long)r.U(4) << 32 | r.U(32);
                md5 = r.Bytes(16);
            }
            else r.Bytes((int)length);
        } while (!last);
        Assert.Equal(16, bps);
        var outp = new short[total * channels];
        long done = 0;
        while (done < total)
        {
            int frameStart = r.Byte;
            Assert.Equal(0b11111111111110u, r.U(14));
            r.U(2);
            uint bsCode = r.U(4), rateCode = r.U(4), chCode = r.U(4), sizeCode = r.U(3);
            r.U(1);
            Assert.Equal(0u, rateCode);
            Assert.Equal((uint)channels - 1, chCode);
            Assert.Equal(0b100u, sizeCode);
            uint lead = r.U(8);
            int extra = lead < 0x80 ? 0 : lead >= 0xFC ? 5 : lead >= 0xF8 ? 4 : lead >= 0xF0 ? 3 : lead >= 0xE0 ? 2 : 1;
            for (int i = 0; i < extra; i++) r.U(8);
            int n = bsCode == 12 ? 4096 : bsCode == 7 ? (int)r.U(16) + 1 : throw new InvalidDataException("block size code " + bsCode);
            byte crc8 = Crc8(data.AsSpan(frameStart, r.Byte - frameStart));
            Assert.Equal(crc8, (byte)r.U(8));
            for (int c = 0; c < channels; c++)
            {
                var x = Subframe(r, n);
                for (int i = 0; i < n; i++) outp[(done + i) * channels + c] = (short)x[i];
            }
            r.Align();
            ushort crc16 = Crc16(data.AsSpan(frameStart, r.Byte - frameStart));
            Assert.Equal(crc16, (ushort)r.U(16));
            done += n;
        }
        var le = new byte[outp.Length * 2];
        Buffer.BlockCopy(outp, 0, le, 0, le.Length);
        Assert.Equal(md5, MD5.HashData(le));
        return (outp, channels, rate);
    }

    static int[] Subframe(Reader r, int n)
    {
        Assert.Equal(0u, r.U(1));
        uint type = r.U(6);
        Assert.Equal(0u, r.U(1));
        var x = new int[n];
        if (type == 0) { int v = r.S(16); Array.Fill(x, v); return x; }
        if (type == 1) { for (int i = 0; i < n; i++) x[i] = r.S(16); return x; }
        Assert.True((type & 0b111000) == 0b001000, "subframe type " + type);
        int order = (int)(type & 7);
        for (int i = 0; i < order; i++) x[i] = r.S(16);
        Assert.Equal(0u, r.U(2));
        int p = (int)r.U(4), per = n >> p, k = order;
        for (int part = 0; part < 1 << p; part++)
        {
            int param = (int)r.U(4);
            Assert.NotEqual(15, param);
            for (int end = (part + 1) * per; k < end; k++)
            {
                uint q = 0;
                while (r.U(1) == 0) q++;
                uint u = q << param | (param > 0 ? r.U(param) : 0);
                int res = (int)(u >> 1) ^ -(int)(u & 1);
                x[k] = order switch
                {
                    0 => res,
                    1 => res + x[k - 1],
                    2 => res + 2 * x[k - 1] - x[k - 2],
                    3 => res + 3 * x[k - 1] - 3 * x[k - 2] + x[k - 3],
                    _ => res + 4 * x[k - 1] - 6 * x[k - 2] + 4 * x[k - 3] - x[k - 4],
                };
            }
        }
        return x;
    }

    static byte Crc8(ReadOnlySpan<byte> d)
    {
        int crc = 0;
        foreach (byte b in d) { crc ^= b; for (int i = 0; i < 8; i++) crc = ((crc & 0x80) != 0 ? (crc << 1) ^ 7 : crc << 1) & 0xFF; }
        return (byte)crc;
    }

    static ushort Crc16(ReadOnlySpan<byte> d)
    {
        int crc = 0;
        foreach (byte b in d) { crc ^= b << 8; for (int i = 0; i < 8; i++) crc = ((crc & 0x8000) != 0 ? (crc << 1) ^ 0x8005 : crc << 1) & 0xFFFF; }
        return (ushort)crc;
    }

    sealed class Reader(byte[] data)
    {
        long bit;
        public int Byte => (int)(bit / 8);
        public uint U(int count)
        {
            uint v = 0;
            for (int i = 0; i < count; i++, bit++) v = v << 1 | (uint)(data[bit / 8] >> (7 - (int)(bit % 8)) & 1);
            return v;
        }
        public int S(int count) { uint v = U(count); return (int)(v << (32 - count)) >> (32 - count); }
        public void Align() { bit = (bit + 7) / 8 * 8; }
        public byte[] Bytes(int count) { var b = new byte[count]; for (int i = 0; i < count; i++) b[i] = (byte)U(8); return b; }
    }
}
