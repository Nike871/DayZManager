using System.Text.RegularExpressions;

namespace DayZLauncher.Core.Config;

/// <summary>Reads/edits a serverDZ.cfg file by patching known keys in place with regex, instead of
/// parsing the whole Enfusion-config grammar. This keeps every line the server admin already has
/// (comments, motd arrays, custom classes) untouched - only the specific key being edited changes.</summary>
public sealed class ServerConfigDocument
{
    public string RawText { get; set; }

    public ServerConfigDocument(string rawText)
    {
        RawText = rawText;
    }

    public static ServerConfigDocument CreateDefault() => new(DefaultServerConfig.Template);

    public static ServerConfigDocument Load(string path) => new(File.ReadAllText(path));

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, RawText);
    }

    public string? GetRawValue(string key)
    {
        var match = KeyRegex(key).Match(RawText);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public bool HasKey(string key) => GetRawValue(key) is not null;

    public string GetString(string key, string fallback = "") => GetRawValue(key) is { } raw ? Unquote(raw) : fallback;

    public int GetInt(string key, int fallback = 0)
        => GetRawValue(key) is { } raw && int.TryParse(raw, out var v) ? v : fallback;

    /// <summary>Like <see cref="GetInt"/> but returns null instead of a guessed fallback when the key
    /// is absent, so a quick-edit TextBox can show a genuinely empty box rather than a made-up value.</summary>
    public int? GetIntOrNull(string key)
        => GetRawValue(key) is { } raw && int.TryParse(raw, out var v) ? v : null;

    public bool GetBool(string key, bool fallback = false)
        => GetRawValue(key)?.Trim() switch { "1" => true, "0" => false, _ => fallback };

    public void SetString(string key, string value) => SetRaw(key, Quote(value));
    public void SetInt(string key, int value) => SetRaw(key, value.ToString());
    public void SetBool(string key, bool value) => SetRaw(key, value ? "1" : "0");

    /// <summary>Writes the key when the value is present, or removes it entirely when cleared back to
    /// null - this is how a quick-edit int? TextBox represents "the admin touched this and blanked it
    /// back out" as opposed to "never touched", since an int field has no other way to mean "absent".</summary>
    public void SetIntOrRemove(string key, int? value)
    {
        if (value.HasValue) SetInt(key, value.Value);
        else RemoveKey(key);
    }

    public void RemoveKey(string key) => RawText = FullLineRegex(key).Replace(RawText, "", 1);

    private void SetRaw(string key, string rawValue)
    {
        var match = KeyRegex(key).Match(RawText);
        if (match.Success)
        {
            var group = match.Groups[1];
            RawText = RawText[..group.Index] + rawValue + RawText[(group.Index + group.Length)..];
        }
        else
        {
            var line = $"{key} = {rawValue};{Environment.NewLine}";
            var classIndex = RawText.IndexOf("class ", StringComparison.Ordinal);
            RawText = classIndex >= 0 ? RawText.Insert(classIndex, line) : RawText.TrimEnd() + Environment.NewLine + line;
        }
    }

    public string? GetMissionTemplate()
    {
        var match = MissionRegex().Match(RawText);
        return match.Success ? match.Groups[1].Value : null;
    }

    public void SetMissionTemplate(string template)
    {
        var match = MissionRegex().Match(RawText);
        if (match.Success)
        {
            var group = match.Groups[1];
            RawText = RawText[..group.Index] + template + RawText[(group.Index + group.Length)..];
        }
        else
        {
            var nl = Environment.NewLine;
            var block = "class Missions" + nl + "{" + nl + "    class DayZ" + nl + "    {" + nl
                + $"        template=\"{template}\";" + nl + "    };" + nl + "};";
            RawText = RawText.TrimEnd() + nl + nl + block + nl;
        }
    }

    private static Regex KeyRegex(string key) =>
        new(@"(?m)^\s*" + Regex.Escape(key) + @"\s*=\s*(""[^""]*""|[^;\r\n]+)\s*;");

    private static Regex FullLineRegex(string key) =>
        new(@"(?m)^[ \t]*" + Regex.Escape(key) + @"\s*=\s*(?:""[^""]*""|[^;\r\n]+)\s*;[ \t]*(?://[^\r\n]*)?\r?\n?");

    private static Regex MissionRegex() =>
        new(@"class\s+Missions\b.*?class\s+DayZ\b.*?template\s*=\s*""([^""]*)""", RegexOptions.Singleline);

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static string Unquote(string raw)
    {
        raw = raw.Trim();
        return raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"'
            ? raw[1..^1].Replace("\\\"", "\"")
            : raw;
    }
}
