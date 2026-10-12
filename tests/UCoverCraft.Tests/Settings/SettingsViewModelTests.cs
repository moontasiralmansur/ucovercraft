using UCoverCraft.App.Settings;
using UCoverCraft.App.Themes;
using UCoverCraft.App.ViewModels;

namespace UCoverCraft.Tests.Settings;

[Collection("Ui")]
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "UCoverCraftTests",
        Guid.NewGuid().ToString("N"));

    private readonly string _filePath;

    public SettingsViewModelTests()
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
    public void SetThemeCommand_AppliesAndPersistsDarkMode()
    {
        TestUi.RunOnStaThread(() =>
        {
            TestUi.EnsureApplication();
            var originalMode = ThemeManager.Mode;

            try
            {
                var viewModel = new SettingsViewModel(new SettingsService(_filePath));

                viewModel.SetThemeCommand.Execute("Dark");

                Assert.Equal(ThemeMode.Dark, viewModel.ThemeMode);
                Assert.Equal(ThemeMode.Dark, ThemeManager.Mode);
                Assert.Equal(AppTheme.Dark, ThemeManager.ActiveTheme);
                Assert.Equal(ThemeMode.Dark, new SettingsService(_filePath).Settings.Theme);
            }
            finally
            {
                ThemeManager.Apply(originalMode);
            }
        });
    }

    [Fact]
    public void SetThemeCommand_FollowDevice_ResolvesTheDeviceTheme()
    {
        TestUi.RunOnStaThread(() =>
        {
            TestUi.EnsureApplication();
            var originalMode = ThemeManager.Mode;

            try
            {
                var viewModel = new SettingsViewModel(new SettingsService(_filePath));

                viewModel.SetThemeCommand.Execute("FollowDevice");

                Assert.Equal(ThemeMode.FollowDevice, viewModel.ThemeMode);
                Assert.Equal(ThemeMode.FollowDevice, ThemeManager.Mode);
                Assert.Equal(DeviceTheme.Current, ThemeManager.ActiveTheme);
                Assert.Equal(ThemeMode.FollowDevice, new SettingsService(_filePath).Settings.Theme);
            }
            finally
            {
                ThemeManager.Apply(originalMode);
            }
        });
    }

    [Fact]
    public void SetThemeCommand_IgnoresUnknownValues()
    {
        TestUi.RunOnStaThread(() =>
        {
            TestUi.EnsureApplication();
            var originalMode = ThemeManager.Mode;

            try
            {
                var viewModel = new SettingsViewModel(new SettingsService(_filePath));

                viewModel.SetThemeCommand.Execute("HighContrast");

                Assert.Equal(ThemeMode.FollowDevice, viewModel.ThemeMode);
                Assert.Equal(originalMode, ThemeManager.Mode);
            }
            finally
            {
                ThemeManager.Apply(originalMode);
            }
        });
    }

    [Fact]
    public void Construction_LoadsThePersistedThemeWithoutApplyingIt()
    {
        var service = new SettingsService(_filePath);
        service.Settings.Theme = ThemeMode.Dark;
        service.Save();

        TestUi.RunOnStaThread(() =>
        {
            TestUi.EnsureApplication();
            var originalMode = ThemeManager.Mode;

            try
            {
                var viewModel = new SettingsViewModel(new SettingsService(_filePath));

                Assert.Equal(ThemeMode.Dark, viewModel.ThemeMode);
                Assert.Equal(originalMode, ThemeManager.Mode);
            }
            finally
            {
                ThemeManager.Apply(originalMode);
            }
        });
    }

    [Fact]
    public void DefaultExportDirectory_PersistsOnSetAndClearsThroughTheCommand()
    {
        var viewModel = new SettingsViewModel(new SettingsService(_filePath));

        viewModel.DefaultExportDirectory = @"C:\Exports";

        Assert.False(viewModel.IsDefaultExportDirectoryEmpty);
        Assert.True(viewModel.ClearExportDirectoryCommand.CanExecute(null));
        Assert.Equal(@"C:\Exports", new SettingsService(_filePath).Settings.DefaultExportDirectory);

        viewModel.ClearExportDirectoryCommand.Execute(null);

        Assert.Null(viewModel.DefaultExportDirectory);
        Assert.True(viewModel.IsDefaultExportDirectoryEmpty);
        Assert.False(viewModel.ClearExportDirectoryCommand.CanExecute(null));
        Assert.Null(new SettingsService(_filePath).Settings.DefaultExportDirectory);
    }

    [Fact]
    public void BrowseCommand_TakesTheDirectoryFromThePicker()
    {
        var folder = Path.Combine(_root, "picked");
        var viewModel = new SettingsViewModel(new SettingsService(_filePath), () => folder);

        viewModel.BrowseExportDirectoryCommand.Execute(null);

        Assert.Equal(folder, viewModel.DefaultExportDirectory);
        Assert.Equal(folder, new SettingsService(_filePath).Settings.DefaultExportDirectory);
    }

    [Fact]
    public void BrowseCommand_KeepsTheCurrentDirectory_WhenThePickerIsCancelled()
    {
        var viewModel = new SettingsViewModel(new SettingsService(_filePath), () => null);
        viewModel.DefaultExportDirectory = @"C:\Exports";

        viewModel.BrowseExportDirectoryCommand.Execute(null);

        Assert.Equal(@"C:\Exports", viewModel.DefaultExportDirectory);
    }
}
