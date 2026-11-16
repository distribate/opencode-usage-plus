using System.Text;
using Microsoft.Data.Sqlite;
using OpenCodeUsagePlus.Core.Models;

namespace OpenCodeUsagePlus.Core.Storage;

/// <summary>
/// Finds the OpenCode sign-in token without asking the user for anything.
/// <para>
/// OpenCode roots its data directory at <c>$XDG_DATA_HOME</c> or <c>&lt;home&gt;/.local/share</c>
/// on every platform, Windows included. Channel builds use <c>opencode-&lt;channel&gt;.db</c>, so the
/// database is discovered by globbing.
/// </para>
/// The database is opened read-only and is never modified.
/// </summary>
public sealed class OpenCodeCredentialStore
{
    private const string SelectActive =
        "select value from credential where integration_id = 'opencode' " +
        "order by coalesce(active, 0) desc, time_created desc, id desc limit 1";

    private const string SelectAny =
        "select value from credential where integration_id = 'opencode' limit 1";

    private readonly string? _databasePathOverride;
    private readonly string? _dataDirectoryOverride;

    public OpenCodeCredentialStore(string? databasePath = null, string? dataDirectory = null)
    {
        _databasePathOverride = string.IsNullOrWhiteSpace(databasePath) ? null : databasePath;
        _dataDirectoryOverride = string.IsNullOrWhiteSpace(dataDirectory) ? null : dataDirectory;
    }

    public string DataDirectory => _dataDirectoryOverride ?? ResolveDataDirectory();

    public static string ResolveHomeDirectory()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            return profile;
        }

        var home = Environment.GetEnvironmentVariable("HOME")
                   ?? Environment.GetEnvironmentVariable("USERPROFILE");
        return string.IsNullOrWhiteSpace(home) ? Environment.CurrentDirectory : home;
    }

    public static string ResolveDataDirectory()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var root = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(ResolveHomeDirectory(), ".local", "share");
        return Path.Combine(root, "opencode");
    }

    public IReadOnlyList<string> CandidateDatabasePaths()
    {
        if (_databasePathOverride is not null)
        {
            return new[] { _databasePathOverride };
        }

        var overridePath = Environment.GetEnvironmentVariable("OPENCODE_DB");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (string.Equals(overridePath, ":memory:", StringComparison.Ordinal))
            {
                return Array.Empty<string>();
            }

            return new[]
            {
                Path.IsPathRooted(overridePath)
                    ? overridePath
                    : Path.Combine(DataDirectory, overridePath),
            };
        }

        var directory = DataDirectory;
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.EnumerateFiles(directory, "opencode*.db", SearchOption.TopDirectoryOnly)
                .OrderByDescending(SafeLastWrite)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"cannot enumerate OpenCode data directory: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>Synchronous and IO-bound; call from a background thread.</summary>
    public AuthCredential? TryRead()
    {
        foreach (var path in CandidateDatabasePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var credential = ReadFromDatabase(path);
                if (credential is not null)
                {
                    return credential;
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"credential read failed ({Path.GetFileName(path)}): {ex.Message}");
            }
        }

        return ReadLegacyAuthJson();
    }

    private static AuthCredential? ReadFromDatabase(string path)
    {
        try
        {
            return Query(path, readOnly: true);
        }
        catch (SqliteException ex)
        {
            // WAL databases can refuse a read-only open while OpenCode holds them; read a snapshot.
            AppLog.Info($"read-only open failed ({ex.SqliteErrorCode}); retrying on a snapshot");
            return QuerySnapshot(path);
        }
    }

    private static AuthCredential? QuerySnapshot(string path)
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode-usage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var copy = Path.Combine(directory, "opencode.db");
            File.Copy(path, copy, overwrite: true);
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var sidecar = path + suffix;
                if (File.Exists(sidecar))
                {
                    File.Copy(sidecar, copy + suffix, overwrite: true);
                }
            }

            return Query(copy, readOnly: false);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Best effort.
            }
        }
    }

    private static AuthCredential? Query(string path, bool readOnly)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 3,
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA busy_timeout = 3000;";
            pragma.ExecuteNonQuery();
        }

        var raw = ExecuteScalar(connection, SelectActive) ?? ExecuteScalar(connection, SelectAny);
        return CredentialJson.TryParse(raw, path);
    }

    private static string? ExecuteScalar(SqliteConnection connection, string sql)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            var result = command.ExecuteScalar();
            return result switch
            {
                string text when !string.IsNullOrWhiteSpace(text) => text,
                byte[] bytes when bytes.Length > 0 => Encoding.UTF8.GetString(bytes),
                _ => null,
            };
        }
        catch (SqliteException ex)
        {
            AppLog.Info($"credential query skipped: {ex.Message}");
            return null;
        }
    }

    private AuthCredential? ReadLegacyAuthJson()
    {
        try
        {
            var path = Path.Combine(DataDirectory, "auth.json");
            if (!File.Exists(path))
            {
                return null;
            }

            return CredentialJson.TryParse(File.ReadAllText(path), path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"legacy auth.json read failed: {ex.Message}");
            return null;
        }
    }

    private static DateTime SafeLastWrite(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}
