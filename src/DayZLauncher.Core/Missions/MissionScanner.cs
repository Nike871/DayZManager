namespace DayZLauncher.Core.Missions;

public static class MissionScanner
{
    public static List<string> ScanMissionFolders(string serverExePath)
    {
        var result = new List<string>();
        var mpMissionsDir = GetMpMissionsDirectory(serverExePath);
        if (mpMissionsDir is null) return result;

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(mpMissionsDir))
                result.Add(Path.GetFileName(dir));
        }
        catch
        {
            // best effort
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public static string? GetMpMissionsDirectory(string serverExePath)
    {
        if (string.IsNullOrWhiteSpace(serverExePath)) return null;

        string? serverDir;
        try { serverDir = Path.GetDirectoryName(Path.GetFullPath(serverExePath)); }
        catch { return null; }

        if (serverDir is null) return null;
        var dir = Path.Combine(serverDir, "mpmissions");
        return Directory.Exists(dir) ? dir : null;
    }
}
