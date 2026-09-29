using UCoverCraft.App.ViewModels;

namespace UCoverCraft.Tests.ViewModels;

public class StudentViewModelTests
{
    [Fact]
    public void Name_RaisesChangedAndPropertyChanged()
    {
        var student = new StudentViewModel();
        var changed = 0;
        var raised = new List<string?>();
        student.Changed += (_, _) => changed++;
        student.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        student.Name = "Jane Doe";

        Assert.Equal(1, changed);
        Assert.Contains(nameof(StudentViewModel.Name), raised);
        Assert.Equal("Jane Doe", student.Name);
    }

    [Fact]
    public void Name_TreatsNullAsEmpty()
    {
        var student = new StudentViewModel();

        student.Name = null!;

        Assert.Equal(string.Empty, student.Name);
    }

    [Fact]
    public void StudentId_RaisesChanged()
    {
        var student = new StudentViewModel();
        var changed = 0;
        student.Changed += (_, _) => changed++;

        student.StudentId = "S-1001";

        Assert.Equal(1, changed);
        Assert.Equal("S-1001", student.StudentId);
    }

    [Fact]
    public void NameError_NotifiesWhenSet()
    {
        var student = new StudentViewModel();
        var raised = new List<string?>();
        student.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        student.NameError = "Name is required.";

        Assert.Equal("Name is required.", student.NameError);
        Assert.Contains(nameof(StudentViewModel.NameError), raised);
    }

    [Fact]
    public void ToModel_MapsNameAndStudentId()
    {
        var student = new StudentViewModel
        {
            Name = "Jane Doe",
            StudentId = "S-1001",
        };

        var model = student.ToModel();

        Assert.Equal("Jane Doe", model.Name);
        Assert.Equal("S-1001", model.StudentId);
    }
}
