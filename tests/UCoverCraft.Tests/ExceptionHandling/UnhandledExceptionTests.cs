using System.Reflection;
using System.Windows.Threading;
using AppType = UCoverCraft.App.App;

namespace UCoverCraft.Tests.ExceptionHandling;

public sealed class UnhandledExceptionTests : IDisposable
{
    private readonly Action<string> _originalShowUserMessage = AppType.ShowUserMessage;

    public void Dispose() => AppType.ShowUserMessage = _originalShowUserMessage;

    [Fact]
    public void TryHandleUnhandledException_ShowsUserFriendlyMessage_ForRecoverableErrors()
    {
        string? shown = null;
        AppType.ShowUserMessage = message => shown = message;

        var handled = AppType.TryHandleUnhandledException(new InvalidOperationException("Could not export the file."));

        Assert.True(handled);
        Assert.NotNull(shown);
        Assert.Contains("UCoverCraft", shown!);
        Assert.Contains("Could not export the file.", shown!);
    }

    [Fact]
    public void BuildUnhandledErrorMessage_HidesTheStackTrace()
    {
        var exception = new InvalidOperationException("Could not export the file.");

        var message = AppType.BuildUnhandledErrorMessage(exception);

        Assert.Contains("unexpected problem", message);
        Assert.Contains("Could not export the file.", message);
        Assert.DoesNotContain("   at ", message);
        Assert.DoesNotContain("StackTrace", message);
        Assert.DoesNotContain(exception.GetType().Name, message);
    }

    [Fact]
    public void BuildUnhandledErrorMessage_SurvivesAnEmptyExceptionMessage()
    {
        var message = AppType.BuildUnhandledErrorMessage(new InvalidOperationException("   "));

        Assert.Contains("UCoverCraft", message);
        Assert.DoesNotContain("Details:", message);
    }

    [Theory]
    [InlineData(typeof(OutOfMemoryException))]
    [InlineData(typeof(StackOverflowException))]
    [InlineData(typeof(AccessViolationException))]
    [InlineData(typeof(AppDomainUnloadedException))]
    public void TryHandleUnhandledException_DoesNotHandleFatalErrors(Type exceptionType)
    {
        var shown = false;
        AppType.ShowUserMessage = _ => shown = true;
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        var handled = AppType.TryHandleUnhandledException(exception);

        Assert.False(handled);
        Assert.False(shown);
    }

    [Fact]
    public void TryHandleUnhandledException_DoesNotHandleFatalInnerExceptions()
    {
        var shown = false;
        AppType.ShowUserMessage = _ => shown = true;
        var exception = new InvalidOperationException("outer failure", new OutOfMemoryException());

        var handled = AppType.TryHandleUnhandledException(exception);

        Assert.False(handled);
        Assert.False(shown);
    }

    [Fact]
    public void OnDispatcherUnhandledException_MarksRecoverableExceptionHandled()
    {
        string? shown = null;
        AppType.ShowUserMessage = message => shown = message;
        var eventArgs = CreateEventArgs(new InvalidOperationException("boom"));

        AppType.OnDispatcherUnhandledException(null, eventArgs);

        Assert.True(eventArgs.Handled);
        Assert.NotNull(shown);
        Assert.Contains("boom", shown!);
    }

    [Fact]
    public void OnDispatcherUnhandledException_LeavesFatalExceptionUnhandled()
    {
        var shown = false;
        AppType.ShowUserMessage = _ => shown = true;
        var eventArgs = CreateEventArgs(new OutOfMemoryException());

        AppType.OnDispatcherUnhandledException(null, eventArgs);

        Assert.False(eventArgs.Handled);
        Assert.False(shown);
    }

    private static DispatcherUnhandledExceptionEventArgs CreateEventArgs(Exception exception)
    {
        var argsType = typeof(DispatcherUnhandledExceptionEventArgs);
        var constructor = argsType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var eventArgs = (DispatcherUnhandledExceptionEventArgs)constructor.Invoke([Dispatcher.CurrentDispatcher]);

        var exceptionField = argsType.GetField("_exception", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(exceptionField);
        exceptionField.SetValue(eventArgs, exception);
        return eventArgs;
    }
}
