namespace DayZLauncher.Core.Models;

/// <summary>All launch settings for one branch (Stable or Experimental). Stable and Experimental
/// are kept as fully independent profiles because they are normally two separate game/tool
/// installs with their own mod builds, server config and mission.</summary>
public sealed class BranchProfile
{
    public string ClientExePath { get; set; } = "";
    public string ServerExePath { get; set; } = "";
    public string ServerConfigPath { get; set; } = "";

    /// <summary>-profiles for the dedicated server - where it keeps BattlEye config and writes
    /// its .RPT/.ADM logs. Unlike the client, the server has no implicit default worth relying on.</summary>
    public string ServerProfilesPath { get; set; } = "";

    /// <summary>One entry per line: "@WorkshopMod" or a full folder path (e.g. a P:\ dev mod).</summary>
    public string Mods { get; set; } = "";

    /// <summary>Same format as <see cref="Mods"/>, passed via -serverMod (server-only mods).</summary>
    public string ServerMods { get; set; } = "";

    public int Port { get; set; } = 2302;
    public int CpuCount { get; set; }
    public int LimitFps { get; set; }

    public bool ClientNoSplash { get; set; }

    /// <summary>-skipIntro - skips the Bohemia/Steam intro logo videos on startup. Distinct from
    /// ClientNoSplash (-noSplash), which only removes the loading-screen image.</summary>
    public bool ClientSkipIntro { get; set; }

    public bool ClientNoPause { get; set; }
    public bool ClientWindow { get; set; }
    public bool ClientScriptDebug { get; set; }

    /// <summary>-world=empty - loads an empty world in the main menu instead of the usual one,
    /// which normally speeds up client startup.</summary>
    public bool ClientWorldEmpty { get; set; }

    /// <summary>-name= - runs the client under a separate profile, so its settings/keybinds/logs
    /// don't collide with your regular play profile. Blank = default profile.</summary>
    public string ClientProfileName { get; set; } = "";

    public bool ServerDoLogs { get; set; } = true;
    public bool ServerAdminLog { get; set; } = true;
    public bool ServerNetLog { get; set; }
    public bool ServerScriptDebug { get; set; }
    public bool ServerFilePatching { get; set; }
    public bool ServerFreezeCheck { get; set; }

    /// <summary>-BEpath= - overrides where the server looks for its BattlEye folder. Blank = default.</summary>
    public string ServerBEPath { get; set; } = "battleye";

    /// <summary>-storage= - overrides the server's storage root folder. Blank = default.</summary>
    public string ServerStoragePath { get; set; } = "";

    public string ConnectIp { get; set; } = "127.0.0.1";
    public string ConnectPort { get; set; } = "2302";
    public string ConnectPassword { get; set; } = "";

    /// <summary>When on (the default), IP/Port for direct connect are derived from "localhost" and
    /// this profile's own server Port instead of being freely editable - the common case of testing
    /// against the server you're about to launch yourself.</summary>
    public bool UseLocalServerConnect { get; set; } = true;

    public string ExtraClientArgs { get; set; } = "";
    public string ExtraServerArgs { get; set; } = "";

    /// <summary>When true, starting the server also starts the client after the configured delay -
    /// the "+" toggle between the server/client launch buttons.</summary>
    public bool ChainClientAfterServerLaunch { get; set; }
}
