using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DayZLauncher.Core.Steam;

public sealed record SteamDetectionResult(
    string? StableClientExe, string? ExperimentalClientExe,
    string? StableServerExe, string? ExperimentalServerExe);

/// <summary>Best-effort scan of Steam's default install locations and library folders for a DayZ
/// client/server install (Stable and Experimental). Purely a convenience for pre-filling paths -
/// if nothing is found the user just browses for the .exe manually, same as always.</summary>
public static class SteamInstallDetector
{
    /// <summary>The client is always launched via the BattlEye-wrapped DayZ_BE.exe (the same one
    /// Steam itself starts) - that's tried first, with the raw DayZ_x64.exe only as a fallback for
    /// the rare install that's missing it.</summary>
    public static SteamDetectionResult Detect()
    {
        string? stableClient = null, experimentalClient = null;
        string? stableServer = null, experimentalServer = null;

        foreach (var libraryRoot in GetLibraryRoots())
        {
            var common = Path.Combine(libraryRoot, "steamapps", "common");

            stableClient ??= FirstExisting(ClientExeCandidates(common, "DayZ"));

            experimentalClient ??= FirstExisting(
                ClientExeCandidates(common, "DayZ Exp")
                    .Concat(ClientExeCandidates(common, "DayZ Experimental"))
                    .Append(Path.Combine(common, "DayZ Exp", "DayZDiag_x64.exe"))
                    .ToArray());

            stableServer ??= FirstExisting(
                Path.Combine(common, "DayZServer", "DayZServer_x64.exe"),
                Path.Combine(common, "DayZ Server", "DayZServer_x64.exe"));

            experimentalServer ??= FirstExisting(
                Path.Combine(common, "DayZ Server Exp", "DayZServer_x64.exe"),
                Path.Combine(common, "DayZServer Exp", "DayZServer_x64.exe"),
                Path.Combine(common, "DayZ Server Experimental", "DayZServer_x64.exe"));
        }

        return new SteamDetectionResult(stableClient, experimentalClient, stableServer, experimentalServer);
    }

    private static string[] ClientExeCandidates(string commonDir, string folderName) =>
        new[] { Path.Combine(commonDir, folderName, "DayZ_BE.exe"), Path.Combine(commonDir, folderName, "DayZ_x64.exe") };

    private static string? FirstExisting(params string[] candidates) => candidates.FirstOrDefault(File.Exists);

    private static List<string> GetLibraryRoots()
    {
        var roots = new List<string>();
        var steamPath = GetSteamInstallPath();
        if (steamPath is null) return roots;

        roots.Add(steamPath);

        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            try
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdfPath), "\"path\"\\s*\"([^\"]+)\""))
                {
                    var path = m.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(path)) roots.Add(path);
                }
            }
            catch
            {
                // best effort
            }
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Steam's own install location, wherever the user actually put it - checked via the
    /// registry keys the Steam client itself maintains first, since plenty of installs live outside
    /// Program Files (e.g. moved to a dedicated drive). Falls back to the Program Files default only
    /// if the registry lookup comes up empty.</summary>
    private static string? GetSteamInstallPath()
    {
        var fromRegistry = GetSteamPathFromRegistry();
        if (fromRegistry is not null && Directory.Exists(fromRegistry)) return fromRegistry;

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string? GetSteamPathFromRegistry()
    {
        try
        {
            var path = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) as string
                       ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;

            // Steam writes SteamPath with forward slashes ("D:/SteamLibrary/Steam").
            return string.IsNullOrWhiteSpace(path) ? null : path.Replace('/', '\\');
        }
        catch
        {
            return null; // registry unavailable/inaccessible - fall back to the default paths
        }
    }
}
