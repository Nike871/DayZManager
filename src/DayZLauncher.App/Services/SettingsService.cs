using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DayZLauncher.Core.Models;

namespace DayZLauncher.App.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DayZManager",
        "settings.json");

    public static bool Exists() => File.Exists(SettingsPath);

    public static LauncherSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions) ?? LauncherSettings.CreateDefault();
            }
        }
        catch
        {
            // corrupt/unreadable settings file - fall back to defaults
        }

        return LauncherSettings.CreateDefault();
    }

    public static void Save(LauncherSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // best-effort persistence only
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
        }
        catch
        {
            // best-effort only
        }
    }
}
