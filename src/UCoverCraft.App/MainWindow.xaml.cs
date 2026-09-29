using System.Windows;
using UCoverCraft.App.ViewModels;

namespace UCoverCraft.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
