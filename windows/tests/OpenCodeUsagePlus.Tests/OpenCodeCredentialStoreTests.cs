using System;
using System.IO;
using Microsoft.Data.Sqlite;
using OpenCodeUsagePlus.Core.Models;
using OpenCodeUsagePlus.Core.Storage;
using Xunit;

namespace OpenCodeUsagePlus.Tests;

public sealed class OpenCodeCredentialStoreTests : IDisposable
{
    private readonly string _directory;

    public OpenCodeCredentialStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ocu-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best effort.
        }
    }

    [Fact]
    public void Reads_the_active_oauth_credential()
    {
        var path = CreateDatabase("opencode.db");
        Insert(path, "cred_1", """{"type":"oauth","access":"at-1","metadata":{"orgID":"wrk_1"}}""", active: 1, timeCreated: 100);
        Insert(path, "cred_2", """{"type":"oauth","access":"at-2"}""", active: 0, timeCreated: 200);

        var credential = new OpenCodeCredentialStore(databasePath: path, dataDirectory: _directory).TryRead();

        Assert.NotNull(credential);
        Assert.Equal("at-1", credential!.Token);
        Assert.Equal("wrk_1", credential.OrganizationId);
        Assert.Equal(CredentialKind.OAuth, credential.Kind);
    }

    [Fact]
    public void Prefers_the_newest_when_no_row_is_active()
    {
        var path = CreateDatabase("opencode.db");
        Insert(path, "cred_old", """{"type":"oauth","access":"older"}""", active: null, timeCreated: 100);
        Insert(path, "cred_new", """{"type":"oauth","access":"newer"}""", active: null, timeCreated: 500);

        var credential = new OpenCodeCredentialStore(databasePath: path, dataDirectory: _directory).TryRead();

        Assert.Equal("newer", credential!.Token);
    }

    [Fact]
    public void Finds_channel_databases_by_glob()
    {
        var stable = CreateDatabase("opencode.db");
        Insert(stable, "cred_stable", """{"type":"oauth","access":"stable"}""", active: 1, timeCreated: 100);

        var dev = CreateDatabase("opencode-dev.db");
        Insert(dev, "cred_dev", """{"type":"oauth","access":"dev"}""", active: 1, timeCreated: 100);

        File.SetLastWriteTimeUtc(stable, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(dev, DateTime.UtcNow);

        var store = new OpenCodeCredentialStore(dataDirectory: _directory);
        var credential = store.TryRead();

        Assert.Equal("dev", credential!.Token);
        Assert.Equal(2, store.CandidateDatabasePaths().Count);
    }

    [Fact]
    public void Skips_a_database_without_an_opencode_row()
    {
        var other = CreateDatabase("opencode.db");
        using (var connection = new SqliteConnection($"Data Source={other}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "insert into credential (id, integration_id, label, value, active, time_created) " +
                "values ('c1', 'anthropic', 'default', '{\"type\":\"key\",\"key\":\"sk-ant\"}', 1, 100);";
            command.ExecuteNonQuery();
        }

        var legacy = Path.Combine(_directory, "auth.json");
        File.WriteAllText(legacy, """{"opencode":{"type":"oauth","access":"legacy-access"}}""");

        var credential = new OpenCodeCredentialStore(dataDirectory: _directory).TryRead();

        Assert.NotNull(credential);
        Assert.Equal("legacy-access", credential!.Token);
    }

    [Fact]
    public void Returns_null_without_any_source()
    {
        var credential = new OpenCodeCredentialStore(dataDirectory: _directory).TryRead();
        Assert.Null(credential);
    }

    [Fact]
    public void Honours_an_explicit_open_code_db_override()
    {
        var path = CreateDatabase("custom.db");
        Insert(path, "cred_custom", """{"type":"key","key":"sk-custom"}""", active: 1, timeCreated: 1);

        var previous = Environment.GetEnvironmentVariable("OPENCODE_DB");
        try
        {
            Environment.SetEnvironmentVariable("OPENCODE_DB", path);
            var credential = new OpenCodeCredentialStore(dataDirectory: _directory).TryRead();

            Assert.Equal("sk-custom", credential!.Token);
            Assert.Equal(CredentialKind.ApiKey, credential.Kind);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENCODE_DB", previous);
        }
    }

    private string CreateDatabase(string fileName)
    {
        var path = Path.Combine(_directory, fileName);
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
        create table credential (
            id text primary key,
            integration_id text,
            label text not null,
            value text not null,
            active integer,
            time_created integer,
            time_updated integer
        );
        """;
        command.ExecuteNonQuery();
        return path;
    }

    private static void Insert(string path, string id, string value, int? active, long timeCreated)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "insert into credential (id, integration_id, label, value, active, time_created) " +
            "values ($id, 'opencode', 'default', $value, $active, $time)";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$active", (object?)active ?? DBNull.Value);
        command.Parameters.AddWithValue("$time", timeCreated);
        command.ExecuteNonQuery();
    }
}
