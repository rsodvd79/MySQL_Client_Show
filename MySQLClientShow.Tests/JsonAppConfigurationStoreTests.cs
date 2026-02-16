using System.Text.Json;
using MySQLClientShow.App.Configuration;
using MySQLClientShow.App.Services;

namespace MySQLClientShow.Tests;

public class JsonAppConfigurationStoreTests
{
    [Fact]
    public void Load_NeverThrows()
    {
        var store = new JsonAppConfigurationStore();

        // The config file may or may not exist depending on environment.
        // Load should never throw regardless.
        var config = store.Load();

        Assert.NotNull(config);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsConfiguration()
    {
        var store = new JsonAppConfigurationStore();

        var original = new AppConfiguration
        {
            ConnectionString = "Server=test;",
            ClientFilter = "testuser@host",
            QuerySearchFilter = "SELECT|INSERT",
            PollingIntervalMs = 750
        };

        store.Save(original);

        var loaded = store.Load();

        Assert.Equal(original.ConnectionString, loaded.ConnectionString);
        Assert.Equal(original.ClientFilter, loaded.ClientFilter);
        Assert.Equal(original.QuerySearchFilter, loaded.QuerySearchFilter);
        Assert.Equal(original.PollingIntervalMs, loaded.PollingIntervalMs);
    }

    [Fact]
    public void AppConfiguration_SerializesToJson()
    {
        var config = new AppConfiguration
        {
            ConnectionString = "Server=127.0.0.1;",
            ClientFilter = "root@localhost",
            QuerySearchFilter = "orders",
            PollingIntervalMs = 2000
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

        Assert.Contains("\"ConnectionString\"", json);
        Assert.Contains("\"ClientFilter\"", json);
        Assert.Contains("\"QuerySearchFilter\"", json);
        Assert.Contains("\"PollingIntervalMs\"", json);
        Assert.Contains("Server=127.0.0.1;", json);
    }

    [Fact]
    public void AppConfiguration_DeserializesFromJson()
    {
        var json = """
        {
            "ConnectionString": "Server=test;",
            "ClientFilter": "user@host",
            "QuerySearchFilter": "select",
            "PollingIntervalMs": 500
        }
        """;

        var config = JsonSerializer.Deserialize<AppConfiguration>(json);

        Assert.NotNull(config);
        Assert.Equal("Server=test;", config.ConnectionString);
        Assert.Equal("user@host", config.ClientFilter);
        Assert.Equal("select", config.QuerySearchFilter);
        Assert.Equal(500, config.PollingIntervalMs);
    }

    [Fact]
    public void AppConfiguration_DeserializesFromEmptyJson_UsesDefaults()
    {
        var json = "{}";

        var config = JsonSerializer.Deserialize<AppConfiguration>(json);

        Assert.NotNull(config);
        Assert.Equal(string.Empty, config.ConnectionString);
        Assert.Equal(string.Empty, config.ClientFilter);
        Assert.Equal(string.Empty, config.QuerySearchFilter);
        Assert.Equal(1000, config.PollingIntervalMs);
    }
}
