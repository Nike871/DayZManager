using System.Text;

namespace DayZLauncher.Core.Logs;

public sealed class LogFileInfo
{
    public string FullPath { get; init; } = "";
    public string FileName { get; init; } = "";
    public DateTime LastWriteTimeUtc { get; init; }
}

public static class LogFileService
{
    private static readonly string[] Extensions = { ".rpt", ".adm", ".log", ".txt" };

    public static List<LogFileInfo> ListLogFiles(string directory)
    {
        var result = new List<LogFileInfo>();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return result;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (!Extensions.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
                var info = new FileInfo(file);
                result.Add(new LogFileInfo
                {
                    FullPath = file,
                    FileName = info.Name,
                    LastWriteTimeUtc = info.LastWriteTimeUtc
                });
            }
        }
        catch
        {
            // best effort - directory may vanish/be locked between calls
        }

        result.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
        return result;
    }

    private static readonly string[] DeletableExtensions = { ".rpt", ".log", ".adm" };

    /// <summary>Deletes .RPT/.log/.ADM files from a folder, best-effort. Used for the "clear logs
    /// before launch" options.</summary>
    public static void DeleteLogFiles(string directory) => DeleteFilesWithExtensions(directory, DeletableExtensions);

    /// <summary>Deletes every file the log viewer lists (.RPT/.ADM/.log/.txt), best-effort. Used by
    /// the log window's "Удалить логи" button, so it clears exactly what's shown on screen.</summary>
    public static void DeleteAllLogFiles(string directory) => DeleteFilesWithExtensions(directory, Extensions);

    private static void DeleteFilesWithExtensions(string directory, string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (!extensions.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
                try { File.Delete(file); }
                catch { /* best effort - file may be locked */ }
            }
        }
        catch
        {
            // best effort
        }
    }
}

/// <summary>Incrementally reads text appended to a growing log file since the last call, so the UI
/// can "tail -f" it without re-reading the whole file every tick.</summary>
public sealed class LogTailer
{
    /// <summary>Caps the very first read after <see cref="Reset"/> so selecting a huge, actively
    /// growing server log (an .ADM/.RPT can reach hundreds of MB over a long session) doesn't read
    /// it from byte 0 on the UI thread - that synchronous read could stall the whole window for
    /// several seconds, which looked like the log window (including its Client/Server toggle) had
    /// simply stopped responding while the server was running and writing to a large file.</summary>
    private const long MaxInitialReadBytes = 2 * 1024 * 1024;

    private long _position;
    private string? _path;

    public void Reset(string? path)
    {
        _path = path;
        _position = 0;

        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var length = new FileInfo(path).Length;
            if (length > MaxInitialReadBytes) _position = length - MaxInitialReadBytes;
        }
        catch
        {
            // best effort - fall back to reading from the start
        }
    }

    public string ReadNewText()
    {
        if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path)) return "";

        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length < _position) _position = 0; // file was truncated/recreated since last read
            stream.Seek(_position, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = reader.ReadToEnd();
            _position = stream.Position;
            return text;
        }
        catch
        {
            return "";
        }
    }
}
