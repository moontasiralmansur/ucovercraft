using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace UCoverCraft.Tests;

[CollectionDefinition("Ui")]
public sealed class UiTestCollection
{
}

internal static class TestUi
{
    private static readonly object UiSync = new();
    private static readonly Queue<Action> UiQueue = new();
    private static Thread? uiThread;

    public static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var completed = false;

        lock (UiSync)
        {
            UiQueue.Enqueue(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    lock (UiSync)
                    {
                        completed = true;
                        Monitor.PulseAll(UiSync);
                    }
                }
            });
            Monitor.Pulse(UiSync);
            EnsureUiThread();

            while (!completed)
            {
                Monitor.Wait(UiSync);
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public static UCoverCraft.App.App EnsureApplication()
    {
        var application = Application.Current as UCoverCraft.App.App;
        if (application is null)
        {
            application = new UCoverCraft.App.App();
            application.InitializeComponent();
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        // Clean up any existing windows from previous tests
        CleanupWindows();

        return application;
    }

    private static void CleanupWindows()
    {
        var application = Application.Current as UCoverCraft.App.App;
        if (application is null)
        {
            return;
        }

        var windows = application.Windows.Cast<Window>().ToList();
        foreach (var window in windows)
        {
            if (window.IsLoaded || window.IsVisible)
            {
                window.Close();
            }
        }
    }

    public static T NearestAncestor<T>(DependencyObject node)
        where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
            {
                return match;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        throw new InvalidOperationException($"No {typeof(T).Name} ancestor was found.");
    }

    public static bool IsEffectivelyVisible(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is UIElement element)
            {
                if (element.Visibility != Visibility.Visible)
                {
                    return false;
                }
                if (element.Opacity <= 0.0)
                {
                    return false;
                }
                if (!element.IsHitTestVisible)
                {
                    return false;
                }
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return true;
    }

    public static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i)))
            {
                yield return child;
            }
        }
    }

    public static Rect GetScreenBounds(FrameworkElement element)
    {
        if (!element.IsLoaded)
        {
            return Rect.Empty;
        }

        var topLeft = element.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0));
        var bottomRight = element.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(element.ActualWidth, element.ActualHeight));
        return new Rect(topLeft, bottomRight);
    }

    private static void EnsureUiThread()
    {
        if (uiThread is not null)
        {
            return;
        }

        uiThread = new Thread(() =>
        {
            while (true)
            {
                Action work;
                lock (UiSync)
                {
                    while (UiQueue.Count == 0)
                    {
                        Monitor.Wait(UiSync);
                    }

                    work = UiQueue.Dequeue();
                }

                work();
            }
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.IsBackground = true;
        uiThread.Start();
    }
}
