using System.Diagnostics;

namespace Dettato;

/// <summary>What Lily#'s own command line says, where Lily# tells us where it is: the environment variable
/// LILYSHARP_LYSC_PATH, set when Lily#'s Convert Recording runs dettato (Lily# REQUESTS 9, 2026-11-16). Nothing of
/// Lily#'s rules is copied here (the owner: no code twice); without the variable, or when lysc fails, nothing.</summary>
static class Lysc
{
    /// <summary>The guitar shape Lily#'s editor writes first for a chord name (`lysc shape D` → "D\txx0232"), or
    /// null.</summary>
    public static string? Shape(string name)
    {
        string? lysc = Environment.GetEnvironmentVariable("LILYSHARP_LYSC_PATH");
        if (string.IsNullOrEmpty(lysc) || !File.Exists(lysc)) return null;
        try
        {
            var start = new ProcessStartInfo(lysc) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("shape");
            start.ArgumentList.Add(name);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(20_000))
            {
                process.Kill(true);
                return null;
            }
            if (process.ExitCode != 0) return null;
            foreach (string line in output.Result.Split('\n'))
            {
                string[] parts = line.TrimEnd('\r').Split('\t');
                if (parts.Length == 2 && parts[0] == name) return parts[1].Length > 0 ? parts[1] : null;
            }
        }
        catch (Exception) { }
        return null;
    }
}
