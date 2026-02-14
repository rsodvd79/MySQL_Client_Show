using System.Text.Json;
using MySQLClientShow.App.Configuration;

namespace MySQLClientShow.App.Services;

public sealed class JsonAppConfigurationStore
{
    private const string FolderName = "MySQLClientShow";
    private const string FileName = "appconfig.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configFilePath;

    public JsonAppConfigurationStore()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var configDirectory = Path.Combine(baseDirectory, FolderName);
        _configFilePath = Path.Combine(configDirectory, FileName);
    }

    public AppConfiguration Load()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                return new AppConfiguration();
            }

            var json = File.ReadAllText(_configFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppConfiguration();
            }

            var config = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions);
            return config ?? new AppConfiguration();
        }
        catch
        {
            // If config is corrupted or unreadable, fallback to defaults.
            return new AppConfiguration();
        }
    }

    public void Save(AppConfiguration configuration)
    {
        var directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(configuration, JsonOptions);
        File.WriteAllText(_configFilePath, json);
    }
}
