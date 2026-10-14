using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace UCoverCraft.Tests;

[CollectionDefinition("Ui")]
public sealed class UiTestCollection
{
}

internal static class TestUI
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

    public static void ClickElement(FrameworkElement element, Point? relativePosition = null)
    {
        if (!element.IsLoaded)
        {
            throw new InvalidOperationException("Element must be loaded before clicking.");
        }

        var position = relativePosition ?? new Point(element.ActualWidth / 2, element.ActualHeight / 2);

        var mouseDownArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            Source = element
        };
        mouseDownArgs.GetType()
            .GetField("_mouseDevice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(mouseDownArgs, Mouse.PrimaryDevice);
        mouseDownArgs.GetType()
            .GetField("_stylusDevice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(mouseDownArgs, null);
        mouseDownArgs.GetType()
            .GetField("_timestamp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(mouseDownArgs, Environment.TickCount);
        mouseDownArgs.GetType()
            .GetField("_clickCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(mouseDownArgs, 1);

        element.RaiseEvent(mouseDownArgs);

        var mouseUpArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonUpEvent,
            Source = element
        };
        mouseUpArgs.GetType()
            .GetField("_mouseDevice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(mouseUpArgs, Mouse.PrimaryDevice);
        mouseUpArgs.GetType()
            .GetField("_stylusDevice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(mouseUpArgs, null);
        mouseUpArgs.GetType()
            .GetField("_timestamp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(mouseUpArgs, Environment.TickCount);
        mouseUpArgs.GetType()
            .GetField("_clickCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(mouseUpArgs, 1);

        element.RaiseEvent(mouseUpArgs);

        var clickArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            Source = element
        };
        clickArgs.GetType()
            .GetField("_clickCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(clickArgs, 1);

        element.RaiseEvent(clickArgs);
    }

    public static void ClickAt(FrameworkElement element, Point absolutePosition)
    {
        if (!element.IsLoaded)
        {
            throw new InvalidOperationException("Element must be loaded before clicking.");
        }

        var elementTopLeft = element.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0));
        var relativePosition = new Point(absolutePosition.X - elementTopLeft.X, absolutePosition.Y - elementTopLeft.Y);
        ClickElement(element, relativePosition);
    }

    public static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000, int pollIntervalMs = 50)
    {
        var start = Environment.TickCount;
        while (!condition())
        {
            if (Environment.TickCount - start > timeoutMs)
            {
                throw new TimeoutException($"Condition not met within {timeoutMs}ms");
            }
            await Task.Delay(pollIntervalMs);
            Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
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
