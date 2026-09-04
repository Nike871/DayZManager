using System.Diagnostics;

namespace DayZLauncher.Core.Launching;

/// <summary>Tracks launched client/server processes by an arbitrary string key (e.g. "Stable-client",
/// "Experimental-server") so Stable and Experimental instances can run side by side without
/// colliding with each other's start/stop state.</summary>
public sealed class GameProcessManager
{
    private readonly Dictionary<string, Process> _processes = new();

    public bool IsRunning(string key) => _processes.TryGetValue(key, out var p) && !HasExitedSafe(p);

    public void Start(string key, string exePath, IEnumerable<string> args)
    {
        if (IsRunning(key)) throw new InvalidOperationException("Процесс уже запущен.");
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException("Исполняемый файл не найден.", exePath);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? "",
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var process = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить процесс.");
        _processes[key] = process;
    }

    public void Stop(string key)
    {
        if (!_processes.TryGetValue(key, out var process)) return;
        try
        {
            if (!HasExitedSafe(process)) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // best effort
        }
        finally
        {
            _processes.Remove(key);
        }
    }

    private static readonly string[] ClientProcessNames = { "DayZ_BE", "DayZ_x64" };
    private static readonly string[] ServerProcessNames = { "DayZServer_x64" };

    /// <summary>Stops the client tracked under <paramref name="key"/> (if any) AND sweeps the system
    /// for DayZ_BE/DayZ_x64 by name - same reasoning as StopClient: the tracked handle is often
    /// DayZ_BE's own launcher process, which BattlEye can already have exited by the time the real
    /// game process is running, so killing only the tracked handle leaves the actual client alive.</summary>
    public void StopClient(string key)
    {
        Stop(key);
        KillProcessesByName(ClientProcessNames);
    }

    /// <summary>Same idea as <see cref="StopClient"/>, for the server.</summary>
    public void StopServer(string key)
    {
        Stop(key);
        KillProcessesByName(ServerProcessNames);
    }

    /// <summary>Kills every process this manager is currently tracking, regardless of branch or
    /// client/server, AND sweeps the whole system for any DayZ client/server process by name - the
    /// "Принудительная остановка" panic button. The name sweep catches copies started outside this
    /// app (e.g. launched straight from Steam) or ones this app lost track of (e.g. after being
    /// restarted while a server was still running).</summary>
    public void StopAll()
    {
        foreach (var key in _processes.Keys.ToList())
            Stop(key);

        KillProcessesByName(ClientProcessNames);
        KillProcessesByName(ServerProcessNames);
    }

    private static void KillProcessesByName(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best effort - e.g. no permission, already exiting
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    /// <summary>True if a DayZ client process (DayZ_BE, the BattlEye-wrapped launcher, or DayZ_x64,
    /// the actual game) is running anywhere on the machine - whether or not this manager itself
    /// launched it. Used to detect a copy started outside the app (straight from Steam, or left
    /// running from before the app was restarted) so its settings can be treated as "in use" the same
    /// as one this manager tracks.
    ///
    /// Deliberately name-only, with no attempt to also verify it's running from
    /// <paramref name="clientExePath"/>'s specific install folder: an earlier version tried to check
    /// that via Process.MainModule, but BattlEye (which wraps every DayZ client/server process) denies
    /// reading another process's module info, so that check silently always failed and this never
    /// detected anything real - only an unprotected stand-in process in testing. Matching the exact
    /// same name-only technique <see cref="KillAllKnownDayZProcessesByName"/> already uses (and which
    /// already works reliably against the real, protected game) trades away branch-level precision
    /// (Stable vs Experimental) for actually working.</summary>
    public static bool IsExternalClientRunning(string clientExePath) =>
        !string.IsNullOrWhiteSpace(clientExePath) && (AnyProcessNamed("DayZ_BE") || AnyProcessNamed("DayZ_x64"));

    /// <summary>Same idea as <see cref="IsExternalClientRunning"/>, for the server process.</summary>
    public static bool IsExternalServerRunning(string serverExePath) =>
        !string.IsNullOrWhiteSpace(serverExePath) && AnyProcessNamed("DayZServer_x64");

    private static bool AnyProcessNamed(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    private static bool HasExitedSafe(Process p)
    {
        try { return p.HasExited; }
        catch { return true; }
    }
}
