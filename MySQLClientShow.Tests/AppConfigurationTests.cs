using MySQLClientShow.App.Configuration;

namespace MySQLClientShow.Tests;

public class AppConfigurationTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new AppConfiguration();

        Assert.Equal(string.Empty, config.ConnectionString);
        Assert.Equal(string.Empty, config.ClientFilter);
        Assert.Equal(string.Empty, config.QuerySearchFilter);
        Assert.Equal(1000, config.PollingIntervalMs);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var config = new AppConfiguration
        {
            ConnectionString = "Server=localhost;Port=3306;User ID=root;",
            ClientFilter = "admin@localhost",
            QuerySearchFilter = "SELECT",
            PollingIntervalMs = 500
        };

        Assert.Equal("Server=localhost;Port=3306;User ID=root;", config.ConnectionString);
        Assert.Equal("admin@localhost", config.ClientFilter);
        Assert.Equal("SELECT", config.QuerySearchFilter);
        Assert.Equal(500, config.PollingIntervalMs);
    }
}
