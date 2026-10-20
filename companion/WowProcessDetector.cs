using System.ComponentModel;
using System.Diagnostics;

namespace RealmlyCompanion;

internal enum WowRunState { Stopped, Running, Unknown }

internal static class WowProcessDetector
{
    private static readonly string[] ProcessNames = ["Wow", "WowT", "WowB", "WowClassic", "WowClassicT", "WowClassicB", "Wow-64", "WowForever"];

    public static WowRunState Check(string gameRoot, ClientKind client)
    {
        var paths = new List<string?>();
        try
        {
            foreach (var name in ProcessNames)
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try { paths.Add(process.MainModule?.FileName); }
                    catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException)
                    {
                        // Exited processes are harmless; inaccessible running clients are not assumed safe.
                        try { if (!process.HasExited) paths.Add(null); }
                        catch (Exception retry) when (retry is Win32Exception or InvalidOperationException) { paths.Add(null); }
                    }
                }
            }
            return Evaluate(gameRoot, client, paths);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or ArgumentException or IOException)
        { return WowRunState.Unknown; }
    }

    internal static WowRunState Evaluate(string gameRoot, ClientKind client, IEnumerable<string?> executablePaths)
    {
        if (string.IsNullOrWhiteSpace(gameRoot)) return WowRunState.Unknown;
        var directory = Path.GetFullPath(Path.Combine(gameRoot, WowFiles.ClientFolder(client))).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var unknown = false;
        foreach (var path in executablePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) { unknown = true; continue; }
            if (Path.GetFullPath(path).StartsWith(directory, StringComparison.OrdinalIgnoreCase)) return WowRunState.Running;
        }
        return unknown ? WowRunState.Unknown : WowRunState.Stopped;
    }

    internal static void RequireStopped(WowRunState state)
    {
        if (state != WowRunState.Stopped) throw new InvalidOperationException(state == WowRunState.Running
            ? "Close the selected WoW client before installing or updating the addon."
            : "Cannot verify that WoW is closed. Close all WoW clients, then try again.");
    }
}
