using System.Diagnostics;

namespace DayZLauncher.Core.Diagnostics;

/// <summary>Reads the installed game version straight off the executable's file version resource,
/// rather than trying to parse it from anywhere else - Steam doesn't expose it to a manifest DayZ
/// Manager can read, and the .exe itself is always right there next to whatever path the user
/// configured (client/server launcher exe), so this just looks the fixed file name up in that same
/// folder.</summary>
public static class GameVersionReader
{
    public static string? GetVersion(string? configuredExePath, string targetFileName)
    {
        if (string.IsNullOrWhiteSpace(configuredExePath)) return null;

        var dir = Path.GetDirectoryName(Path.GetFullPath(configuredExePath));
        if (dir is null) return null;

        var targetPath = Path.Combine(dir, targetFileName);
        if (!File.Exists(targetPath)) return null;

        try
        {
            var info = FileVersionInfo.GetVersionInfo(targetPath);
            return $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
        }
        catch
        {
            return null;
        }
    }
}
