using UCoverCraft.Core.Mvvm;

namespace UCoverCraft.Tests.Mvvm;

public class ObservableObjectTests
{
    private sealed class Sample : ObservableObject
    {
        private string? _name;

        public string? Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public void Notify() => OnPropertyChanged();
    }

    [Fact]
    public void SetProperty_AssignsValue()
    {
        var sample = new Sample();

        sample.Name = "cover";

        Assert.Equal("cover", sample.Name);
    }

    [Fact]
    public void SetProperty_RaisesPropertyChanged_WithPropertyName()
    {
        var sample = new Sample();
        string? raisedFor = null;
        sample.PropertyChanged += (_, e) => raisedFor = e.PropertyName;

        sample.Name = "cover";

        Assert.Equal(nameof(Sample.Name), raisedFor);
    }

    [Fact]
    public void SetProperty_DoesNotRaisePropertyChanged_WhenValueIsUnchanged()
    {
        var sample = new Sample { Name = "cover" };
        var raised = 0;
        sample.PropertyChanged += (_, _) => raised++;

        sample.Name = "cover";

        Assert.Equal(0, raised);
    }

    [Fact]
    public void OnPropertyChanged_UsesCallerMemberName_WhenNotSpecified()
    {
        var sample = new Sample();
        string? raisedFor = null;
        sample.PropertyChanged += (_, e) => raisedFor = e.PropertyName;

        sample.Notify();

        Assert.Equal(nameof(Sample.Notify), raisedFor);
    }

    [Fact]
    public void PropertyChanged_HasNoSubscribers_DoesNotThrow()
    {
        var sample = new Sample();

        sample.Name = "cover";

        Assert.Equal("cover", sample.Name);
    }
}
