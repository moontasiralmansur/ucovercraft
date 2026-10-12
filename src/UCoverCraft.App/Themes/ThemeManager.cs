using System.IO;
using System.Windows;

namespace UCoverCraft.App.Themes;

public static class ThemeManager
{
    private const string ThemeDictionaryToken = "Themes/";
    private const string LightDictionaryFileName = "LightTheme.xaml";
    private const string DarkDictionaryFileName = "DarkTheme.xaml";

    private static readonly Uri LightThemeUri = new("pack://application:,,,/UCoverCraft;component/Themes/LightTheme.xaml");
    private static readonly Uri DarkThemeUri = new("pack://application:,,,/UCoverCraft;component/Themes/DarkTheme.xaml");

    public static ThemeMode Mode { get; private set; } = ThemeMode.FollowDevice;

    public static AppTheme ActiveTheme { get; private set; } = AppTheme.Light;

    public static void Apply(ThemeMode mode)
    {
        Mode = mode;
        SetTheme(ResolveTheme(mode));
    }

    public static void RefreshIfFollowDevice()
    {
        if (Mode == ThemeMode.FollowDevice)
        {
            SetTheme(DeviceTheme.Current);
        }
    }

    private static AppTheme ResolveTheme(ThemeMode mode) =>
        mode switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            _ => DeviceTheme.Current,
        };

    private static void SetTheme(AppTheme theme)
    {
        ActiveTheme = theme;
        var fileName = theme == AppTheme.Dark ? DarkDictionaryFileName : LightDictionaryFileName;

        if (Application.Current is null)
        {
            return;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source;
            if (source is null || !source.OriginalString.Contains(ThemeDictionaryToken, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(Path.GetFileName(source.OriginalString), fileName, StringComparison.OrdinalIgnoreCase))
            {
                dictionaries[i] = new ResourceDictionary { Source = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri };
            }

            return;
        }

        dictionaries.Insert(0, new ResourceDictionary { Source = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri });
    }
}
