using UCoverCraft.Core.Mvvm;
using UCoverCraft.Core.Models;

namespace UCoverCraft.App.ViewModels;

public sealed class StudentViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string? _studentId;
    private string _nameError = string.Empty;

    public event EventHandler? Changed;

    public string Name
    {
        get => _name;
        set
        {
            SetProperty(ref _name, value ?? string.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? StudentId
    {
        get => _studentId;
        set
        {
            SetProperty(ref _studentId, value);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string NameError
    {
        get => _nameError;
        set => SetProperty(ref _nameError, value);
    }

    public Student ToModel() => new()
    {
        Name = Name,
        StudentId = StudentId,
    };
}
