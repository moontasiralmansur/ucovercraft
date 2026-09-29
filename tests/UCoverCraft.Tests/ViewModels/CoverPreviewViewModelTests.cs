using UCoverCraft.App.ViewModels;
using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Tests.ViewModels;

public class CoverPreviewViewModelTests
{
    private static readonly CoverSection[] ExpectedSectionOrder =
    [
        CoverSection.Logo,
        CoverSection.DocumentTitle,
        CoverSection.CourseInformation,
        CoverSection.SubmittedTo,
        CoverSection.SubmittedBy,
        CoverSection.SubmissionDate,
    ];

    [Fact]
    public void Sections_AreEmpty_BeforeFirstUpdate()
    {
        var preview = new CoverPreviewViewModel();

        Assert.Empty(preview.Sections);
    }

    [Fact]
    public void Geometry_FollowsA4Page_AndOneInchMargins()
    {
        var preview = new CoverPreviewViewModel();

        Assert.Equal(210d * 96 / 25.4, preview.PageWidth, 3);
        Assert.Equal(297d * 96 / 25.4, preview.PageHeight, 3);
        Assert.Equal(96d, preview.PageMargin.Left, 3);
        Assert.Equal(96d, preview.PageMargin.Top, 3);
        Assert.Equal(96d, preview.PageMargin.Right, 3);
        Assert.Equal(96d, preview.PageMargin.Bottom, 3);
        Assert.Equal("Times New Roman", preview.FontFamily.Source);
    }

    [Fact]
    public void Geometry_FollowsProvidedTemplate()
    {
        var preview = new CoverPreviewViewModel(new CoverPageTemplate { FontFamily = "Arial" });

        Assert.Equal(0d, preview.PageMargin.Left, 3);
        Assert.Equal("Arial", preview.FontFamily.Source);
    }

    [Fact]
    public void Update_KeepsTemplateSectionOrder()
    {
        var preview = new CoverPreviewViewModel();

        preview.Update(CreateCoverPage());

        Assert.Equal(ExpectedSectionOrder, preview.Sections.Select(section => section.Section));
    }

    [Fact]
    public void Update_CarriesReferenceLogoGeometry()
    {
        var preview = new CoverPreviewViewModel();

        preview.Update(CreateCoverPage());

        var logo = preview.Sections[0];
        Assert.True(logo.IsLogo);
        Assert.Empty(logo.Lines);
        Assert.Equal(111.919d * 96 / 25.4, logo.LogoWidth, 3);
        Assert.Equal(42.856d * 96 / 25.4, logo.LogoHeight, 3);
        Assert.Equal(1.693d * 96 / 25.4, logo.LogoMargin.Top, 3);
        Assert.Equal(0d, preview.Sections[1].LogoMargin.Top, 3);
    }

    [Fact]
    public void Update_AppliesTheCalculatedLayoutSpacingAfterEverySection()
    {
        var preview = new CoverPreviewViewModel();

        preview.Update(CreateCoverPage());

        var layout = CoverPageLayout.Calculate(
            CoverContentBuilder.Build(CreateCoverPage(), CoverPageTemplate.Reference),
            CoverPageTemplate.Reference);
        var gap = layout.GapMm * 96 / 25.4;

        for (var i = 0; i < preview.Sections.Count - 1; i++)
        {
            Assert.Equal(gap, preview.Sections[i].Margin.Bottom, 3);
        }

        Assert.Equal(0d, preview.Sections[^1].Margin.Bottom, 3);
    }

    [Fact]
    public void Update_RendersEveryCoverPageField()
    {
        var preview = new CoverPreviewViewModel();

        preview.Update(CreateCoverPage());
        var rendered = RenderedText(preview);

        Assert.Contains("PROJECT REPORT", rendered);
        Assert.Contains("Course Title: ", rendered);
        Assert.Contains("Software Engineering", rendered);
        Assert.Contains("Course Code: ", rendered);
        Assert.Contains("CSE-4402", rendered);
        Assert.Contains("Section: ", rendered);
        Assert.Contains("Submitted to:", rendered);
        Assert.Contains("Wahida Ferdose Urmi", rendered);
        Assert.Contains("Department of Computer Science and Engineering", rendered);
        Assert.Contains("Submitted by:", rendered);
        Assert.Contains("Jane Doe (S-1001)", rendered);
        Assert.Contains("John Smith (S-1002)", rendered);
        Assert.Contains("Date of Submission: ", rendered);
        Assert.Contains("13 June, 2026", rendered);
    }

    [Fact]
    public void Update_GroupsRunsIntoOneLinePerCourseRow()
    {
        var preview = new CoverPreviewViewModel();

        preview.Update(CreateCoverPage());
        var course = preview.Sections.Single(section => section.Section == CoverSection.CourseInformation);

        Assert.Equal(3, course.Lines.Count);
        Assert.All(course.Lines, line => Assert.Equal(2, line.Runs.Count));
    }

    [Fact]
    public void Update_UsesReferenceTypography_ForRuns()
    {
        var preview = new CoverPreviewViewModel();

        preview.Update(CreateCoverPage());
        var runs = Runs(preview);
        var title = runs.Single(run => run.Text == "PROJECT REPORT");
        var value = runs.Single(run => run.Text == "Software Engineering");

        Assert.True(title.IsBold);
        Assert.Equal(14d * 96 / 72, title.FontSize, 3);
        Assert.Equal(14d * 1.15 * 96 / 72, title.LineHeight, 3);
        Assert.False(value.IsBold);
        Assert.Equal(title.LineHeight, value.LineHeight, 3);
    }

    [Fact]
    public void Update_RaisesPropertyChanged_ForSections()
    {
        var preview = new CoverPreviewViewModel();
        var raised = new List<string?>();
        preview.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        preview.Update(CreateCoverPage());

        Assert.Contains(nameof(CoverPreviewViewModel.Sections), raised);
    }

    [Fact]
    public void Update_PreservesCoverPageValues()
    {
        var coverPage = CreateCoverPage();
        var preview = new CoverPreviewViewModel();

        preview.Update(coverPage);

        Assert.Equal("PROJECT REPORT", coverPage.DocumentTitle);
        Assert.Equal("Software Engineering", coverPage.CourseTitle);
        Assert.Equal("CSE-4402", coverPage.CourseCode);
        Assert.Equal("7", coverPage.Section);
        Assert.Equal("Wahida Ferdose Urmi", coverPage.SubmittedTo?.Name);
        Assert.Equal(2, coverPage.Students.Count);
        Assert.Equal(new DateOnly(2026, 6, 13), coverPage.SubmissionDate);
    }

    [Fact]
    public void Update_Throws_WhenCoverPageIsNull()
    {
        var preview = new CoverPreviewViewModel();

        Assert.Throws<ArgumentNullException>(() => preview.Update(null!));
    }

    private static string RenderedText(CoverPreviewViewModel preview) =>
        string.Join("|", Runs(preview).Select(run => run.Text));

    private static IReadOnlyList<CoverPreviewRun> Runs(CoverPreviewViewModel preview) =>
        preview.Sections.SelectMany(section => section.Lines).SelectMany(line => line.Runs).ToList();

    private static CoverPage CreateCoverPage() => new()
    {
        DocumentTitle = "PROJECT REPORT",
        CourseTitle = "Software Engineering",
        CourseCode = "CSE-4402",
        Section = "7",
        SubmittedTo = new Instructor
        {
            Name = "Wahida Ferdose Urmi",
            Department = "Department of Computer Science and Engineering",
        },
        Students =
        [
            new Student { Name = "Jane Doe", StudentId = "S-1001" },
            new Student { Name = "John Smith", StudentId = "S-1002" },
        ],
        SubmissionDate = new DateOnly(2026, 6, 13),
    };
}
