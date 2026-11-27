using System.Runtime.InteropServices;
using System.Text;

namespace Orecchiante;

/// <summary>macOS's own audio (AudioToolbox, part of the system: nothing to carry): a recording's samples by
/// ExtAudioFile, which has the decoders Media Foundation has on Windows (mp3, AAC in m4a/mp4, FLAC; not WMA), and its
/// tags by AudioFile's info dictionary (Tags.Of on Windows). ExtAudioFile goes by the encoder's priming as the file
/// says it (MP4's edit list or iTunSMPB, LAME's note), as a player does: tests/Orecchiante.Tests/AudioTests.cs
/// measures where a click comes against ffmpeg's decoding on the CI's macOS.</summary>
static class Apple
{
    const string Toolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    const string Foundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    /// <summary>The channels averaged, at the recording's own rate (Audio.Read on macOS).</summary>
    public static (double[] Mono, int Rate) Read(string path)
    {
        IntPtr url = Url(path), file = IntPtr.Zero;
        try
        {
            Check(ExtAudioFileOpenURL(url, out file), "open");
            var format = new Asbd();
            uint size = (uint)Marshal.SizeOf<Asbd>();
            Check(ExtAudioFileGetProperty(file, Code("ffmt"), ref size, ref format), "the file's format");
            int channels = (int)format.ChannelsPerFrame, rate = (int)Math.Round(format.SampleRate);
            if (channels < 1 || rate < 1) throw new InvalidDataException("no sound in it");
            var client = new Asbd
            {
                SampleRate = format.SampleRate, FormatId = Code("lpcm"), FormatFlags = 1 | 8,       // float, packed
                BytesPerPacket = (uint)(4 * channels), FramesPerPacket = 1, BytesPerFrame = (uint)(4 * channels),
                ChannelsPerFrame = (uint)channels, BitsPerChannel = 32,
            };
            Check(ExtAudioFileSetProperty(file, Code("cfmt"), (uint)Marshal.SizeOf<Asbd>(), ref client), "the samples' format");
            const int Frames = 16384;
            var buffer = new float[Frames * channels];
            long length = 0;
            size = 8;
            ExtAudioFileGetProperty(file, Code("#frm"), ref size, ref length);      // the length in frames: room for them at once
            var mono = new List<double>((int)Math.Clamp(length + Frames, Frames, int.MaxValue / 2));
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                while (true)
                {
                    var list = new BufferList { Count = 1, Buffer = new AudioBuffer { Channels = (uint)channels, Bytes = (uint)(buffer.Length * 4), Data = handle.AddrOfPinnedObject() } };
                    uint frames = Frames;
                    Check(ExtAudioFileRead(file, ref frames, ref list), "the samples");
                    if (frames == 0) break;
                    for (int i = 0; i < frames; i++)
                    {
                        double sum = 0;
                        for (int c = 0; c < channels; c++) sum += buffer[i * channels + c];
                        mono.Add(sum / channels);
                    }
                }
            }
            finally { handle.Free(); }
            if (mono.Count == 0) throw new InvalidDataException("no sound in it");
            return ([.. mono], rate);
        }
        finally
        {
            if (file != IntPtr.Zero) ExtAudioFileDispose(file);
            CFRelease(url);
        }
    }

    /// <summary>The title, artist, album and length the tags give (Tags.Of on macOS); nulls and 0 where they do not.</summary>
    public static Tags.Found Tags(string path)
    {
        IntPtr url = Url(path), file = IntPtr.Zero, info = IntPtr.Zero;
        try
        {
            if (AudioFileOpenURL(url, 1, 0, out file) != 0) return new(null, null, null, 0);
            uint size = (uint)IntPtr.Size;
            if (AudioFileGetProperty(file, Code("info"), ref size, out info) != 0 || info == IntPtr.Zero) return new(null, null, null, 0);
            string? Get(string key)
            {
                IntPtr k = CFStringCreateWithCString(IntPtr.Zero, key, 0x08000100);       // UTF-8
                try
                {
                    IntPtr v = CFDictionaryGetValue(info, k);
                    if (v == IntPtr.Zero || CFGetTypeID(v) != CFStringGetTypeID()) return null;
                    int length = (int)CFStringGetLength(v);
                    var chars = new char[length];
                    CFStringGetCharacters(v, new CFRange { Location = 0, Length = length }, chars);
                    var s = new string(chars).Trim();
                    return s.Length == 0 ? null : s;
                }
                finally { CFRelease(k); }
            }
            double seconds = double.TryParse(Get("approximate duration in seconds"), System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : 0;
            return new(Get("title"), Get("artist"), Get("album"), seconds);
        }
        finally
        {
            if (info != IntPtr.Zero) CFRelease(info);
            if (file != IntPtr.Zero) AudioFileClose(file);
            CFRelease(url);
        }
    }

    static IntPtr Url(string path)
    {
        var bytes = Encoding.UTF8.GetBytes(Path.GetFullPath(path));
        IntPtr url = CFURLCreateFromFileSystemRepresentation(IntPtr.Zero, bytes, bytes.Length, false);
        return url != IntPtr.Zero ? url : throw new InvalidDataException("not a path macOS takes");
    }

    static void Check(int status, string what)
    {
        if (status == 0) return;
        // (an OSStatus is often four letters: 'typ?', 'fmt?')
        var b = BitConverter.GetBytes(status);
        string code = b.All(c => c is >= 0x20 and < 0x7F) ? $"'{(char)b[3]}{(char)b[2]}{(char)b[1]}{(char)b[0]}'" : status.ToString();
        throw new InvalidDataException($"macOS did not read {what} ({code})");
    }

    static uint Code(string four) => (uint)(four[0] << 24 | four[1] << 16 | four[2] << 8 | four[3]);

    [StructLayout(LayoutKind.Sequential)]
    struct Asbd
    {
        public double SampleRate;
        public uint FormatId, FormatFlags, BytesPerPacket, FramesPerPacket, BytesPerFrame, ChannelsPerFrame, BitsPerChannel, Reserved;
    }

    // AudioBufferList with its one AudioBuffer: the count, then at 8 (the buffer aligned as its pointer is) the
    // channels, the bytes and the data.
    [StructLayout(LayoutKind.Sequential)]
    struct BufferList
    {
        public uint Count;
        public AudioBuffer Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AudioBuffer
    {
        public uint Channels;
        public uint Bytes;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CFRange { public nint Location, Length; }

    [DllImport(Toolbox)] static extern int ExtAudioFileOpenURL(IntPtr url, out IntPtr file);
    [DllImport(Toolbox)] static extern int ExtAudioFileGetProperty(IntPtr file, uint id, ref uint size, ref Asbd data);
    [DllImport(Toolbox)] static extern int ExtAudioFileGetProperty(IntPtr file, uint id, ref uint size, ref long data);
    [DllImport(Toolbox)] static extern int ExtAudioFileSetProperty(IntPtr file, uint id, uint size, ref Asbd data);
    [DllImport(Toolbox)] static extern int ExtAudioFileRead(IntPtr file, ref uint frames, ref BufferList data);
    [DllImport(Toolbox)] static extern int ExtAudioFileDispose(IntPtr file);
    [DllImport(Toolbox)] static extern int AudioFileOpenURL(IntPtr url, sbyte permissions, uint typeHint, out IntPtr file);
    [DllImport(Toolbox)] static extern int AudioFileGetProperty(IntPtr file, uint id, ref uint size, out IntPtr data);
    [DllImport(Toolbox)] static extern int AudioFileClose(IntPtr file);
    [DllImport(Foundation)] static extern IntPtr CFURLCreateFromFileSystemRepresentation(IntPtr allocator, byte[] path, nint length, [MarshalAs(UnmanagedType.U1)] bool directory);
    [DllImport(Foundation)] static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string s, uint encoding);
    [DllImport(Foundation)] static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);
    [DllImport(Foundation)] static extern nuint CFGetTypeID(IntPtr cf);
    [DllImport(Foundation)] static extern nuint CFStringGetTypeID();
    [DllImport(Foundation)] static extern nint CFStringGetLength(IntPtr s);
    [DllImport(Foundation)] static extern void CFStringGetCharacters(IntPtr s, CFRange range, [Out] char[] buffer);
    [DllImport(Foundation)] static extern void CFRelease(IntPtr cf);
}
