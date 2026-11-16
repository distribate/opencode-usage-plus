namespace OpenCodeUsagePlus.Core.Storage;

public static class AppPaths
{
    public const string ProductName = "OpenCode Usage Plus";

    public const string FolderName = "OpenCode Usage Plus";

    public static string AppDataDirectory { get; } = ResolveAppDataDirectory();

    public static string HistoryFile => Path.Combine(AppDataDirectory, "history.json");

    public static string SettingsFile => Path.Combine(AppDataDirectory, "settings.json");

    public static string ApiKeyFile => Path.Combine(AppDataDirectory, "apikey.bin");

    public static string LogFile => Path.Combine(AppDataDirectory, "log.txt");

    public static void EnsureExists() => Directory.CreateDirectory(AppDataDirectory);

    private static string ResolveAppDataDirectory()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            local = Path.GetTempPath();
        }

        return Path.Combine(local, FolderName);
    }
}
