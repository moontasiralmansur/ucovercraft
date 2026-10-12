using System.IO;
using Microsoft.Win32;
using UCoverCraft.App.Settings;
using UCoverCraft.App.Themes;
using UCoverCraft.Core.Mvvm;

namespace UCoverCraft.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly Func<string?> _folderPicker;
    private ThemeMode _themeMode;
    private string? _defaultExportDirectory;

    public SettingsViewModel(SettingsService settings)
        : this(settings, null)
    {
    }

    internal SettingsViewModel(SettingsService settings, Func<string?>? folderPicker)
    {
        _settings = settings;
        _folderPicker = folderPicker ?? BrowseForFolder;
        _themeMode = settings.Settings.Theme;
        _defaultExportDirectory = settings.Settings.DefaultExportDirectory;

        SetThemeCommand = new RelayCommand<string>(SetTheme);
        BrowseExportDirectoryCommand = new RelayCommand(BrowseExportDirectory);
        ClearExportDirectoryCommand = new RelayCommand(ClearExportDirectory, CanClearExportDirectory);
    }

    public ThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            if (!SetProperty(ref _themeMode, value))
            {
                return;
            }

            _settings.Settings.Theme = value;
            _settings.Save();
            ThemeManager.Apply(value);
        }
    }

    public string? DefaultExportDirectory
    {
        get => _defaultExportDirectory;
        set
        {
            var directory = string.IsNullOrWhiteSpace(value) ? null : value;
            if (!SetProperty(ref _defaultExportDirectory, directory))
            {
                return;
            }

            _settings.Settings.DefaultExportDirectory = directory;
            _settings.Save();
            OnPropertyChanged(nameof(IsDefaultExportDirectoryEmpty));
            ClearExportDirectoryCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsDefaultExportDirectoryEmpty => string.IsNullOrWhiteSpace(_defaultExportDirectory);

    public RelayCommand<string> SetThemeCommand { get; }

    public RelayCommand BrowseExportDirectoryCommand { get; }

    public RelayCommand ClearExportDirectoryCommand { get; }

    private string? BrowseForFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choose default export folder" };

        if (!string.IsNullOrWhiteSpace(DefaultExportDirectory) && Directory.Exists(DefaultExportDirectory))
        {
            dialog.InitialDirectory = DefaultExportDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private void SetTheme(string? mode)
    {
        if (Enum.TryParse<ThemeMode>(mode, out var parsed))
        {
            ThemeMode = parsed;
        }
    }

    private void BrowseExportDirectory()
    {
        var folder = _folderPicker();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            DefaultExportDirectory = folder;
        }
    }

    private void ClearExportDirectory() => DefaultExportDirectory = null;

    private bool CanClearExportDirectory() => !string.IsNullOrWhiteSpace(_defaultExportDirectory);
}
