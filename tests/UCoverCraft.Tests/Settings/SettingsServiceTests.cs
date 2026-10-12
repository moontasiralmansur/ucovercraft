using UCoverCraft.App.Settings;
using UCoverCraft.App.Themes;

namespace UCoverCraft.Tests.Settings;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "UCoverCraftTests",
        Guid.NewGuid().ToString("N"));

    private readonly string _filePath;

    public SettingsServiceTests()
    {
        _filePath = Path.Combine(_root, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenTheFileDoesNotExist()
    {
        var service = new SettingsService(_filePath);

        Assert.Equal(ThemeMode.FollowDevice, service.Settings.Theme);
        Assert.Null(service.Settings.DefaultExportDirectory);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsThemeAndExportDirectory()
    {
        var service = new SettingsService(_filePath);
        service.Settings.Theme = ThemeMode.Dark;
        service.Settings.DefaultExportDirectory = @"C:\Exports";
        service.Save();

        var reloaded = new SettingsService(_filePath);

        Assert.Equal(ThemeMode.Dark, reloaded.Settings.Theme);
        Assert.Equal(@"C:\Exports", reloaded.Settings.DefaultExportDirectory);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenTheFileIsCorrupt()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_filePath, "{ this is not json");

        var service = new SettingsService(_filePath);

        Assert.Equal(ThemeMode.FollowDevice, service.Settings.Theme);
        Assert.Null(service.Settings.DefaultExportDirectory);
    }

    [Fact]
    public void Save_PersistsAClearedExportDirectory()
    {
        var service = new SettingsService(_filePath);
        service.Settings.DefaultExportDirectory = @"C:\Exports";
        service.Save();

        service.Settings.DefaultExportDirectory = null;
        service.Save();

        var reloaded = new SettingsService(_filePath);

        Assert.Null(reloaded.Settings.DefaultExportDirectory);
    }

    [Fact]
    public void Save_WritesTheThemeAsAReadableName()
    {
        var service = new SettingsService(_filePath);
        service.Settings.Theme = ThemeMode.FollowDevice;
        service.Save();

        Assert.Contains("\"FollowDevice\"", File.ReadAllText(_filePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Save_DoesNotThrow_WhenTheSettingsFileCannotBeWritten()
    {
        Directory.CreateDirectory(_root);
        var blocked = Path.Combine(_root, "blocked");
        File.WriteAllText(blocked, "file in the way");
        var service = new SettingsService(Path.Combine(blocked, "settings.json"));

        service.Save();

        Assert.False(File.Exists(Path.Combine(blocked, "settings.json")));
    }

    [Fact]
    public void DefaultFilePath_IsStoredInThePerUserLocalApplicationDataFolder()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(
            Path.Combine(localApplicationData, "UCoverCraft", "settings.json"),
            SettingsService.DefaultFilePath);
    }
}
