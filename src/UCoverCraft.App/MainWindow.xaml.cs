using System.Windows;
using UCoverCraft.App.Themes;
using UCoverCraft.App.ViewModels;

namespace UCoverCraft.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Activated += OnActivated;
    }

    private void OnActivated(object? sender, EventArgs e) => ThemeManager.RefreshIfFollowDevice();
}
