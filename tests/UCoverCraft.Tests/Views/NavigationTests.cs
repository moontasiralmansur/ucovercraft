using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using UCoverCraft.App;
using UCoverCraft.App.Settings;
using UCoverCraft.App.Themes;
using UCoverCraft.App.ViewModels;
using ThemeMode = UCoverCraft.App.Themes.ThemeMode;

namespace UCoverCraft.Tests.Views;

[Collection("Ui")]
public sealed class NavigationTests
{
[Fact]
    public void Navigation_SwitchesBetweenTheWorkspaceAndTheSettingsPage()
    {
        TestUI.RunOnStaThread(() =>
            {
                TestUI.EnsureApplication();
                var window = new MainWindow();

                try
                {
                    window.Show();
                    window.UpdateLayout();

                    var viewModel = (MainViewModel)window.DataContext;

                    Assert.True(viewModel.IsWorkspaceVisible);
                    Assert.False(viewModel.IsSettingsVisible);
                    Assert.Equal(Visibility.Visible, WorkspaceRoot(window).Visibility);
                    Assert.Equal(Visibility.Visible, FormScrollViewer(window).Visibility);
                    Assert.Equal(Visibility.Collapsed, SettingsRoot(window).Visibility);

                viewModel.ShowSettingsCommand.Execute(null);
                window.UpdateLayout();
                WaitForOpacity(SettingsRoot(window), 1.0);
                WaitForVisibility(WorkspaceRoot(window), Visibility.Collapsed);
                WaitForVisibility(FormScrollViewer(window), Visibility.Collapsed);
                WaitForVisibility(SettingsRoot(window), Visibility.Visible);

                Assert.False(viewModel.IsWorkspaceVisible);
                Assert.True(viewModel.IsSettingsVisible);
                Assert.Equal(Visibility.Collapsed, WorkspaceRoot(window).Visibility);
                Assert.Equal(Visibility.Collapsed, FormScrollViewer(window).Visibility);
                Assert.Equal(Visibility.Visible, SettingsRoot(window).Visibility);
                Assert.Equal(1.0, SettingsRoot(window).Opacity, 1);
                Assert.True(SettingsRoot(window).IsHitTestVisible);
                Assert.False(FormScrollViewer(window).IsHitTestVisible);
                Assert.False(WorkspaceRoot(window).IsHitTestVisible);

                var settingsTexts = TestUI.Descendants<TextBlock>(SettingsRoot(window))
                    .Select(block => block.Text)
                    .ToList();
                Assert.Contains("Settings", settingsTexts);
                Assert.Contains("Appearance", settingsTexts);
                Assert.Contains("Default export directory", settingsTexts);
                Assert.Contains("No default folder selected", settingsTexts);

                viewModel.ShowWorkspaceCommand.Execute(null);
                window.UpdateLayout();
                WaitForOpacity(SettingsRoot(window), 0.0);
                WaitForVisibility(SettingsRoot(window), Visibility.Collapsed);
                WaitForVisibility(WorkspaceRoot(window), Visibility.Visible);
                WaitForVisibility(FormScrollViewer(window), Visibility.Visible);

                Assert.True(viewModel.IsWorkspaceVisible);
                Assert.False(viewModel.IsSettingsVisible);
                Assert.Equal(Visibility.Visible, WorkspaceRoot(window).Visibility);
                Assert.Equal(Visibility.Visible, FormScrollViewer(window).Visibility);
                Assert.Equal(Visibility.Collapsed, SettingsRoot(window).Visibility);
                Assert.Equal(0.0, SettingsRoot(window).Opacity, 1);
                Assert.False(SettingsRoot(window).IsHitTestVisible);
                Assert.True(FormScrollViewer(window).IsHitTestVisible);
                Assert.True(WorkspaceRoot(window).IsHitTestVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void WaitForOpacity(UIElement element, double targetOpacity, int timeoutMs = 5000)
    {
        var start = Environment.TickCount;
        var initialOpacity = element.Opacity;

        // If already at target (within tolerance), return immediately
        if (Math.Abs(initialOpacity - targetOpacity) <= 0.02)
        {
            return;
        }

        // Wait for animation to start (opacity begins changing from initial value)
        var animationStartTimeout = 1000;
        var animationStartTime = Environment.TickCount;
        while (Math.Abs(element.Opacity - initialOpacity) <= 0.02)
        {
            if (Environment.TickCount - animationStartTime > animationStartTimeout)
            {
                break; // Animation didn't start, but continue waiting for target
            }
            Thread.Sleep(10);
            element.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        }

        // Wait for animation to reach target
        while (Math.Abs(element.Opacity - targetOpacity) > 0.02)
        {
            if (Environment.TickCount - start > timeoutMs)
            {
                break;
            }
            Thread.Sleep(10);
            element.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        }
    }

    private static void WaitForDispatcherIdle(Dispatcher dispatcher)
    {
        dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void WaitForVisibility(UIElement element, Visibility targetVisibility, int timeoutMs = 2000)
    {
        var start = Environment.TickCount;
        while (element.Visibility != targetVisibility)
        {
            if (Environment.TickCount - start > timeoutMs)
            {
                break;
            }
            Thread.Sleep(10);
            element.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        }
    }

    [Fact]
    public void SettingsPage_ExposesThemeChoicesAndExportDirectoryControls()
    {
        TestUI.RunOnStaThread(() =>
        {
            TestUI.EnsureApplication();
            var originalMode = ThemeManager.Mode;
            var settingsRoot = Path.Combine(
                Path.GetTempPath(),
                "UCoverCraftTests",
                Guid.NewGuid().ToString("N"));
            var window = new MainWindow
            {
                DataContext = new MainViewModel(new SettingsService(Path.Combine(settingsRoot, "settings.json"))),
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var viewModel = (MainViewModel)window.DataContext;
                viewModel.ShowSettingsCommand.Execute(null);
                window.UpdateLayout();
                WaitForDispatcherIdle(window.Dispatcher);
                WaitForOpacity(SettingsRoot(window), 1.0);

                var settings = SettingsRoot(window);
                Assert.Equal(1.0, settings.Opacity, 1);
                Assert.True(settings.IsHitTestVisible);
                var themeButtons = TestUI.Descendants<Button>(settings)
                    .Where(button => button.Command == viewModel.Settings.SetThemeCommand)
                    .ToList();

                Assert.Equal(
                    new[] { "Light", "Dark", "Follow device" },
                    themeButtons.Select(button => button.Content as string));

                var accent = ((SolidColorBrush)Application.Current!.Resources["AccentBrush"]!).Color;
                Assert.Equal(accent, ((SolidColorBrush)themeButtons[2].Background!).Color);
                Assert.NotEqual(accent, ((SolidColorBrush)themeButtons[0].Background!).Color);

                viewModel.Settings.SetThemeCommand.Execute("Dark");

                Assert.Equal(ThemeMode.Dark, viewModel.Settings.ThemeMode);
                Assert.Equal(accent, ((SolidColorBrush)themeButtons[1].Background!).Color);
                Assert.NotEqual(accent, ((SolidColorBrush)themeButtons[2].Background!).Color);

                var browse = TestUI.Descendants<Button>(settings)
                    .Single(button => button.Content as string == "Browse...");
                var clear = TestUI.Descendants<Button>(settings)
                    .Single(button => button.Content as string == "Clear");

                Assert.Same(viewModel.Settings.BrowseExportDirectoryCommand, browse.Command);
                Assert.Same(viewModel.Settings.ClearExportDirectoryCommand, clear.Command);
                Assert.False(clear.IsEnabled);

                viewModel.Settings.DefaultExportDirectory = @"C:\Exports";

                Assert.True(clear.IsEnabled);
            }
            finally
            {
                ThemeManager.Apply(originalMode);
                window.Close();

                if (Directory.Exists(settingsRoot))
                {
                    Directory.Delete(settingsRoot, recursive: true);
                }
            }
        });
    }

    private static Grid MainLayout(MainWindow window) =>
        ((DockPanel)window.Content).Children.OfType<Grid>().Single();

    private static Grid WorkspaceRoot(MainWindow window) =>
        MainLayout(window).Children.OfType<Grid>().Single(child => Grid.GetColumn(child) == 2 && child.Style == (Style)window.FindResource("WorkspaceContentStyle"));

    private static Grid SettingsRoot(MainWindow window) =>
        MainLayout(window).Children.OfType<Grid>().Single(child => Grid.GetColumn(child) == 0 && Grid.GetColumnSpan(child) == 3 && child.Style == (Style)window.FindResource("SettingsContentStyle"));

    private static ScrollViewer FormScrollViewer(MainWindow window) =>
        TestUI.Descendants<ScrollViewer>(window).Single(viewer => viewer.Content is StackPanel);
}
