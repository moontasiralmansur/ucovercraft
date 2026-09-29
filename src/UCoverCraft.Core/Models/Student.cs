namespace UCoverCraft.Core.Models;

public sealed class Student
{
    public string Name { get; set; } = string.Empty;

    public string? StudentId { get; set; }

    public bool IsValid => Validate().Count == 0;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        ModelValidation.RequireText(errors, Name, nameof(Name));
        return errors;
    }
}
