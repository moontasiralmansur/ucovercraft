using Microsoft.Win32;

namespace UCoverCraft.App.Themes;

public static class DeviceTheme
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    public static AppTheme Current
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
                var value = key?.GetValue(AppsUseLightThemeValueName);
                return value is int useLightTheme && useLightTheme == 0 ? AppTheme.Dark : AppTheme.Light;
            }
            catch
            {
                return AppTheme.Light;
            }
        }
    }
}
