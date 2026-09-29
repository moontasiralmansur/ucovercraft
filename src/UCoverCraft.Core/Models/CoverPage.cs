namespace UCoverCraft.Core.Models;

public sealed class CoverPage
{
    public const int MaxStudents = 10;

    public string DocumentTitle { get; set; } = string.Empty;

    public string CourseTitle { get; set; } = string.Empty;

    public string CourseCode { get; set; } = string.Empty;

    public string? Section { get; set; }

    public Instructor? SubmittedTo { get; set; }

    public List<Student> Students { get; set; } = [];

    public DateOnly SubmissionDate { get; set; }

    public bool IsValid => Validate().Count == 0;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        ModelValidation.RequireText(errors, DocumentTitle, nameof(DocumentTitle));
        ModelValidation.RequireText(errors, CourseTitle, nameof(CourseTitle));
        ModelValidation.RequireText(errors, CourseCode, nameof(CourseCode));

        if (SubmittedTo is null)
        {
            errors.Add($"{nameof(SubmittedTo)} is required.");
        }
        else
        {
            foreach (var error in SubmittedTo.Validate())
            {
                errors.Add($"{nameof(SubmittedTo)}.{error}");
            }
        }

        if (Students.Count == 0)
        {
            errors.Add("At least one student is required.");
        }
        else
        {
            for (var i = 0; i < Students.Count; i++)
            {
                foreach (var error in Students[i].Validate())
                {
                    errors.Add($"Students[{i}].{error}");
                }
            }
        }

        if (Students.Count > MaxStudents)
        {
            errors.Add($"At most {MaxStudents} students fit on one cover page.");
        }

        if (SubmissionDate == default)
        {
            errors.Add($"{nameof(SubmissionDate)} is required.");
        }

        return errors;
    }
}
