using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UCoverCraft.App;
using UCoverCraft.App.Themes;
using ThemeMode = UCoverCraft.App.Themes.ThemeMode;

namespace UCoverCraft.Tests.Themes;

[Collection("Ui")]
public sealed class ThemeManagerTests
{
    private static readonly Color LightWindowBackground = Color.FromRgb(0xF2, 0xF2, 0xF2);
    private static readonly Color DarkWindowBackground = Color.FromRgb(0x00, 0x00, 0x00);
    private static readonly Color LightSurface = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color DarkSurface = Color.FromRgb(0x1D, 0x1D, 0x1D);

    [Fact]
    public void Apply_Light_SwitchesTheApplicationResourcesToTheLightPalette()
    {
        TestUI.RunOnStaThread(() =>
        {
            TestUI.EnsureApplication();
            var originalMode = ThemeManager.Mode;

            try
            {
                ThemeManager.Apply(ThemeMode.Light);

                Assert.Equal(ThemeMode.Light, ThemeManager.Mode);
                Assert.Equal(AppTheme.Light, ThemeManager.ActiveTheme);
                Assert.Equal(LightWindowBackground, ResourceColor("WindowBackgroundBrush"));
                Assert.Equal(LightSurface, ResourceColor("SurfaceBrush"));
            }
            finally
            {
                ThemeManager.Apply(originalMode);
            }
        });
    }

    [Fact]
    public void Apply_Dark_SwitchesTheApplicationResourcesToTheDarkPalette()
    {
        TestUI.RunOnStaThread(() =>
        {
            TestUI.EnsureApplication();
            var originalMode = ThemeManager.Mode;

            try
            {
                ThemeManager.Apply(ThemeMode.Dark);

                Assert.Equal(ThemeMode.Dark, ThemeManager.Mode);
                Assert.Equal(AppTheme.Dark, ThemeManager.ActiveTheme);
                Assert.Equal(DarkWindowBackground, ResourceColor("WindowBackgroundBrush"));
                Assert.Equal(DarkSurface, ResourceColor("SurfaceBrush"));
            }
            finally
            {
                ThemeManager.Apply(originalMode);
            }
        });
    }

    [Fact]
    public void Apply_FollowDevice_ResolvesTheCurrentDeviceTheme()
    {
        TestUI.RunOnStaThread(() =>
        {
            TestUI.EnsureApplication();
            var originalMode = ThemeManager.Mode;

            try
            {
                ThemeManager.Apply(ThemeMode.FollowDevice);

                Assert.Equal(ThemeMode.FollowDevice, ThemeManager.Mode);
                Assert.Equal(DeviceTheme.Current, ThemeManager.ActiveTheme);

                var expected = ThemeManager.ActiveTheme == AppTheme.Dark
                    ? DarkWindowBackground
                    : LightWindowBackground;
                Assert.Equal(expected, ResourceColor("WindowBackgroundBrush"));
            }
            finally
            {
                ThemeManager.Apply(originalMode);
            }
        });
    }

    [Fact]
    public void RefreshIfFollowDevice_DoesNotOverrideAnExplicitMode()
    {
        TestUI.RunOnStaThread(() =>
        {
            TestUI.EnsureApplication();
            var originalMode = ThemeManager.Mode;

            try
            {
                ThemeManager.Apply(ThemeMode.Dark);

                ThemeManager.RefreshIfFollowDevice();

                Assert.Equal(ThemeMode.Dark, ThemeManager.Mode);
                Assert.Equal(AppTheme.Dark, ThemeManager.ActiveTheme);
                Assert.Equal(DarkWindowBackground, ResourceColor("WindowBackgroundBrush"));
            }
            finally
            {
                ThemeManager.Apply(originalMode);
            }
        });
    }

    [Fact]
    public void Apply_UpdatesAnOpenWindowWithoutChangingThePreviewPage()
    {
        TestUI.RunOnStaThread(() =>
        {
            TestUI.EnsureApplication();
            var originalMode = ThemeManager.Mode;
            var window = new MainWindow();

            try
            {
                window.Show();
                window.UpdateLayout();

                ThemeManager.Apply(ThemeMode.Dark);
                window.UpdateLayout();

                Assert.Equal(DarkWindowBackground, ((SolidColorBrush)window.Background!).Color);

                var field = TestUI.Descendants<TextBox>(window)
                    .First(box => TestUI.IsEffectivelyVisible(box));
                Assert.Equal(DarkSurface, ((SolidColorBrush)field.Background!).Color);

                var pageBorder = PreviewPageBorder(window);
                Assert.Equal(Colors.White, ((SolidColorBrush)pageBorder.Background!).Color);

                ThemeManager.Apply(ThemeMode.Light);
                window.UpdateLayout();

                Assert.Equal(LightWindowBackground, ((SolidColorBrush)window.Background!).Color);
                Assert.Equal(LightSurface, ((SolidColorBrush)field.Background!).Color);
                Assert.Equal(Colors.White, ((SolidColorBrush)pageBorder.Background!).Color);
            }
            finally
            {
                ThemeManager.Apply(originalMode);
                window.Close();
            }
        });
    }

    private static Border PreviewPageBorder(MainWindow window)
    {
        var pageGrid = TestUI.Descendants<Grid>(window)
            .Single(grid => Math.Abs(grid.Width - 210d * 96 / 25.4) < 0.01);

        return (Border)VisualTreeHelper.GetParent(pageGrid);
    }

    private static Color ResourceColor(string key) =>
        ((SolidColorBrush)Application.Current!.Resources[key]!).Color;
}
