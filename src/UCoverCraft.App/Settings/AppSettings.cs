using UCoverCraft.App.Themes;

namespace UCoverCraft.App.Settings;

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.FollowDevice;

    public string? DefaultExportDirectory { get; set; }
}
