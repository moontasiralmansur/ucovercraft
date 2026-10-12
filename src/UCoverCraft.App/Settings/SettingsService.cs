using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UCoverCraft.App.Settings;

public sealed class SettingsService
{
    private static readonly Lazy<SettingsService> SharedInstance = new(() => new SettingsService(DefaultFilePath));

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsService(string filePath)
    {
        FilePath = filePath;
        Settings = Load();
    }

    public string FilePath { get; }

    public AppSettings Settings { get; private set; }

    public static SettingsService Default => SharedInstance.Value;

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UCoverCraft",
        "settings.json");

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(Settings, _serializerOptions));
        }
        catch
        {
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), _serializerOptions)
                ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
