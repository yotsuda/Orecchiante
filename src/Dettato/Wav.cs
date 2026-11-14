namespace Dettato;

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
                if (channels == 0) throw new InvalidDataException("no format before the samples: " + path);
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
                            (1, 24) => (bytes[p] | bytes[p + 1] << 8 | (sbyte)bytes[p + 2] << 16) / 8388608.0,
                            (3, 32) => BitConverter.ToSingle(bytes, p),
                            _ => throw new NotSupportedException($"wav format {format}, {bits} bits: {path}"),
                        };
                    mono[i] = sum / channels;
                }
                return (mono, rate);
            }
            at += 8 + size + (size & 1);
        }
        throw new InvalidDataException("no samples: " + path);
    }

    /// <summary>One channel as a 32-bit float .wav (`dettato audio`: what the reader heard of a recording).</summary>
    public static void Write(string path, double[] mono, int rate)
    {
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8); w.Write(36 + mono.Length * 4); w.Write("WAVEfmt "u8);
        w.Write(16); w.Write((short)3); w.Write((short)1); w.Write(rate); w.Write(rate * 4); w.Write((short)4); w.Write((short)32);
        w.Write("data"u8); w.Write(mono.Length * 4);
        foreach (double x in mono) w.Write((float)x);
    }
}
