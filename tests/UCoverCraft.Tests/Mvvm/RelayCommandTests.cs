using UCoverCraft.Core.Mvvm;

namespace UCoverCraft.Tests.Mvvm;

public class RelayCommandTests
{
    [Fact]
    public void Execute_InvokesAction()
    {
        var executed = 0;
        var command = new RelayCommand(() => executed++);

        command.Execute(null);

        Assert.Equal(1, executed);
    }

    [Fact]
    public void CanExecute_ReturnsTrue_WhenNoPredicateIsProvided()
    {
        var command = new RelayCommand(() => { });

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void CanExecute_UsesPredicate_WhenProvided()
    {
        var allowed = false;
        var command = new RelayCommand(() => { }, () => allowed);

        Assert.False(command.CanExecute(null));

        allowed = true;

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void RaiseCanExecuteChanged_RaisesEvent()
    {
        var command = new RelayCommand(() => { });
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Constructor_Throws_WhenExecuteIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
    }

    [Fact]
    public void Constructor_Throws_WhenTypedExecuteIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(null!));
    }
}

public class RelayCommandOfTTests
{
    [Fact]
    public void Execute_PassesParameterToAction()
    {
        string? received = null;
        var command = new RelayCommand<string>(value => received = value);

        command.Execute("cover-page");

        Assert.Equal("cover-page", received);
    }

    [Fact]
    public void Execute_PassesNull_WhenParameterIsNull()
    {
        string? received = "not-set";
        var command = new RelayCommand<string?>(value => received = value);

        command.Execute(null);

        Assert.Null(received);
    }

    [Fact]
    public void CanExecute_UsesPredicateWithParameter()
    {
        var command = new RelayCommand<string>(value => { }, value => value is not null);

        Assert.False(command.CanExecute(null));
        Assert.True(command.CanExecute("cover-page"));
    }

    [Fact]
    public void RaiseCanExecuteChanged_RaisesEvent()
    {
        var command = new RelayCommand<int>(_ => { });
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
    }
}
