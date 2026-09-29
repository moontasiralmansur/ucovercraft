using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Tests.Templates;

public class CoverPageLayoutTests
{
    private const double MarginMm = 25.4d;
    private const double ReferenceGapMm = 1.693d;

    [Fact]
    public void Calculate_TextHeight_IsA4MinusTopAndBottomMargins()
    {
        var layout = Calculate(CreateCoverPage());

        Assert.Equal(297d - 2 * MarginMm, layout.TextHeightMm, 6);
        Assert.Equal(246.2d, layout.TextHeightMm, 6);
    }

    [Fact]
    public void Calculate_SectionHeights_AccountForEveryMajorSection()
    {
        var layout = Calculate(CreateCoverPage());
        var lineMm = CoverPageLayout.LineHeightMm(CoverContentBuilder.DefaultFontSizePt);

        Assert.Equal(Enum.GetValues<CoverSection>().Length, layout.SectionHeightMm.Count);
        Assert.Equal(1.693d + 42.856d, layout.SectionHeightMm[CoverSection.Logo], 6);
        Assert.Equal(lineMm, layout.SectionHeightMm[CoverSection.DocumentTitle], 6);
        Assert.Equal(3 * lineMm, layout.SectionHeightMm[CoverSection.CourseInformation], 6);
        Assert.Equal(3 * lineMm, layout.SectionHeightMm[CoverSection.SubmittedTo], 6);
        Assert.Equal(3 * lineMm, layout.SectionHeightMm[CoverSection.SubmittedBy], 6);
        Assert.Equal(lineMm, layout.SectionHeightMm[CoverSection.SubmissionDate], 6);
    }

    [Fact]
    public void Calculate_OccupiedHeight_IsTheSumOfSectionHeights()
    {
        var layout = Calculate(CreateCoverPage());

        Assert.Equal(layout.SectionHeightMm.Values.Sum(), layout.OccupiedHeightMm, 9);
    }

    [Fact]
    public void Calculate_EndsFinalDateAtTheBottomOfTheTextArea()
    {
        var layout = Calculate(CreateCoverPage());
        var gapCount = CoverPageTemplate.Reference.SectionOrder.Count - 1;

        Assert.Equal(
            layout.TextHeightMm,
            layout.OccupiedHeightMm + (layout.GapMm * gapCount),
            6);
        Assert.Equal(0d, layout.SpacingAfterMm[CoverSection.SubmissionDate]);
    }

    [Fact]
    public void Calculate_UsesOneConsistentGap_BetweenMajorSections()
    {
        var layout = Calculate(CreateCoverPage());
        var order = CoverPageTemplate.Reference.SectionOrder;
        var gapSections = order.Take(order.Count - 1);

        foreach (var section in gapSections)
        {
            Assert.Equal(layout.GapMm, layout.SpacingAfterMm[section]);
        }

        Assert.True(layout.GapMm > ReferenceGapMm, "The gap must stretch beyond the reference minimum.");
    }

    [Fact]
    public void Calculate_GrowsTheGap_WhenContentIsSparse()
    {
        var coverPage = CreateCoverPage();
        coverPage.Section = null;
        coverPage.Students = [new Student { Name = "Solo Student", StudentId = "S-0001" }];

        var dense = Calculate(CreateCoverPage());
        var sparse = Calculate(coverPage);

        Assert.True(sparse.GapMm > dense.GapMm);
        Assert.Equal(
            sparse.TextHeightMm,
            sparse.OccupiedHeightMm + (sparse.GapMm * 5),
            6);
    }

    [Fact]
    public void Calculate_ClampsToTheMinimumGap_WhenStudentCountIsLarge()
    {
        var coverPage = CreateCoverPage();
        coverPage.Students = Enumerable
            .Range(1, 40)
            .Select(index => new Student { Name = $"Student {index}", StudentId = $"S-{index}" })
            .ToList();

        var layout = Calculate(coverPage);

        Assert.Equal(ReferenceGapMm, layout.GapMm, 6);
        Assert.All(layout.SpacingAfterMm.Values, spacing => Assert.True(spacing >= 0));
        Assert.True(layout.OccupiedHeightMm > layout.TextHeightMm);
    }

    [Fact]
    public void Calculate_UsesNoGaps_ForASingleSectionTemplate()
    {
        var template = new CoverPageTemplate
        {
            MarginTopMm = MarginMm,
            MarginBottomMm = MarginMm,
            SectionOrder = [CoverSection.SubmissionDate],
        };
        var sections = CoverContentBuilder.Build(CreateCoverPage(), template);

        var layout = CoverPageLayout.Calculate(sections, template);

        Assert.Equal(0d, layout.GapMm);
        Assert.Equal(0d, layout.SpacingAfterMm[CoverSection.SubmissionDate]);
    }

    [Fact]
    public void LineHeight_MatchesTheLineSpacingFactor()
    {
        Assert.Equal(1.15d, CoverPageLayout.LineSpacingFactor);
        Assert.Equal(14d * 1.15d, CoverPageLayout.LineHeightPt(14d), 9);
        Assert.Equal(16.1d * 25.4d / 72d, CoverPageLayout.LineHeightMm(14d), 9);
    }

    [Fact]
    public void MaxStudents_IsTwentyFour()
    {
        Assert.Equal(24, CoverPageLayout.MaxStudents);
        Assert.True(CoverPage.MaxStudents <= CoverPageLayout.MaxStudents);
    }

    [Fact]
    public void Calculate_FitsTwentyFourStudents_InsideTheTextArea()
    {
        var layout = Calculate(WorstCaseCoverPage(24));

        Assert.True(layout.FitsInTextArea);
        Assert.Equal(layout.TextHeightMm, layout.ContentHeightMm, 6);
        Assert.Equal(246.2d, layout.ContentHeightMm, 6);
        Assert.True(layout.GapMm > ReferenceGapMm, "The maximum count must not exhaust the section gap.");
        Assert.Equal(0d, layout.SpacingAfterMm[CoverSection.SubmissionDate]);
    }

    [Fact]
    public void Calculate_OverflowsTheTextArea_AtTwentyFiveStudents()
    {
        var layout = Calculate(WorstCaseCoverPage(25));

        Assert.False(layout.FitsInTextArea);
        Assert.Equal(ReferenceGapMm, layout.GapMm, 6);
        Assert.True(layout.ContentHeightMm > layout.TextHeightMm);
        Assert.True(layout.ContentHeightMm - layout.TextHeightMm > 5d, "The date would leave the page.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Calculate_KeepsNormalStudentCounts_InsideTheTextArea(int studentCount)
    {
        var layout = Calculate(WorstCaseCoverPage(studentCount));

        Assert.True(layout.FitsInTextArea);
        Assert.Equal(layout.TextHeightMm, layout.ContentHeightMm, 6);
        Assert.True(layout.GapMm > ReferenceGapMm);
        Assert.Equal(0d, layout.SpacingAfterMm[CoverSection.SubmissionDate]);
    }

    [Fact]
    public void Calculate_Throws_WhenSectionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => CoverPageLayout.Calculate(null!, CoverPageTemplate.Reference));
    }

    [Fact]
    public void Calculate_Throws_WhenTemplateIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => CoverPageLayout.Calculate([], null!));
    }

    private static CoverPageLayout Calculate(CoverPage coverPage)
    {
        var sections = CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference);
        return CoverPageLayout.Calculate(sections, CoverPageTemplate.Reference);
    }

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

    private static CoverPage WorstCaseCoverPage(int studentCount) => new()
    {
        DocumentTitle = "PROJECT REPORT",
        CourseTitle = "Software Engineering",
        CourseCode = "CSE-4402",
        Section = "7",
        SubmittedTo = new Instructor
        {
            Name = "Wahida Ferdose Urmi",
            Designation = "Lecturer",
            Department = "Department of Computer Science and Engineering",
        },
        Students = Enumerable
            .Range(1, studentCount)
            .Select(index => new Student { Name = $"Student {index}", StudentId = $"S-{index:0000}" })
            .ToList(),
        SubmissionDate = new DateOnly(2026, 6, 13),
    };
}
