using DayZLauncher.Core.Models;

namespace DayZLauncher.Core.Logs;

/// <summary>Where DayZ itself keeps client/server logs, per branch - shared by the log viewer and
/// by the "clear logs before launch" cleanup so both use exactly the same folder.</summary>
public static class DayZPaths
{
    public static string GetClientLogDirectory(Branch branch)
    {
        var folderName = branch == Branch.Experimental ? "DayZ Exp" : "DayZ";
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), folderName);
    }

    public static string GetServerLogDirectory(BranchProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.ServerProfilesPath)) return profile.ServerProfilesPath;
        if (string.IsNullOrWhiteSpace(profile.ServerExePath)) return "";

        var serverDir = Path.GetDirectoryName(Path.GetFullPath(profile.ServerExePath));
        return serverDir is null ? "" : Path.Combine(serverDir, "profiles");
    }
}
