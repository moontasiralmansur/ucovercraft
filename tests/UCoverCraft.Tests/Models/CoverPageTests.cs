using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Tests.Models;

public class CoverPageTests
{
    private static readonly DateOnly SubmissionDate = new(2026, 9, 25);

    private static CoverPage CreateValidPage() => new()
    {
        DocumentTitle = "Smart Campus Navigation",
        CourseTitle = "Software Engineering",
        CourseCode = "CSE-401",
        Section = "A",
        SubmittedTo = new Instructor { Name = "Dr. Rahman", Department = "CSE" },
        Students =
        [
            new Student { Name = "Jane Doe", StudentId = "S-1001" },
            new Student { Name = "John Smith", StudentId = "S-1002" },
        ],
        SubmissionDate = SubmissionDate,
    };

    [Fact]
    public void Validate_ReturnsNoErrors_ForValidPage()
    {
        var page = CreateValidPage();

        Assert.Empty(page.Validate());
        Assert.True(page.IsValid);
    }

    [Fact]
    public void Validate_ReturnsError_ForEachMissingRequiredTextField()
    {
        var page = CreateValidPage();
        page.DocumentTitle = " ";
        page.CourseTitle = string.Empty;
        page.CourseCode = null!;

        var errors = page.Validate();

        Assert.Equal(3, errors.Count);
        Assert.StartsWith(nameof(CoverPage.DocumentTitle), errors[0]);
        Assert.StartsWith(nameof(CoverPage.CourseTitle), errors[1]);
        Assert.StartsWith(nameof(CoverPage.CourseCode), errors[2]);
    }

    [Fact]
    public void Validate_ReturnsError_WhenSubmittedToIsMissing()
    {
        var page = CreateValidPage();
        page.SubmittedTo = null;

        var errors = page.Validate();

        Assert.Single(errors);
        Assert.StartsWith(nameof(CoverPage.SubmittedTo), errors[0]);
    }

    [Fact]
    public void Validate_ReportsSubmittedToFieldErrors()
    {
        var page = CreateValidPage();
        page.SubmittedTo = new Instructor { Name = "" };

        var errors = page.Validate();

        Assert.Single(errors);
        Assert.StartsWith($"{nameof(CoverPage.SubmittedTo)}.{nameof(Instructor.Name)}", errors[0]);
    }

    [Fact]
    public void Validate_ReturnsError_WhenThereAreNoStudents()
    {
        var page = CreateValidPage();
        page.Students.Clear();

        var errors = page.Validate();

        Assert.Single(errors);
        Assert.Contains("student", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ReportsStudentErrorsWithIndex()
    {
        var page = CreateValidPage();
        page.Students[1] = new Student { Name = "  " };

        var errors = page.Validate();

        Assert.Single(errors);
        Assert.StartsWith($"Students[1].{nameof(Student.Name)}", errors[0]);
    }

    [Fact]
    public void Validate_ReturnsError_WhenSubmissionDateIsMissing()
    {
        var page = CreateValidPage();
        page.SubmissionDate = default;

        var errors = page.Validate();

        Assert.Single(errors);
        Assert.StartsWith(nameof(CoverPage.SubmissionDate), errors[0]);
    }

    [Fact]
    public void Validate_CollectsAllErrors_AtOnce()
    {
        var page = new CoverPage();

        var errors = page.Validate();

        Assert.Equal(6, errors.Count);
    }

    [Fact]
    public void Section_IsOptional()
    {
        var page = CreateValidPage();
        page.Section = null;

        Assert.Empty(page.Validate());
    }

    [Fact]
    public void Students_SupportVaryingCount()
    {
        var page = CreateValidPage();
        Assert.Equal(2, page.Students.Count);

        page.Students.Add(new Student { Name = "Amina Noor" });
        page.Students.RemoveAt(0);

        Assert.Equal(2, page.Students.Count);
        Assert.Equal("Amina Noor", page.Students[1].Name);
        Assert.Empty(page.Validate());
    }

    [Fact]
    public void SubmissionDate_IsDateOnly()
    {
        var page = CreateValidPage();
        object boxed = page.SubmissionDate;

        Assert.IsType<DateOnly>(boxed);
        Assert.Equal(SubmissionDate, page.SubmissionDate);
    }

    [Fact]
    public void MaxStudents_IsTen_AndStaysWithinTheLayoutSafetyMaximum()
    {
        Assert.Equal(10, CoverPage.MaxStudents);
        Assert.True(CoverPage.MaxStudents <= CoverPageLayout.MaxStudents);
    }

    [Fact]
    public void Validate_AcceptsTenStudents()
    {
        var page = CreateValidPage();
        page.Students = CreateStudents(10);

        Assert.Empty(page.Validate());
        Assert.True(page.IsValid);
    }

    [Fact]
    public void Validate_RejectsElevenStudents()
    {
        var page = CreateValidPage();
        page.Students = CreateStudents(11);

        var errors = page.Validate();

        Assert.Contains(
            errors,
            error => error.Contains("At most 10", StringComparison.Ordinal));
    }

    private static List<Student> CreateStudents(int count) =>
        Enumerable
            .Range(1, count)
            .Select(index => new Student { Name = $"Student {index}", StudentId = $"S-{index:0000}" })
            .ToList();
}
