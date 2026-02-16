namespace MySQLClientShow.App.Configuration;

public sealed class AppConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;

    public string ClientFilter { get; set; } = string.Empty;

    public string QuerySearchFilter { get; set; } = string.Empty;

    public int PollingIntervalMs { get; set; } = 1000;
}
