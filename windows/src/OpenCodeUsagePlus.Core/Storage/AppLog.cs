using System.Text;

namespace OpenCodeUsagePlus.Core.Storage;

/// <summary>
/// Tiny append-only log. A WinExe has no console, so this is the only diagnostic trail.
/// Never pass tokens, keys or raw credential JSON to these methods.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    /// <summary>Writes the full exception, stack trace included, so a crash report is actionable.</summary>
    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        try
        {
            AppPaths.EnsureExists();

            lock (Gate)
            {
                var file = new FileInfo(AppPaths.LogFile);
                if (file.Exists && file.Length > MaxBytes)
                {
                    File.Move(AppPaths.LogFile, AppPaths.LogFile + ".old", overwrite: true);
                }

                var text = message.Replace("\r\n", "\n").TrimEnd('\n');
                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {text}{Environment.NewLine}";
                File.AppendAllText(AppPaths.LogFile, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never break the app.
        }
    }
}
