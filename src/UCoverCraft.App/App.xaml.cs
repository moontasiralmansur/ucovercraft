using System.Windows;
using System.Windows.Threading;

namespace UCoverCraft.App;

public partial class App : Application
{
    private const string ErrorDialogTitle = "UCoverCraft";

    internal static Action<string> ShowUserMessage { get; set; } = ShowErrorDialog;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    internal static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e) =>
        e.Handled = TryHandleUnhandledException(e.Exception);

    internal static bool TryHandleUnhandledException(Exception exception)
    {
        if (IsFatal(exception))
        {
            return false;
        }

        ShowUserMessage(BuildUnhandledErrorMessage(exception));
        return true;
    }

    internal static string BuildUnhandledErrorMessage(Exception exception)
    {
        var details = string.IsNullOrWhiteSpace(exception.Message)
            ? string.Empty
            : $"Details: {exception.Message}\n\n";

        return
            "UCoverCraft hit an unexpected problem and couldn't finish what it was doing.\n\n" +
            details +
            "The application is still running. Save your work and try again, " +
            "or restart UCoverCraft if the problem keeps happening.";
    }

    private static bool IsFatal(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or AppDomainUnloadedException)
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }

    private static void ShowErrorDialog(string message) =>
        MessageBox.Show(message, ErrorDialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
}
