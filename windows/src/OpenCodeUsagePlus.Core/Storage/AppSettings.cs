using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCodeUsagePlus.Core.Models;

namespace OpenCodeUsagePlus.Core.Storage;

/// <summary>User preferences. Never contains secrets.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _path;

    public AppSettings(string? path = null) => _path = path ?? AppPaths.SettingsFile;

    public TrayIconStyle IconStyle { get; set; } = TrayIconStyle.Ring;

    public bool ProgressAccent { get; set; }

    public bool NotificationsEnabled { get; set; } = true;

    public bool LaunchAtLogin { get; set; }

    public bool FirstRunHintShown { get; set; }

    public string PathOnDisk => _path;

    public static AppSettings Load(string? path = null)
    {
        var settings = new AppSettings(path);
        try
        {
            if (!File.Exists(settings._path))
            {
                return settings;
            }

            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(settings._path), JsonOptions);
            if (dto is not null)
            {
                settings.Apply(dto);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLog.Warn($"settings load failed: {ex.Message}");
        }

        return settings;
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureExists();
            var dto = new SettingsDto(IconStyle, ProgressAccent, NotificationsEnabled, LaunchAtLogin, FirstRunHintShown);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(dto, JsonOptions));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"settings save failed: {ex.Message}");
        }
    }

    private void Apply(SettingsDto dto)
    {
        IconStyle = dto.IconStyle ?? IconStyle;
        ProgressAccent = dto.ProgressAccent ?? ProgressAccent;
        NotificationsEnabled = dto.NotificationsEnabled ?? NotificationsEnabled;
        LaunchAtLogin = dto.LaunchAtLogin ?? LaunchAtLogin;
        FirstRunHintShown = dto.FirstRunHintShown ?? FirstRunHintShown;
    }

    private sealed record SettingsDto(
        TrayIconStyle? IconStyle,
        bool? ProgressAccent,
        bool? NotificationsEnabled,
        bool? LaunchAtLogin,
        bool? FirstRunHintShown);
}
