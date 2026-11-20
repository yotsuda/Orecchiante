using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Orecchiante;

/// <summary>numpy's files, for the tools in Python that measure: an array as .npy, several in an .npz.</summary>
static class Npy
{
    public static void Write(Stream to, string type, int[] shape, ReadOnlySpan<byte> data)
    {
        string dims = shape.Length == 1 ? $"{shape[0]}," : string.Join(", ", shape);
        string header = $"{{'descr': '{type}', 'fortran_order': False, 'shape': ({dims}), }}";
        header = header.PadRight((10 + header.Length + 1 + 63) / 64 * 64 - 11) + "\n";
        to.Write([0x93, .. "NUMPY"u8, 1, 0, (byte)(header.Length & 255), (byte)(header.Length >> 8)]);
        to.Write(Encoding.ASCII.GetBytes(header));
        to.Write(data);
    }

    public static void Write(string path, float[] values, params int[] shape)
    {
        using var file = File.Create(path);
        Write(file, "<f4", shape, MemoryMarshal.AsBytes<float>(values));
    }

    /// <summary>An .npz of float32 arrays (a shape of no numbers is a single value).</summary>
    public static void WriteFloats(string path, params (string Name, float[] Values, int[] Shape)[] arrays)
    {
        using var zip = new ZipArchive(File.Create(path), ZipArchiveMode.Create);
        foreach (var (name, values, shape) in arrays)
        {
            using var entry = zip.CreateEntry(name + ".npy", CompressionLevel.Fastest).Open();
            Write(entry, "<f4", shape, MemoryMarshal.AsBytes<float>(values));
        }
    }

    /// <summary>Reads what <see cref="WriteHeard"/> and tools/crema_chords.py write: the log probabilities as they
    /// are held there (float16), [frame * chords + chord], the frames, and the seconds a frame.</summary>
    public static (float[] LogP, int Frames, double Step) ReadHeard(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var (type, shape, data) = Read(zip.GetEntry("logp.npy") ?? throw new InvalidDataException($"{path}: no logp"));
        if (shape.Length != 2) throw new InvalidDataException($"{path}: logp is not frames by chords");
        float[] logp = type switch
        {
            "<f2" => [.. MemoryMarshal.Cast<byte, Half>(data).ToArray().Select(h => (float)h)],
            "<f4" => MemoryMarshal.Cast<byte, float>(data).ToArray(),
            _ => throw new InvalidDataException($"{path}: logp is {type}"),
        };
        var (stepType, _, stepData) = Read(zip.GetEntry("step.npy") ?? throw new InvalidDataException($"{path}: no step"));
        double step = stepType switch
        {
            "<f8" => BitConverter.ToDouble(stepData),
            "<f4" => BitConverter.ToSingle(stepData),
            _ => throw new InvalidDataException($"{path}: step is {stepType}"),
        };
        return (logp, shape[0], step);
    }

    /// <summary>A float32 .npy of any shape (the vocals' loudness, `orecchiante ears` and scratch/sep/song.py), flat.</summary>
    public static float[] ReadFloats(string path)
    {
        using var file = File.OpenRead(path);
        var (type, _, data) = Read(file);
        if (type != "<f4") throw new InvalidDataException($"{path} is {type}, not float32");
        return MemoryMarshal.Cast<byte, float>(data).ToArray();
    }

    static (string Type, int[] Shape, byte[] Data) Read(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return Read(stream);
    }

    static (string Type, int[] Shape, byte[] Data) Read(Stream stream)
    {
        using var all = new MemoryStream();
        stream.CopyTo(all);
        var bytes = all.ToArray();
        int start = bytes[6] == 1 ? 10 : 12;        // the header's length is two bytes in the first version of the format, then four
        int length = bytes[6] == 1 ? BitConverter.ToUInt16(bytes, 8) : BitConverter.ToInt32(bytes, 8);
        string header = Encoding.ASCII.GetString(bytes, start, length);
        string type = System.Text.RegularExpressions.Regex.Match(header, @"'descr':\s*'([^']+)'").Groups[1].Value;
        string dims = System.Text.RegularExpressions.Regex.Match(header, @"'shape':\s*\(([^)]*)\)").Groups[1].Value;
        if (header.Contains("'fortran_order': True")) throw new InvalidDataException("an array in Fortran's order");
        int[] shape = [.. dims.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse)];
        return (type, shape, bytes[(start + length)..]);
    }

    /// <summary>What tools/crema_chords.py writes: logp (float16, frames * chords) and step (seconds a frame).</summary>
    public static void WriteHeard(string path, float[] logp, int frames, int chords, double step)
    {
        using var zip = new ZipArchive(File.Create(path), ZipArchiveMode.Create);
        using (var entry = zip.CreateEntry("logp.npy", CompressionLevel.Fastest).Open())
        {
            var halves = new Half[logp.Length];
            for (int i = 0; i < logp.Length; i++) halves[i] = (Half)logp[i];
            Write(entry, "<f2", [frames, chords], MemoryMarshal.AsBytes<Half>(halves));
        }
        using (var entry = zip.CreateEntry("step.npy", CompressionLevel.Fastest).Open())
            Write(entry, "<f8", [], BitConverter.GetBytes(step));
    }
}
