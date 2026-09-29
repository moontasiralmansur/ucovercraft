using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Tests.Templates;

public class CoverContentBuilderTests
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
    public void Build_ReturnsEveryTemplateSection_InOrder()
    {
        var sections = CoverContentBuilder.Build(CreateCoverPage(), CoverPageTemplate.Reference);

        Assert.Equal(ExpectedSectionOrder, sections.Select(section => section.Section));
    }

    [Fact]
    public void Build_LogoSection_HasNoTextLines()
    {
        var logo = CoverContentBuilder.Build(CreateCoverPage(), CoverPageTemplate.Reference)[0];

        Assert.Equal(CoverSection.Logo, logo.Section);
        Assert.Empty(logo.Lines);
        Assert.Equal(CalculateLayout().GapMm, logo.SpacingAfterMm);
    }

    [Fact]
    public void Build_AppliesTheCalculatedLayoutSpacing_ToEverySection()
    {
        var sections = CoverContentBuilder.Build(CreateCoverPage(), CoverPageTemplate.Reference);
        var layout = CalculateLayout();
        var lastSection = CoverPageTemplate.Reference.SectionOrder[^1];

        foreach (var section in sections)
        {
            var expected = section.Section == lastSection ? 0d : layout.GapMm;
            Assert.Equal(expected, section.SpacingAfterMm);
        }
    }

    [Fact]
    public void Build_CourseInformation_UsesBoldLabelsAndRegularValues()
    {
        var section = Section(CoverSection.CourseInformation);
        string[] expected = ["Course Title: Software Engineering", "Course Code: CSE-4402", "Section: 7"];

        Assert.Equal(expected, Flatten(section));
        Assert.True(section.Lines[0][0].IsBold);
        Assert.False(section.Lines[0][1].IsBold);
        Assert.Equal(14d, section.Lines[0][1].FontSizePt);
    }

    [Fact]
    public void Build_KeepsTheSectionLine_WhenSectionIsMissing()
    {
        var coverPage = CreateCoverPage();
        coverPage.Section = null;

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.CourseInformation);
        string[] expected = ["Course Title: Software Engineering", "Course Code: CSE-4402", "Section: "];

        Assert.Equal(expected, Flatten(section));
        Assert.Equal(2, section.Lines[2].Count);
        Assert.Equal(string.Empty, section.Lines[2][1].Text);
    }

    [Fact]
    public void Build_SubmittedTo_ShowsHeadingNameAndDepartment()
    {
        var section = Section(CoverSection.SubmittedTo);
        string[] expected =
        [
            "Submitted to:",
            "Wahida Ferdose Urmi",
            "Department of Computer Science and Engineering",
        ];

        Assert.Equal(expected, Flatten(section));
        Assert.True(section.Lines[0][0].IsBold);
        Assert.False(section.Lines[1][0].IsBold);
    }

    [Fact]
    public void Build_SubmittedTo_ListsDesignation_BetweenNameAndDepartment()
    {
        var coverPage = CreateCoverPage();
        coverPage.SubmittedTo!.Designation = "Lecturer";

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.SubmittedTo);
        string[] expected =
        [
            "Submitted to:",
            "Wahida Ferdose Urmi",
            "Lecturer",
            "Department of Computer Science and Engineering",
        ];

        Assert.Equal(expected, Flatten(section));
    }

    [Fact]
    public void Build_SubmittedBy_ListsEveryStudent_WithStudentId()
    {
        var section = Section(CoverSection.SubmittedBy);
        string[] expected = ["Submitted by:", "Jane Doe (S-1001)", "John Smith (S-1002)"];

        Assert.Equal(expected, Flatten(section));
    }

    [Fact]
    public void Build_SubmittedBy_OmitsStudentId_WhenMissing()
    {
        var coverPage = CreateCoverPage();
        coverPage.Students = [new Student { Name = "Solo Student" }];

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.SubmittedBy);
        string[] expected = ["Submitted by:", "Solo Student"];

        Assert.Equal(expected, Flatten(section));
    }

    [Fact]
    public void Build_SubmissionDate_ShowsFormattedDate()
    {
        var section = Section(CoverSection.SubmissionDate);
        string[] expected = ["Date of Submission: 13 June, 2026"];

        Assert.Equal(expected, Flatten(section));
    }

    [Fact]
    public void Build_PreservesCoverPageValues()
    {
        var coverPage = CreateCoverPage();
        var students = coverPage.Students.Select(student => (student.Name, student.StudentId)).ToList();

        CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference);

        Assert.Equal("PROJECT REPORT", coverPage.DocumentTitle);
        Assert.Equal("Software Engineering", coverPage.CourseTitle);
        Assert.Equal("CSE-4402", coverPage.CourseCode);
        Assert.Equal("7", coverPage.Section);
        Assert.Equal("Wahida Ferdose Urmi", coverPage.SubmittedTo?.Name);
        Assert.Equal(students, coverPage.Students.Select(student => (student.Name, student.StudentId)));
        Assert.Equal(new DateOnly(2026, 6, 13), coverPage.SubmissionDate);
    }

    [Fact]
    public void Build_Throws_WhenCoverPageIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => CoverContentBuilder.Build(null!, CoverPageTemplate.Reference));
    }

    [Fact]
    public void Build_Throws_WhenTemplateIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => CoverContentBuilder.Build(CreateCoverPage(), null!));
    }

    [Theory]
    [InlineData(2026, 6, 13, "13 June, 2026")]
    [InlineData(2026, 1, 1, "1 January, 2026")]
    [InlineData(2025, 12, 31, "31 December, 2025")]
    public void FormatSubmissionDate_FormatsDayMonthYear(int year, int month, int day, string expected)
    {
        var formatted = CoverContentBuilder.FormatSubmissionDate(new DateOnly(year, month, day));

        Assert.Equal(expected, formatted);
    }

    private static CoverSectionContent Section(CoverSection section) =>
        CoverContentBuilder.Build(CreateCoverPage(), CoverPageTemplate.Reference)
            .Single(content => content.Section == section);

    private static IReadOnlyList<string> Flatten(CoverSectionContent section) =>
        section.Lines.Select(line => string.Concat(line.Select(run => run.Text))).ToList();

    private static CoverPageLayout CalculateLayout() =>
        CoverPageLayout.Calculate(
            CoverContentBuilder.Build(CreateCoverPage(), CoverPageTemplate.Reference),
            CoverPageTemplate.Reference);

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
