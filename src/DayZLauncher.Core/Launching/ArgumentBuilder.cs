using System.Text;
using DayZLauncher.Core.Models;

namespace DayZLauncher.Core.Launching;

public static class ArgumentBuilder
{
    public static List<string> BuildClientArgs(BranchProfile p)
    {
        var args = new List<string>();

        var mods = JoinList(p.Mods);
        if (mods.Length > 0) args.Add($"-mod={mods}");

        if (!string.IsNullOrWhiteSpace(p.ClientProfileName)) args.Add($"-name={p.ClientProfileName}");

        if (!string.IsNullOrWhiteSpace(p.ConnectIp))
        {
            args.Add($"-connect={p.ConnectIp}");
            if (!string.IsNullOrWhiteSpace(p.ConnectPort)) args.Add($"-port={p.ConnectPort}");
            if (!string.IsNullOrWhiteSpace(p.ConnectPassword)) args.Add($"-password={p.ConnectPassword}");
        }

        if (p.CpuCount > 0) args.Add($"-cpuCount={p.CpuCount}");

        if (p.ClientNoSplash) args.Add("-noSplash");
        if (p.ClientSkipIntro) args.Add("-skipIntro");
        if (p.ClientWorldEmpty) args.Add("-world=empty");
        if (p.ClientNoPause) args.Add("-noPause");
        if (p.ClientWindow) args.Add("-window");
        if (p.ClientScriptDebug) args.Add("-scriptDebug");

        AppendExtra(args, p.ExtraClientArgs);
        return args;
    }

    public static List<string> BuildServerArgs(BranchProfile p)
    {
        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(p.ServerBEPath)) args.Add($"-BEpath={p.ServerBEPath}");
        if (!string.IsNullOrWhiteSpace(p.ServerStoragePath)) args.Add($"-storage={p.ServerStoragePath}");
        if (!string.IsNullOrWhiteSpace(p.ServerConfigPath)) args.Add($"-config={p.ServerConfigPath}");
        if (!string.IsNullOrWhiteSpace(p.ServerProfilesPath)) args.Add($"-profiles={p.ServerProfilesPath}");
        args.Add($"-port={p.Port}");
        if (p.CpuCount > 0) args.Add($"-cpuCount={p.CpuCount}");
        if (p.LimitFps > 0) args.Add($"-limitFPS={p.LimitFps}");

        var mods = JoinList(p.Mods);
        if (mods.Length > 0) args.Add($"-mod={mods}");

        var serverMods = JoinList(p.ServerMods);
        if (serverMods.Length > 0) args.Add($"-serverMod={serverMods}");

        if (p.ServerDoLogs) args.Add("-doLogs");
        if (p.ServerAdminLog) args.Add("-adminLog");
        if (p.ServerNetLog) args.Add("-netLog");
        if (p.ServerScriptDebug) args.Add("-scriptDebug");
        if (p.ServerFilePatching) args.Add("-filePatching");
        if (p.ServerFreezeCheck) args.Add("-freezeCheck");

        AppendExtra(args, p.ExtraServerArgs);
        return args;
    }

    public static string ToDisplayString(IEnumerable<string> args)
    {
        var sb = new StringBuilder();
        foreach (var a in args)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(a.Contains(' ') ? $"\"{a}\"" : a);
        }
        return sb.ToString();
    }

    /// <summary>Splits a mod textbox's one-entry-per-line text into individual mod entries
    /// (@WorkshopName or a full folder path), skipping blanks and //-comments. Public because the
    /// Bikey-copy launch step needs the same list independently of building -mod=.</summary>
    public static IEnumerable<string> SplitModList(string multilineText)
    {
        if (string.IsNullOrWhiteSpace(multilineText)) return Enumerable.Empty<string>();
        return multilineText
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("//") && !l.StartsWith('#'));
    }

    private static string JoinList(string multilineText) => string.Join(";", SplitModList(multilineText));

    private static void AppendExtra(List<string> args, string extra)
    {
        if (string.IsNullOrWhiteSpace(extra)) return;
        args.AddRange(SplitArgs(extra));
    }

    private static IEnumerable<string> SplitArgs(string s)
    {
        var current = new StringBuilder();
        var inQuotes = false;
        foreach (var c in s)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0) yield return current.ToString();
    }
}
