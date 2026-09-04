namespace DayZLauncher.Core.Mods;

/// <summary>Copies each dev mod's signature key(s) into the server's keys folder before launch, so
/// the server accepts clients running that mod. Only mods given as a real folder path are handled -
/// "@WorkshopName" entries are skipped because a subscribed Workshop mod's key is already registered
/// by Steam on both ends.</summary>
public static class ModKeyCopier
{
    public static void CopyModKeys(IEnumerable<string> modPaths, string serverExeDir)
    {
        if (string.IsNullOrWhiteSpace(serverExeDir)) return;

        foreach (var modPath in modPaths)
        {
            if (string.IsNullOrWhiteSpace(modPath) || modPath.StartsWith('@')) continue;

            var keysDir = FindKeysDirectory(modPath);
            if (keysDir is null) continue;

            string destKeysDir;
            try
            {
                destKeysDir = Path.Combine(serverExeDir, "keys");
                Directory.CreateDirectory(destKeysDir);
            }
            catch
            {
                continue; // best effort - can't create the destination, skip this mod
            }

            foreach (var bikey in Directory.EnumerateFiles(keysDir, "*.bikey"))
            {
                try
                {
                    File.Copy(bikey, Path.Combine(destKeysDir, Path.GetFileName(bikey)), overwrite: true);
                }
                catch
                {
                    // best effort - e.g. file locked by a running server
                }
            }
        }
    }

    /// <summary>Deletes each mod's signature key(s) from the server's keys folder - the inverse of
    /// <see cref="CopyModKeys"/>, run when a dev mod is removed from a mods list so a stale key
    /// doesn't linger on the server after the mod is gone.</summary>
    public static void RemoveModKeys(IEnumerable<string> modPaths, string serverExeDir)
    {
        if (string.IsNullOrWhiteSpace(serverExeDir)) return;

        var destKeysDir = Path.Combine(serverExeDir, "keys");
        if (!Directory.Exists(destKeysDir)) return;

        foreach (var modPath in modPaths)
        {
            if (string.IsNullOrWhiteSpace(modPath) || modPath.StartsWith('@')) continue;

            var keysDir = FindKeysDirectory(modPath);
            if (keysDir is null) continue;

            foreach (var bikey in Directory.EnumerateFiles(keysDir, "*.bikey"))
            {
                try
                {
                    var destFile = Path.Combine(destKeysDir, Path.GetFileName(bikey));
                    if (File.Exists(destFile)) File.Delete(destFile);
                }
                catch
                {
                    // best effort - e.g. file locked by a running server
                }
            }
        }
    }

    /// <summary>Finds the mod's key folder regardless of casing ("keys" or "Keys" - DayZ mods use
    /// both conventions) - a plain Path.Combine(modPath, "keys") only matched an exact-case folder,
    /// which silently skipped mods that ship a capitalized "Keys" directory on a case-sensitive
    /// volume.</summary>
    private static string? FindKeysDirectory(string modPath)
    {
        try
        {
            return Directory.EnumerateDirectories(modPath)
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), "keys", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null; // mod folder missing or inaccessible
        }
    }
}
