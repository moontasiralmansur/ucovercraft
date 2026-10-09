using UCoverCraft.App.ViewModels;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Tests.ViewModels;

public class MainViewModelTests
{
    private static MainViewModel CreatePopulatedViewModel()
    {
        var viewModel = new MainViewModel
        {
            CourseTitle = "Software Engineering",
            CourseCode = "CSE-401",
            Section = "A",
            SubmittedToName = "Dr. Rahman",
            SubmittedToDesignation = "Lecturer",
            SubmittedToDepartment = "CSE",
            SubmissionDay = "25",
            SubmissionMonth = "9",
            SubmissionYear = "2026",
        };
        SelectDocumentType(viewModel, DocumentType.LabReport);

        viewModel.Students[0].Name = "Jane Doe";
        viewModel.Students[0].StudentId = "S-1001";
        return viewModel;
    }

    private static void SelectDocumentType(MainViewModel viewModel, DocumentType type) =>
        viewModel.SelectedDocumentType = viewModel.DocumentTypes.First(option => option.Value == type);

    private static string RenderedText(CoverPreviewViewModel preview) =>
        string.Join(
            "|",
            preview.Sections
                .SelectMany(section => section.Lines)
                .SelectMany(line => line.Runs)
                .Select(run => run.Text));

    private static string[] SubmittedToLines(MainViewModel viewModel) =>
        viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.SubmittedTo)
            .Lines
            .Select(line => string.Concat(line.Runs.Select(run => run.Text)))
            .ToArray();

    [Fact]
    public void DocumentTypes_AreInTheRequestedOrder()
    {
        var viewModel = new MainViewModel();

        Assert.Equal(
            new[] { "ASSIGNMENT", "LAB REPORT", "PROJECT REPORT", "CUSTOM" },
            viewModel.DocumentTypes.Select(option => option.Label));
        Assert.Equal(
            new[]
            {
                DocumentType.Assignment,
                DocumentType.LabReport,
                DocumentType.ProjectReport,
                DocumentType.Custom,
            },
            viewModel.DocumentTypes.Select(option => option.Value));
    }

    [Fact]
    public void SelectedDocumentType_IsNothingOnStartup()
    {
        var viewModel = new MainViewModel();

        Assert.Null(viewModel.SelectedDocumentType);
        Assert.False(viewModel.IsCustomTitleEditable);
    }

    [Fact]
    public void DocumentTitle_IsEmptyOnStartup_AndHiddenFromThePreview()
    {
        var viewModel = new MainViewModel();

        Assert.Equal(string.Empty, viewModel.DocumentTitle);
        Assert.Equal(string.Empty, viewModel.BuildCoverPage().DocumentTitle);

        var title = viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.DocumentTitle);

        Assert.Equal(
            string.Empty,
            string.Concat(title.Lines.SelectMany(line => line.Runs.Select(run => run.Text))));
    }

    [Fact]
    public void Preview_ShowsTheSectionLine_WhenSectionIsEmpty()
    {
        var viewModel = new MainViewModel();

        var course = viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.CourseInformation);

        Assert.Equal(3, course.Lines.Count);
        Assert.Equal(
            "Course Title: |Course Code: |Section: ",
            string.Join("|", course.Lines.Select(line => string.Concat(line.Runs.Select(run => run.Text)))));
    }

    [Theory]
    [InlineData(DocumentType.ProjectReport, "PROJECT REPORT")]
    [InlineData(DocumentType.Assignment, "ASSIGNMENT")]
    public void SelectingPreset_UsesPresetTitle(DocumentType type, string expectedTitle)
    {
        var viewModel = new MainViewModel();

        SelectDocumentType(viewModel, type);

        Assert.Equal(expectedTitle, viewModel.DocumentTitle);
        Assert.False(viewModel.IsCustomTitleEditable);
        Assert.Equal(expectedTitle, viewModel.BuildCoverPage().DocumentTitle);
    }

    [Fact]
    public void SelectingCustom_ClearsTitle_AndAllowsCustomEntry()
    {
        var viewModel = new MainViewModel();

        SelectDocumentType(viewModel, DocumentType.Custom);

        Assert.True(viewModel.IsCustomTitleEditable);
        Assert.Equal(string.Empty, viewModel.DocumentTitle);

        viewModel.DocumentTitle = "Smart Campus Navigation";

        Assert.Equal("Smart Campus Navigation", viewModel.DocumentTitle);
        Assert.Equal("Smart Campus Navigation", viewModel.BuildCoverPage().DocumentTitle);
    }

    [Fact]
    public void PresetTitle_IgnoresDirectEdits()
    {
        var viewModel = new MainViewModel();
        SelectDocumentType(viewModel, DocumentType.LabReport);

        viewModel.DocumentTitle = "Something Else";

        Assert.Equal("LAB REPORT", viewModel.DocumentTitle);
    }

    [Fact]
    public void SwitchingBackToPreset_ReplacesCustomTitle()
    {
        var viewModel = new MainViewModel();
        SelectDocumentType(viewModel, DocumentType.Custom);
        viewModel.DocumentTitle = "Thesis Report";

        SelectDocumentType(viewModel, DocumentType.Assignment);

        Assert.Equal("ASSIGNMENT", viewModel.DocumentTitle);
    }

    [Fact]
    public void BuildCoverPage_MapsAllFormValues()
    {
        var viewModel = CreatePopulatedViewModel();

        var page = viewModel.BuildCoverPage();

        Assert.Equal("LAB REPORT", page.DocumentTitle);
        Assert.Equal("Software Engineering", page.CourseTitle);
        Assert.Equal("CSE-401", page.CourseCode);
        Assert.Equal("A", page.Section);
        Assert.NotNull(page.SubmittedTo);
        Assert.Equal("Dr. Rahman", page.SubmittedTo.Name);
        Assert.Equal("Lecturer", page.SubmittedTo.Designation);
        Assert.Equal("CSE", page.SubmittedTo.Department);
        Assert.Single(page.Students);
        Assert.Equal("Jane Doe", page.Students[0].Name);
        Assert.Equal("S-1001", page.Students[0].StudentId);
        Assert.Equal(new DateOnly(2026, 9, 25), page.SubmissionDate);
    }

    [Fact]
    public void CourseFields_AreEditable()
    {
        var viewModel = new MainViewModel();

        viewModel.CourseTitle = "Software Engineering";
        viewModel.CourseCode = "CSE-401";
        viewModel.Section = "B";

        Assert.Equal("Software Engineering", viewModel.CourseTitle);
        Assert.Equal("CSE-401", viewModel.CourseCode);
        Assert.Equal("B", viewModel.Section);
    }

    [Fact]
    public void Students_StartWithSingleEmptyStudent()
    {
        var viewModel = new MainViewModel();

        var student = Assert.Single(viewModel.Students);
        Assert.Equal(string.Empty, student.Name);
        Assert.False(viewModel.RemoveStudentCommand.CanExecute(student));
    }

    [Fact]
    public void AddStudentCommand_AppendsStudent_AndReportsMissingName()
    {
        var viewModel = new MainViewModel();

        viewModel.AddStudentCommand.Execute(null);

        Assert.Equal(2, viewModel.Students.Count);
        Assert.Equal("Name is required.", viewModel.Students[1].NameError);
        Assert.Contains("Student 2: Name is required.", viewModel.ValidationErrors);
        Assert.True(viewModel.RemoveStudentCommand.CanExecute(viewModel.Students[1]));
    }

    [Fact]
    public void RemoveStudentCommand_RemovesStudent_WhenMoreThanOneRemain()
    {
        var viewModel = CreatePopulatedViewModel();
        viewModel.AddStudentCommand.Execute(null);

        viewModel.RemoveStudentCommand.Execute(viewModel.Students[1]);

        Assert.Single(viewModel.Students);
        Assert.Equal("Jane Doe", viewModel.Students[0].Name);
    }

    [Fact]
    public void RemoveStudentCommand_PreventsRemovingLastStudent()
    {
        var viewModel = new MainViewModel();
        var student = viewModel.Students[0];

        Assert.False(viewModel.RemoveStudentCommand.CanExecute(student));

        viewModel.RemoveStudentCommand.Execute(student);

        Assert.Same(student, Assert.Single(viewModel.Students));
    }

    [Fact]
    public void RemoveStudentCommand_RaisesCanExecuteChanged_WhenStudentsChange()
    {
        var viewModel = new MainViewModel();
        var raised = 0;
        viewModel.RemoveStudentCommand.CanExecuteChanged += (_, _) => raised++;

        viewModel.AddStudentCommand.Execute(null);
        viewModel.RemoveStudentCommand.Execute(viewModel.Students[1]);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void StudentNameError_AppearsAfterEdit_AndClearsWhenFilled()
    {
        var viewModel = CreatePopulatedViewModel();
        Assert.Equal(string.Empty, viewModel.Students[0].NameError);

        viewModel.Students[0].Name = string.Empty;

        Assert.Equal("Name is required.", viewModel.Students[0].NameError);
        Assert.Contains("Student 1: Name is required.", viewModel.ValidationErrors);

        viewModel.Students[0].Name = "Jane Doe";

        Assert.Equal(string.Empty, viewModel.Students[0].NameError);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Theory]
    [InlineData("25", "9", "2026", 2026, 9, 25)]
    [InlineData("29", "2", "2024", 2024, 2, 29)]
    [InlineData("1", "12", "1999", 1999, 12, 1)]
    public void SubmissionDate_AcceptsValidDates(string day, string month, string year, int y, int m, int d)
    {
        var viewModel = new MainViewModel();

        viewModel.SubmissionDay = day;
        viewModel.SubmissionMonth = month;
        viewModel.SubmissionYear = year;

        Assert.Equal(string.Empty, viewModel.SubmissionDateError);
        Assert.Equal(new DateOnly(y, m, d), viewModel.BuildCoverPage().SubmissionDate);
    }

    [Theory]
    [InlineData("32", "1", "2026")]
    [InlineData("31", "2", "2026")]
    [InlineData("0", "1", "2026")]
    [InlineData("15", "13", "2026")]
    [InlineData("abc", "1", "2026")]
    [InlineData("25", "", "2026")]
    public void SubmissionDate_ReportsInvalid_ForUnusableValues(string day, string month, string year)
    {
        var viewModel = new MainViewModel();

        viewModel.SubmissionDay = day;
        viewModel.SubmissionMonth = month;
        viewModel.SubmissionYear = year;

        Assert.Equal("Submission date is invalid.", viewModel.SubmissionDateError);
        Assert.Contains("Submission date is invalid.", viewModel.ValidationErrors);
        Assert.Equal(default(DateOnly), viewModel.BuildCoverPage().SubmissionDate);
    }

    [Fact]
    public void SubmissionDate_ReportsRequired_WhenEmptyAfterEdit()
    {
        var viewModel = new MainViewModel();

        viewModel.SubmissionDay = string.Empty;

        Assert.Equal("Submission date is required.", viewModel.SubmissionDateError);
        Assert.Contains("Submission date is required.", viewModel.ValidationErrors);
    }

    [Fact]
    public void SubmissionDateError_Clears_WhenValidDateEntered()
    {
        var viewModel = new MainViewModel();
        viewModel.SubmissionDay = "45";
        Assert.True(viewModel.HasErrors);

        viewModel.SubmissionDay = "25";
        viewModel.SubmissionMonth = "9";
        viewModel.SubmissionYear = "2026";

        Assert.Equal(string.Empty, viewModel.SubmissionDateError);
        Assert.False(viewModel.HasErrors);
    }

    [Fact]
    public void SubmissionDate_StartsUnset_OnStartup()
    {
        var viewModel = new MainViewModel();

        Assert.Equal(string.Empty, viewModel.SubmissionDay);
        Assert.Equal(string.Empty, viewModel.SubmissionMonth);
        Assert.Equal(string.Empty, viewModel.SubmissionYear);
        Assert.Equal(default(DateOnly), viewModel.BuildCoverPage().SubmissionDate);
        Assert.Equal(string.Empty, viewModel.SubmissionDateError);
    }

    [Fact]
    public void Preview_ShowsTheEmptyDateLabel_OnStartup()
    {
        var viewModel = new MainViewModel();

        var rendered = RenderedText(viewModel.Preview);
        var dateSection = viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.SubmissionDate);
        var line = Assert.Single(dateSection.Lines);

        Assert.Equal("Date of Submission: ", string.Concat(line.Runs.Select(run => run.Text)));
        Assert.Contains("Date of Submission: ", rendered);
        Assert.DoesNotContain("1 January", rendered);
        Assert.DoesNotContain("0001", rendered);
    }

    [Fact]
    public void Preview_ShowsTheSubmissionDateLine_WhenADateIsSupplied()
    {
        var viewModel = new MainViewModel();
        viewModel.SubmissionDay = "13";
        viewModel.SubmissionMonth = "6";
        viewModel.SubmissionYear = "2026";

        var dateSection = viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.SubmissionDate);
        var line = Assert.Single(dateSection.Lines);

        Assert.Equal(
            "Date of Submission: 13 June, 2026",
            string.Concat(line.Runs.Select(run => run.Text)));
        Assert.Contains("Date of Submission: ", RenderedText(viewModel.Preview));
        Assert.Contains("13 June, 2026", RenderedText(viewModel.Preview));
    }

    [Fact]
    public void Preview_RendersTheValidDateExactly()
    {
        var viewModel = new MainViewModel();
        viewModel.SubmissionDay = "30";
        viewModel.SubmissionMonth = "12";
        viewModel.SubmissionYear = "2026";

        var dateSection = viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.SubmissionDate);
        var line = Assert.Single(dateSection.Lines);

        Assert.Equal("Date of Submission: ", line.Runs[0].Text);
        Assert.Equal("30 December, 2026", line.Runs[1].Text);
        Assert.Equal(
            "Date of Submission: 30 December, 2026",
            string.Concat(line.Runs.Select(run => run.Text)));
        Assert.Contains("Date of Submission: ", RenderedText(viewModel.Preview));
        Assert.Contains("30 December, 2026", RenderedText(viewModel.Preview));
    }

    [Fact]
    public void Preview_UsesTheExistingDateFormat_WhenADateIsSupplied()
    {
        var viewModel = new MainViewModel();
        viewModel.SubmissionDay = "13";
        viewModel.SubmissionMonth = "6";
        viewModel.SubmissionYear = "2026";

        var dateSection = viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.SubmissionDate);
        var line = Assert.Single(dateSection.Lines);

        Assert.Equal(
            CoverContentBuilder.FormatSubmissionDate(new DateOnly(2026, 6, 13)),
            line.Runs[1].Text);
        Assert.Equal("13 June, 2026", line.Runs[1].Text);
    }

    [Theory]
    [InlineData("13", "", "2026")]
    [InlineData("", "6", "2026")]
    [InlineData("13", "6", "")]
    [InlineData("", "", "2026")]
    [InlineData("32", "1", "2026")]
    [InlineData("31", "2", "2026")]
    public void Preview_KeepsOnlyTheDateLabel_ForIncompleteAndInvalidDates(
        string day,
        string month,
        string year)
    {
        var viewModel = new MainViewModel();
        viewModel.SubmissionDay = day;
        viewModel.SubmissionMonth = month;
        viewModel.SubmissionYear = year;

        Assert.Equal("Submission date is invalid.", viewModel.SubmissionDateError);
        Assert.Equal(default(DateOnly), viewModel.BuildCoverPage().SubmissionDate);

        var dateSection = viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.SubmissionDate);
        var rendered = RenderedText(viewModel.Preview);
        var line = Assert.Single(dateSection.Lines);

        Assert.Equal("Date of Submission: ", string.Concat(line.Runs.Select(run => run.Text)));
        Assert.Equal(string.Empty, line.Runs[1].Text);
        Assert.Contains("Date of Submission: ", rendered);
        Assert.DoesNotContain("1 January", rendered);
        Assert.DoesNotContain("0001", rendered);
    }

    [Fact]
    public void Preview_ClearsTheDateValue_WhenTheDateIsCleared()
    {
        var viewModel = CreatePopulatedViewModel();
        Assert.Contains("25 September, 2026", RenderedText(viewModel.Preview));

        viewModel.SubmissionDay = string.Empty;

        Assert.Equal(default(DateOnly), viewModel.BuildCoverPage().SubmissionDate);

        var dateSection = viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.SubmissionDate);
        var line = Assert.Single(dateSection.Lines);

        Assert.Equal("Date of Submission: ", string.Concat(line.Runs.Select(run => run.Text)));
        Assert.DoesNotContain("25 September, 2026", RenderedText(viewModel.Preview));
        Assert.DoesNotContain("0001", RenderedText(viewModel.Preview));
    }

    [Fact]
    public void Preview_ShowsTheSubmissionDateLine_AgainAfterTheDateIsRestored()
    {
        var viewModel = CreatePopulatedViewModel();
        viewModel.SubmissionDay = string.Empty;

        viewModel.SubmissionDay = "25";

        Assert.Contains("Date of Submission: ", RenderedText(viewModel.Preview));
        Assert.Contains("25 September, 2026", RenderedText(viewModel.Preview));
    }

    [Fact]
    public void FreshViewModel_HasNoDisplayedErrors()
    {
        var viewModel = new MainViewModel();

        Assert.False(viewModel.HasErrors);
        Assert.Empty(viewModel.ValidationErrors);
        Assert.Equal(string.Empty, viewModel.CourseTitleError);
        Assert.Equal(string.Empty, viewModel.SubmissionDateError);
    }

    [Fact]
    public void EditingEmptyRequiredField_ShowsFieldErrorAndSummary()
    {
        var viewModel = new MainViewModel();

        viewModel.CourseTitle = string.Empty;

        Assert.Equal("Course title is required.", viewModel.CourseTitleError);
        Assert.True(viewModel.HasErrors);
        Assert.Equal("Course title is required.", Assert.Single(viewModel.ValidationErrors));
    }

    [Fact]
    public void UntouchedInvalidFields_AreNotDisplayed()
    {
        var viewModel = new MainViewModel();

        viewModel.CourseTitle = "Software Engineering";

        Assert.False(viewModel.HasErrors);
        Assert.Equal(string.Empty, viewModel.CourseCodeError);
        Assert.Equal(string.Empty, viewModel.SubmittedToNameError);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Fact]
    public void ErrorClears_WhenFieldBecomesValid()
    {
        var viewModel = new MainViewModel();
        viewModel.CourseTitle = string.Empty;
        Assert.True(viewModel.HasErrors);

        viewModel.CourseTitle = "Software Engineering";

        Assert.Equal(string.Empty, viewModel.CourseTitleError);
        Assert.False(viewModel.HasErrors);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Fact]
    public void ErrorProperties_RaisePropertyChanged()
    {
        var viewModel = new MainViewModel();
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        viewModel.CourseTitle = "Software Engineering";
        raised.Clear();

        viewModel.CourseTitle = string.Empty;

        Assert.Contains(nameof(MainViewModel.CourseTitle), raised);
        Assert.Contains(nameof(MainViewModel.CourseTitleError), raised);
        Assert.Contains(nameof(MainViewModel.HasErrors), raised);
    }

    [Fact]
    public void MissingInstructorName_ReportsError()
    {
        var viewModel = CreatePopulatedViewModel();

        viewModel.SubmittedToName = string.Empty;

        Assert.Equal("Instructor name is required.", viewModel.SubmittedToNameError);
        Assert.Contains("Instructor name is required.", viewModel.ValidationErrors);

        viewModel.SubmittedToName = "Dr. Rahman";

        Assert.Equal(string.Empty, viewModel.SubmittedToNameError);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Fact]
    public void Validate_ReturnsFalse_AndRevealsAllErrors()
    {
        var viewModel = new MainViewModel();

        Assert.False(viewModel.Validate());

        Assert.True(viewModel.HasErrors);
        Assert.Equal("Document title is required.", viewModel.DocumentTitleError);
        Assert.Equal("Course title is required.", viewModel.CourseTitleError);
        Assert.Equal("Course code is required.", viewModel.CourseCodeError);
        Assert.Equal("Instructor name is required.", viewModel.SubmittedToNameError);
        Assert.Equal("Submission date is required.", viewModel.SubmissionDateError);
        Assert.Contains("Student 1: Name is required.", viewModel.ValidationErrors);
    }

    [Fact]
    public void Validate_ReturnsTrue_ForFullyPopulatedForm()
    {
        var viewModel = CreatePopulatedViewModel();

        Assert.True(viewModel.Validate());

        Assert.False(viewModel.HasErrors);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Fact]
    public void Preview_ReflectsPopulatedFormValues()
    {
        var viewModel = CreatePopulatedViewModel();

        var rendered = RenderedText(viewModel.Preview);

        Assert.Contains("LAB REPORT", rendered);
        Assert.Contains("Software Engineering", rendered);
        Assert.Contains("Dr. Rahman", rendered);
        Assert.Contains("Jane Doe (S-1001)", rendered);
        Assert.Contains("25 September, 2026", rendered);
    }

    [Fact]
    public void Preview_UpdatesWhenCourseTitleChanges()
    {
        var viewModel = CreatePopulatedViewModel();

        viewModel.CourseTitle = "Operating Systems";

        Assert.Contains("Operating Systems", RenderedText(viewModel.Preview));
    }

    [Fact]
    public void Preview_UpdatesWhenInstructorFieldsChange()
    {
        var viewModel = CreatePopulatedViewModel();
        string[] expected = ["Submitted to:", "Dr. Rahman", "Lecturer", "CSE"];

        Assert.Equal(expected, SubmittedToLines(viewModel));

        viewModel.SubmittedToDesignation = "Professor";
        viewModel.SubmittedToDepartment = "ECE";

        string[] updated = ["Submitted to:", "Dr. Rahman", "Professor", "ECE"];
        Assert.Equal(updated, SubmittedToLines(viewModel));
    }

    [Fact]
    public void Preview_UpdatesWhenSectionChanges()
    {
        var viewModel = CreatePopulatedViewModel();

        viewModel.Section = "B";
        var course = viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.CourseInformation);

        Assert.Equal("B", course.Lines[2].Runs[1].Text);
    }

    [Fact]
    public void Preview_UpdatesWhenDocumentTypeChanges()
    {
        var viewModel = new MainViewModel();

        SelectDocumentType(viewModel, DocumentType.Assignment);

        Assert.Contains("ASSIGNMENT", RenderedText(viewModel.Preview));
    }

    [Fact]
    public void Preview_UpdatesWhenSubmissionDateChanges()
    {
        var viewModel = CreatePopulatedViewModel();

        viewModel.SubmissionYear = "2027";

        Assert.Contains("25 September, 2027", RenderedText(viewModel.Preview));
    }

    [Fact]
    public void Preview_UpdatesWhenStudentsChange()
    {
        var viewModel = CreatePopulatedViewModel();

        viewModel.AddStudentCommand.Execute(null);
        viewModel.Students[1].Name = "Alan Turing";
        viewModel.Students[1].StudentId = "S-2001";

        Assert.Contains("Alan Turing (S-2001)", RenderedText(viewModel.Preview));

        viewModel.RemoveStudentCommand.Execute(viewModel.Students[1]);

        Assert.DoesNotContain("Alan Turing (S-2001)", RenderedText(viewModel.Preview));
    }

    [Fact]
    public void StudentLimitHint_IsEmpty_BelowTheMaximumStudentCount()
    {
        var viewModel = new MainViewModel();

        Assert.Equal(10, viewModel.MaxStudents);
        Assert.False(viewModel.IsStudentLimitReached);
        Assert.Equal(string.Empty, viewModel.StudentLimitHint);
        Assert.True(viewModel.AddStudentCommand.CanExecute(null));
    }

    [Fact]
    public void AddStudentCommand_StopsAtTenStudents_WithFeedback()
    {
        var viewModel = CreatePopulatedViewModel();

        while (viewModel.AddStudentCommand.CanExecute(null))
        {
            viewModel.AddStudentCommand.Execute(null);
            viewModel.Students[^1].Name = $"Student {viewModel.Students.Count}";
        }

        Assert.Equal(10, viewModel.Students.Count);
        Assert.False(viewModel.AddStudentCommand.CanExecute(null));
        Assert.True(viewModel.IsStudentLimitReached);
        Assert.Contains("Maximum of 10", viewModel.StudentLimitHint);
        Assert.Contains("Remove a student", viewModel.StudentLimitHint);
        Assert.DoesNotContain("24", viewModel.StudentLimitHint);

        viewModel.AddStudentCommand.Execute(null);

        Assert.Equal(10, viewModel.Students.Count);
        Assert.True(viewModel.Validate());
    }

    [Fact]
    public void Validate_AcceptsTenStudents()
    {
        var viewModel = CreatePopulatedViewModel();
        while (viewModel.AddStudentCommand.CanExecute(null))
        {
            viewModel.AddStudentCommand.Execute(null);
            viewModel.Students[^1].Name = $"Student {viewModel.Students.Count}";
        }

        Assert.Equal(10, viewModel.Students.Count);
        Assert.True(viewModel.Validate());
        Assert.False(viewModel.HasErrors);
    }

    [Fact]
    public void Validate_ReportsTheStudentLimit_WhenTheListIsPushedBeyondIt()
    {
        var viewModel = CreatePopulatedViewModel();
        while (viewModel.AddStudentCommand.CanExecute(null))
        {
            viewModel.AddStudentCommand.Execute(null);
            viewModel.Students[^1].Name = $"Student {viewModel.Students.Count}";
        }

        viewModel.Students.Add(new StudentViewModel { Name = "One too many" });

        Assert.Equal(11, viewModel.Students.Count);
        Assert.False(viewModel.Validate());
        Assert.Contains(
            viewModel.ValidationErrors,
            error => error.Contains("At most 10", StringComparison.Ordinal));
    }

    [Fact]
    public void NumberAndTitleTopic_DefaultToEmpty()
    {
        var viewModel = new MainViewModel();

        Assert.Equal(string.Empty, viewModel.Number);
        Assert.Equal(string.Empty, viewModel.TitleTopic);
        Assert.Equal(string.Empty, viewModel.NumberError);
        Assert.Equal(string.Empty, viewModel.BuildCoverPage().Number);
        Assert.Equal(string.Empty, viewModel.BuildCoverPage().TitleTopic);
    }

    [Fact]
    public void FreshViewModel_HasNoNumberError()
    {
        var viewModel = new MainViewModel();

        Assert.False(viewModel.HasErrors);
        Assert.Equal(string.Empty, viewModel.NumberError);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("01")]
    [InlineData("12")]
    [InlineData("999")]
    public void Number_AcceptsPositiveIntegers(string number)
    {
        var viewModel = CreatePopulatedViewModel();

        viewModel.Number = number;

        Assert.Equal(string.Empty, viewModel.NumberError);
        Assert.False(viewModel.HasErrors);
        Assert.True(viewModel.Validate());
        Assert.Equal(number, viewModel.BuildCoverPage().Number);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("3.5")]
    public void Number_ReportsAnError_ForNonPositiveAndNonIntegerValues(string number)
    {
        var viewModel = CreatePopulatedViewModel();

        viewModel.Number = number;

        Assert.Equal("Number must be a positive integer.", viewModel.NumberError);
        Assert.Contains("Number must be a positive integer.", viewModel.ValidationErrors);
        Assert.True(viewModel.HasErrors);
        Assert.False(viewModel.Validate());
    }

    [Fact]
    public void NumberError_Clears_WhenTheNumberBecomesValid()
    {
        var viewModel = CreatePopulatedViewModel();
        viewModel.Number = "0";
        Assert.True(viewModel.HasErrors);

        viewModel.Number = "4";

        Assert.Equal(string.Empty, viewModel.NumberError);
        Assert.False(viewModel.HasErrors);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Fact]
    public void TitleTopic_IsEditable_AndAcceptsEmptyValues()
    {
        var viewModel = CreatePopulatedViewModel();

        Assert.Equal(string.Empty, viewModel.TitleTopic);

        viewModel.TitleTopic = "Distributed Systems";

        Assert.Equal("Distributed Systems", viewModel.TitleTopic);
        Assert.True(viewModel.Validate());

        viewModel.TitleTopic = string.Empty;

        Assert.Equal(string.Empty, viewModel.TitleTopic);
        Assert.True(viewModel.Validate());
        Assert.False(viewModel.HasErrors);
    }

    [Fact]
    public void BuildCoverPage_MapsTheOptionalNumberAndTitleTopic()
    {
        var viewModel = CreatePopulatedViewModel();
        viewModel.Number = "03";
        viewModel.TitleTopic = "Compiler Design";

        var page = viewModel.BuildCoverPage();

        Assert.Equal("LAB REPORT", page.DocumentTitle);
        Assert.Equal("03", page.Number);
        Assert.Equal("Compiler Design", page.TitleTopic);
    }

    [Fact]
    public void BuildCoverPage_KeepsBothOptionalFieldsEmpty_ByDefault()
    {
        var viewModel = CreatePopulatedViewModel();

        var page = viewModel.BuildCoverPage();

        Assert.Equal(string.Empty, page.Number);
        Assert.Equal(string.Empty, page.TitleTopic);
    }

    [Fact]
    public void Preview_OmitsTheOptionalLines_WhenBothFieldsAreEmpty()
    {
        var viewModel = CreatePopulatedViewModel();

        string[] expected = ["LAB REPORT"];

        Assert.Equal(expected, DocumentTitleLines(viewModel));
        Assert.DoesNotContain(
            viewModel.Preview.Sections.SelectMany(section => section.Lines).SelectMany(line => line.Runs),
            run => run.Text == "Title: ");
    }

    [Fact]
    public void Preview_UpdatesWhenNumberChanges()
    {
        var viewModel = CreatePopulatedViewModel();

        viewModel.Number = "3";

        string[] expected = ["LAB REPORT 03"];

        Assert.Equal(expected, DocumentTitleLines(viewModel));
        Assert.Contains("LAB REPORT 03", RenderedText(viewModel.Preview));
    }

    [Fact]
    public void Preview_UpdatesWhenTitleTopicChanges()
    {
        var viewModel = CreatePopulatedViewModel();

        viewModel.TitleTopic = "Operating Systems";

        string[] expected = ["LAB REPORT", "Title: Operating Systems"];
        Assert.Equal(expected, DocumentTitleLines(viewModel));

        viewModel.TitleTopic = string.Empty;

        string[] updated = ["LAB REPORT"];
        Assert.Equal(updated, DocumentTitleLines(viewModel));
    }

    [Fact]
    public void Preview_UpdatesWhenNumberIsCleared()
    {
        var viewModel = CreatePopulatedViewModel();
        viewModel.Number = "7";

        viewModel.Number = string.Empty;

        string[] expected = ["LAB REPORT"];
        Assert.Equal(expected, DocumentTitleLines(viewModel));
    }

    [Theory]
    [InlineData(DocumentType.Assignment, "1", "ASSIGNMENT 01")]
    [InlineData(DocumentType.LabReport, "3", "LAB REPORT 03")]
    [InlineData(DocumentType.ProjectReport, "2", "PROJECT REPORT 02")]
    public void Preview_AppendsTheNumberToThePresetTitle(DocumentType type, string number, string expected)
    {
        var viewModel = new MainViewModel();
        SelectDocumentType(viewModel, type);
        viewModel.Number = number;

        Assert.Equal(expected, Assert.Single(DocumentTitleLines(viewModel)));
        Assert.False(viewModel.IsCustomTitleEditable);
    }

    [Fact]
    public void Preview_AppendsTheNumberToTheCustomTitle()
    {
        var viewModel = new MainViewModel();
        SelectDocumentType(viewModel, DocumentType.Custom);
        viewModel.DocumentTitle = "Smart Campus Navigation";
        viewModel.Number = "1";

        Assert.Equal("Smart Campus Navigation 01", Assert.Single(DocumentTitleLines(viewModel)));
        Assert.Equal("Smart Campus Navigation", viewModel.BuildCoverPage().DocumentTitle);
    }

    [Fact]
    public void PresetTitle_IgnoresEdits_WhenNumberAndTitleTopicAreSet()
    {
        var viewModel = new MainViewModel();
        SelectDocumentType(viewModel, DocumentType.Assignment);
        viewModel.Number = "1";
        viewModel.TitleTopic = "Distributed Systems";

        viewModel.DocumentTitle = "Something Else";

        Assert.Equal("ASSIGNMENT", viewModel.DocumentTitle);
        string[] expected = ["ASSIGNMENT 01", "Title: Distributed Systems"];
        Assert.Equal(expected, DocumentTitleLines(viewModel));
        Assert.False(viewModel.IsCustomTitleEditable);
    }

    [Fact]
    public void Preview_KeepsTheNumberAndTitleTopicOnSeparateLines()
    {
        var viewModel = CreatePopulatedViewModel();
        viewModel.Number = "4";
        viewModel.TitleTopic = "Distributed Systems";

        string[] expected = ["LAB REPORT 04", "Title: Distributed Systems"];

        Assert.Equal(expected, DocumentTitleLines(viewModel));
    }

    private static string[] DocumentTitleLines(MainViewModel viewModel) =>
        viewModel.Preview.Sections
            .Single(section => section.Section == CoverSection.DocumentTitle)
            .Lines
            .Select(line => string.Concat(line.Runs.Select(run => run.Text)))
            .ToArray();
}
