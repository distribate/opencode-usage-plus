using System.Runtime.InteropServices;
using System.Text;

namespace OpenCodeUsagePlus.Core.Storage;

/// <summary>
/// Writes a self-contained crash report next to the log. Reports never contain credentials: the
/// caller only passes exceptions and environment facts.
/// </summary>
public static class CrashReporter
{
    public static string Report(string source, Exception? exception)
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(AppPaths.AppDataDirectory, $"crash-{stamp}.txt");
        var report = new StringBuilder();

        report.AppendLine("OpenCode Usage Plus — crash report");
        report.AppendLine($"time:     {DateTimeOffset.Now:O}");
        report.AppendLine($"source:   {source}");
        report.AppendLine($"version:  {typeof(CrashReporter).Assembly.GetName().Version}");
        report.AppendLine($"os:       {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        report.AppendLine($"runtime:  {RuntimeInformation.FrameworkDescription}");
        report.AppendLine($"data:     {AppPaths.AppDataDirectory}");
        report.AppendLine();
        report.AppendLine(exception?.ToString() ?? "(no exception object)");
        report.AppendLine();

        try
        {
            AppPaths.EnsureExists();
            File.WriteAllText(path, report.ToString(), Encoding.UTF8);
        }
        catch
        {
            // The log entry below is the fallback.
        }

        AppLog.Error($"crash report written: {path}", exception);
        return path;
    }
}
