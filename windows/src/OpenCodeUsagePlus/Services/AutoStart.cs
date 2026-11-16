using System;
using Microsoft.Win32;

namespace OpenCodeUsagePlus.Services;

/// <summary>HKCU Run registration — the Windows equivalent of SMAppService.</summary>
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OpenCodeUsagePlus";

    public static bool IsSupported => OperatingSystem.IsWindows();

    public static string CommandLine
    {
        get
        {
            var executable = Environment.ProcessPath;
            return string.IsNullOrEmpty(executable) ? string.Empty : $"\"{executable}\" --tray";
        }
    }

    public static bool IsEnabled()
    {
        if (!IsSupported)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value
                   && string.Equals(value.Trim(), CommandLine, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool SetEnabled(bool enabled)
    {
        if (!IsSupported)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                key.SetValue(ValueName, CommandLine);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
