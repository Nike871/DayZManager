namespace DayZLauncher.Core.Config;

/// <summary>Minimal serverDZ.cfg template (fields per the official BI "Server Config File" wiki
/// page), used only as a starting point when the user has no config file yet.</summary>
public static class DefaultServerConfig
{
    public const string Template = """
        hostname = "DayZ Server";
        password = "";
        passwordAdmin = "";
        enableWhitelist = 0;
        maxPlayers = 60;
        verifySignatures = 2;
        forceSameBuild = 1;
        disableVoN = 0;
        vonCodecQuality = 20;
        disable3rdPerson = 0;
        disableCrosshair = 0;
        serverTime = "SystemTime";
        serverTimeAcceleration = 4;
        serverNightTimeAcceleration = 1;
        serverTimePersistent = 0;
        guaranteedUpdates = 1;
        loginQueueConcurrentPlayers = 5;
        loginQueueMaxPlayers = 500;
        instanceId = 1;
        storageAutoFix = 1;

        class Missions
        {
            class DayZ
            {
                template="dayzOffline.chernarusplus";
            };
        };
        """;
}
