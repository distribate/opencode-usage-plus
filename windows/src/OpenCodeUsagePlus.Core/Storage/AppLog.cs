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

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}: {exception.GetType().Name}: {exception.Message}");

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

                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
                File.AppendAllText(AppPaths.LogFile, line);
            }
        }
        catch
        {
            // Logging must never break the app.
        }
    }
}
