using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Dettato;

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
