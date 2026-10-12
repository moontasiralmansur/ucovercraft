using UCoverCraft.App.Settings;
using UCoverCraft.App.ViewModels;

namespace UCoverCraft.Tests.ViewModels;

public sealed class ExportDirectoryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "UCoverCraftTests",
        Guid.NewGuid().ToString("N"));

    private readonly string _settingsPath;

    public ExportDirectoryTests()
    {
        _settingsPath = Path.Combine(_root, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void ResolveExportDirectory_ReturnsTheConfiguredDirectory_WhenItExists()
    {
        var directory = Path.Combine(_root, "exports");
        Directory.CreateDirectory(directory);

        Assert.Equal(directory, MainViewModel.ResolveExportDirectory(directory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveExportDirectory_ReturnsNull_WhenNothingUsefulIsConfigured(string? configured)
    {
        Assert.Null(MainViewModel.ResolveExportDirectory(configured));
    }

    [Fact]
    public void ResolveExportDirectory_ReturnsNull_WhenTheDirectoryIsMissing_AndNeverCreatesIt()
    {
        var missing = Path.Combine(_root, "missing");

        Assert.Null(MainViewModel.ResolveExportDirectory(missing));
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void MainViewModel_UsesThePersistedDirectory_WhenItExists()
    {
        var directory = Path.Combine(_root, "exports");
        Directory.CreateDirectory(directory);
        var viewModel = new MainViewModel(new SettingsService(_settingsPath));

        viewModel.Settings.DefaultExportDirectory = directory;

        Assert.Equal(directory, viewModel.ResolveExportDirectory());
        Assert.Equal(directory, new SettingsService(_settingsPath).Settings.DefaultExportDirectory);
    }

    [Fact]
    public void MainViewModel_FallsBackToNull_WhenTheConfiguredDirectoryDisappears()
    {
        var directory = Path.Combine(_root, "exports");
        Directory.CreateDirectory(directory);
        var viewModel = new MainViewModel(new SettingsService(_settingsPath));
        viewModel.Settings.DefaultExportDirectory = directory;

        Directory.Delete(directory);

        Assert.Null(viewModel.ResolveExportDirectory());
        Assert.False(Directory.Exists(directory));
    }
}
