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

    [Theory]
    [InlineData("ASSIGNMENT", "1", "ASSIGNMENT 01")]
    [InlineData("LAB REPORT", "3", "LAB REPORT 03")]
    [InlineData("PROJECT REPORT", "2", "PROJECT REPORT 02")]
    [InlineData("CUSTOM", "1", "CUSTOM 01")]
    [InlineData("Smart Campus Navigation", "7", "Smart Campus Navigation 07")]
    [InlineData("PROJECT REPORT", "12", "PROJECT REPORT 12")]
    [InlineData("PROJECT REPORT", "123", "PROJECT REPORT 123")]
    public void Build_DocumentTitle_AppendsTheFormattedNumber(string documentTitle, string number, string expected)
    {
        var coverPage = CreateCoverPage();
        coverPage.DocumentTitle = documentTitle;
        coverPage.Number = number;

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.DocumentTitle);

        Assert.Equal(expected, Flatten(section).Single());
        Assert.Single(section.Lines);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_DocumentTitle_KeepsTheTitleOnly_WhenNumberIsEmpty(string number)
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = number;

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.DocumentTitle);

        Assert.Equal("PROJECT REPORT", Flatten(section).Single());
        Assert.Single(section.Lines);
    }

    [Fact]
    public void Build_DocumentTitle_TreatsANullNumberAsEmpty()
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = null!;

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.DocumentTitle);

        Assert.Equal("PROJECT REPORT", Flatten(section).Single());
    }

    [Fact]
    public void Build_DocumentTitle_UsesTheResolvedTitle_WithTheNumber()
    {
        var coverPage = CreateCoverPage();
        coverPage.DocumentTitle = CoverPageTemplate.ResolveDocumentTitle(DocumentType.Assignment, null);
        coverPage.Number = "1";

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.DocumentTitle);

        Assert.Equal("ASSIGNMENT 01", Flatten(section).Single());
    }

    [Fact]
    public void Build_DocumentTitle_CombinesACustomTitleWithTheNumber()
    {
        var coverPage = CreateCoverPage();
        coverPage.DocumentTitle = CoverPageTemplate.ResolveDocumentTitle(DocumentType.Custom, "Smart Campus Navigation");
        coverPage.Number = "1";

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.DocumentTitle);

        Assert.Equal("Smart Campus Navigation 01", Flatten(section).Single());
    }

    [Fact]
    public void Build_TitleTopic_RendersALineImmediatelyAfterTheDocumentTitle()
    {
        var coverPage = CreateCoverPage();
        coverPage.TitleTopic = "Smart Campus Navigation";

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.DocumentTitle);
        string[] expected = ["PROJECT REPORT", "Title: Smart Campus Navigation"];

        Assert.Equal(expected, Flatten(section));
        Assert.Equal(2, section.Lines[1].Count);
        Assert.Equal("Title: ", section.Lines[1][0].Text);
        Assert.True(section.Lines[1][0].IsBold);
        Assert.Equal("Smart Campus Navigation", section.Lines[1][1].Text);
        Assert.False(section.Lines[1][1].IsBold);
        Assert.Equal(14d, section.Lines[1][1].FontSizePt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_TitleTopic_OmitsTheLine_WhenEmpty(string titleTopic)
    {
        var coverPage = CreateCoverPage();
        coverPage.TitleTopic = titleTopic;

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.DocumentTitle);

        Assert.Single(section.Lines);
        Assert.Equal("PROJECT REPORT", Flatten(section).Single());
    }

    [Fact]
    public void Build_TitleTopic_KeepsTheDocumentTitleSeparateFromTheNumber()
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = "5";
        coverPage.TitleTopic = "Distributed Systems";

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.DocumentTitle);
        string[] expected = ["PROJECT REPORT 05", "Title: Distributed Systems"];

        Assert.Equal(expected, Flatten(section));
    }

    [Theory]
    [InlineData("", "", "PROJECT REPORT", null)]
    [InlineData("2", "", "PROJECT REPORT 02", null)]
    [InlineData("", "Smart Campus Navigation", "PROJECT REPORT", "Smart Campus Navigation")]
    [InlineData("2", "Smart Campus Navigation", "PROJECT REPORT 02", "Smart Campus Navigation")]
    public void Build_NumberAndTitleTopic_ProduceTheExpectedDocumentTitleLines(
        string number,
        string titleTopic,
        string expectedTitleLine,
        string? expectedTopic)
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = number;
        coverPage.TitleTopic = titleTopic;

        var section = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference)
            .Single(content => content.Section == CoverSection.DocumentTitle);
        var lines = Flatten(section);

        Assert.Equal(expectedTopic is null ? 1 : 2, lines.Count);
        Assert.Equal(expectedTitleLine, lines[0]);

        if (expectedTopic is not null)
        {
            Assert.Equal($"Title: {expectedTopic}", lines[1]);
        }
    }

    [Fact]
    public void Build_KeepsExistingOutput_WhenBothOptionalFieldsAreEmpty()
    {
        var coverPage = CreateCoverPage();
        var withOptionals = CreateCoverPage();
        withOptionals.Number = string.Empty;
        withOptionals.TitleTopic = string.Empty;

        var sections = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference);
        var unchanged = CoverContentBuilder.Build(withOptionals, CoverPageTemplate.Reference);

        Assert.Equal(ExpectedSectionOrder, sections.Select(section => section.Section));
        Assert.Equal(
            sections.Select(section => string.Join("|", Flatten(section))),
            unchanged.Select(section => string.Join("|", Flatten(section))));

        var title = sections.Single(content => content.Section == CoverSection.DocumentTitle);
        Assert.Single(title.Lines);
        Assert.Equal("PROJECT REPORT", Flatten(title).Single());
        Assert.DoesNotContain(Flatten(title), line => line.StartsWith("Title:", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_LeavesTheOtherSectionsUntouched_ByTheOptionalFields()
    {
        var baseline = CreateCoverPage();
        var coverPage = CreateCoverPage();
        coverPage.Number = "9";
        coverPage.TitleTopic = "Distributed Systems";

        var sections = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference);
        var original = CoverContentBuilder.Build(baseline, CoverPageTemplate.Reference);

        foreach (CoverSection section in Enum.GetValues<CoverSection>())
        {
            if (section == CoverSection.DocumentTitle)
            {
                continue;
            }

            Assert.Equal(
                string.Join("|", Flatten(original.Single(content => content.Section == section))),
                string.Join("|", Flatten(sections.Single(content => content.Section == section))));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatDocumentTitle_UsesTheTitleOnly_WhenTheNumberIsMissing(string number)
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = number;

        Assert.Equal("PROJECT REPORT", CoverContentBuilder.FormatDocumentTitle(coverPage));
    }

    [Fact]
    public void FormatDocumentTitle_PadsTheNumberToAtLeastTwoDigits()
    {
        var coverPage = CreateCoverPage();

        coverPage.Number = "1";
        Assert.Equal("PROJECT REPORT 01", CoverContentBuilder.FormatDocumentTitle(coverPage));

        coverPage.Number = "9";
        Assert.Equal("PROJECT REPORT 09", CoverContentBuilder.FormatDocumentTitle(coverPage));

        coverPage.Number = "12";
        Assert.Equal("PROJECT REPORT 12", CoverContentBuilder.FormatDocumentTitle(coverPage));
    }

    [Fact]
    public void FormatDocumentTitle_Throws_WhenCoverPageIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => CoverContentBuilder.FormatDocumentTitle(null!));
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
