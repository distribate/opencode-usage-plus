using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using OpenCodeUsagePlus.Core.Models;
using OpenCodeUsagePlus.Core.Services;
using OpenCodeUsagePlus.Core.Storage;

namespace OpenCodeUsagePlus;

/// <summary>
/// <c>--diagnose</c>: writes an environment/credential/API report next to the log and opens it.
/// The report never contains the token itself, only its length.
/// </summary>
internal static class Diagnostics
{
    public static int Run()
    {
        var report = new StringBuilder();
        var startedAt = DateTimeOffset.Now;

        report.AppendLine("OpenCode Usage Plus — diagnostics");
        report.AppendLine($"time:        {startedAt:O}");
        report.AppendLine($"version:     {typeof(Diagnostics).Assembly.GetName().Version}");
        report.AppendLine($"os:          {Environment.OSVersion}");
        report.AppendLine($"runtime:     {Environment.Version} ({System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription})");
        report.AppendLine($"arch:        {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        report.AppendLine($"executable:  {Environment.ProcessPath}");
        report.AppendLine($"data:        {AppPaths.AppDataDirectory}");
        report.AppendLine($"plainWindow: {AppEnvironment.PlainWindow}");
        report.AppendLine();

        report.AppendLine("settings:");
        var settings = AppSettings.Load();
        report.AppendLine($"  path:               {settings.PathOnDisk} (exists: {File.Exists(settings.PathOnDisk)})");
        report.AppendLine($"  iconStyle:          {settings.IconStyle}");
        report.AppendLine($"  launchAtLogin:      {settings.LaunchAtLogin}");
        report.AppendLine($"  notifications:      {settings.NotificationsEnabled}");
        report.AppendLine($"  firstRunHintShown:  {settings.FirstRunHintShown}");
        report.AppendLine($"  savedApiKey:        {(new SecretStore().Read() is null ? "no" : "yes")}");
        report.AppendLine();

        var store = new OpenCodeCredentialStore();
        report.AppendLine("opencode database:");
        report.AppendLine($"  data directory: {store.DataDirectory} (exists: {Directory.Exists(store.DataDirectory)})");
        var candidates = store.CandidateDatabasePaths();
        if (candidates.Count == 0)
        {
            report.AppendLine("  candidates:     none");
        }

        foreach (var path in candidates)
        {
            var exists = File.Exists(path);
            var write = exists ? File.GetLastWriteTime(path).ToString("O") : "-";
            report.AppendLine($"  - {path} (exists: {exists}, modified: {write})");
        }

        report.AppendLine();
        report.AppendLine("credential:");

        AuthCredential? credential = null;
        try
        {
            credential = store.TryRead();
        }
        catch (Exception exception)
        {
            report.AppendLine($"  read failed: {exception.GetType().Name}: {exception.Message}");
        }

        if (credential is null)
        {
            report.AppendLine("  not found");
        }
        else
        {
            report.AppendLine($"  kind:    {credential.Kind}");
            report.AppendLine($"  source:  {credential.SourcePath ?? "unknown"}");
            report.AppendLine($"  orgID:   {credential.OrganizationId ?? "-"}");
            report.AppendLine($"  token:   present, length {credential.Token.Length}");
        }

        report.AppendLine();
        report.AppendLine("history:");
        try
        {
            var history = new UsageHistoryStore().Load(DateTimeOffset.Now);
            report.AppendLine($"  file:    {AppPaths.HistoryFile} (exists: {File.Exists(AppPaths.HistoryFile)})");
            report.AppendLine($"  samples: {history.Count}");
            if (history.Count > 0)
            {
                report.AppendLine($"  first:   {history[0].Timestamp:O} rolling={history[0].Rolling} weekly={history[0].Weekly} monthly={history[0].Monthly}");
                report.AppendLine($"  last:    {history[^1].Timestamp:O} rolling={history[^1].Rolling} weekly={history[^1].Weekly} monthly={history[^1].Monthly}");
            }
        }
        catch (Exception exception)
        {
            report.AppendLine($"  load failed: {exception.GetType().Name}: {exception.Message}");
        }

        report.AppendLine();
        report.AppendLine("usage api:");
        if (credential is null)
        {
            report.AppendLine("  skipped (no credential)");
        }
        else
        {
            try
            {
                using var api = new UsageApiClient();
                var stats = api.FetchAsync(credential).GetAwaiter().GetResult();
                report.AppendLine($"  ok:      rolling={stats.Rolling.Percent}% weekly={stats.Weekly.Percent}% monthly={stats.Monthly.Percent}%");
                report.AppendLine($"  resets:  rolling={stats.Rolling.ResetsAt:O} weekly={stats.Weekly.ResetsAt:O} monthly={stats.Monthly.ResetsAt:O}");
            }
            catch (UsageApiException exception)
            {
                report.AppendLine($"  error:   {exception.Kind}: {exception.Message}");
            }
            catch (Exception exception)
            {
                report.AppendLine($"  error:   {exception.GetType().Name}: {exception.Message}");
            }
        }

        report.AppendLine();
        report.AppendLine("recent log:");
        try
        {
            if (File.Exists(AppPaths.LogFile))
            {
                var lines = File.ReadAllLines(AppPaths.LogFile);
                var start = Math.Max(0, lines.Length - 60);
                for (var index = start; index < lines.Length; index++)
                {
                    report.AppendLine("  " + lines[index]);
                }
            }
            else
            {
                report.AppendLine("  (no log file)");
            }
        }
        catch (Exception exception)
        {
            report.AppendLine($"  cannot read log: {exception.Message}");
        }

        var reportPath = Path.Combine(AppPaths.AppDataDirectory, $"diagnose-{startedAt:yyyyMMdd-HHmmss}.txt");
        try
        {
            AppPaths.EnsureExists();
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
            Process.Start(new ProcessStartInfo { FileName = reportPath, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            AppLog.Error("diagnostics failed", exception);
            return 1;
        }

        AppLog.Info($"diagnostics written: {reportPath}");
        return 0;
    }
}
