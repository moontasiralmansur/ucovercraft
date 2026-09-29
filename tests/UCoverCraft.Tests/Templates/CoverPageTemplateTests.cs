using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Tests.Templates;

public class CoverPageTemplateTests
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

    private static readonly CoverSection[] ContentSections =
    [
        CoverSection.CourseInformation,
        CoverSection.SubmittedTo,
        CoverSection.SubmittedBy,
        CoverSection.SubmissionDate,
    ];

    [Fact]
    public void A4PageSize_MatchesIso216()
    {
        Assert.Equal(210d, CoverPageTemplate.A4WidthMm);
        Assert.Equal(297d, CoverPageTemplate.A4HeightMm);
    }

    [Fact]
    public void SectionOrder_MatchesReferenceLayout()
    {
        Assert.Equal(ExpectedSectionOrder, CoverPageTemplate.Reference.SectionOrder);
    }

    [Fact]
    public void SectionOrder_IncludesSubmittedByOnce()
    {
        var count = CoverPageTemplate.Reference.SectionOrder.Count(section => section == CoverSection.SubmittedBy);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Alignment_DefaultsToCenter()
    {
        Assert.Equal(TemplateAlignment.Center, CoverPageTemplate.Reference.Alignment);
    }

    [Fact]
    public void Reference_Margins_AreOneInchOnEverySide()
    {
        var template = CoverPageTemplate.Reference;

        Assert.Equal(25.4d, template.MarginTopMm);
        Assert.Equal(25.4d, template.MarginRightMm);
        Assert.Equal(25.4d, template.MarginBottomMm);
        Assert.Equal(25.4d, template.MarginLeftMm);
    }

    [Fact]
    public void Reference_LogoSize_MatchesDocumentedExtent()
    {
        var template = CoverPageTemplate.Reference;

        Assert.Equal(111.919d, template.LogoWidthMm);
        Assert.Equal(42.856d, template.LogoHeightMm);
    }

    [Fact]
    public void Reference_LogoTopOffset_MatchesLogoParagraphSpaceBefore()
    {
        Assert.Equal(1.693d, CoverPageTemplate.Reference.LogoTopOffsetMm);
    }

    [Fact]
    public void Reference_FontFamily_IsTimesNewRoman()
    {
        Assert.Equal("Times New Roman", CoverPageTemplate.Reference.FontFamily);
    }

    [Fact]
    public void Styles_CoverEverySection()
    {
        var template = CoverPageTemplate.Reference;
        var sections = Enum.GetValues<CoverSection>();

        Assert.Equal(sections.Length, template.Styles.Count);
        foreach (CoverSection section in sections)
        {
            Assert.Contains(section, template.Styles.Keys);
        }
    }

    [Fact]
    public void Reference_Styles_MatchDocumentedSizesAndWeights()
    {
        var styles = CoverPageTemplate.Reference.Styles;

        Assert.Equal(11d, styles[CoverSection.Logo].FontSizePt);
        Assert.False(styles[CoverSection.Logo].IsBold);

        foreach (CoverSection section in ContentSections)
        {
            Assert.Equal(14d, styles[section].FontSizePt);
            Assert.True(styles[section].IsBold);
        }
    }

    [Fact]
    public void Reference_Styles_AreUnspecified_ForDocumentTitle()
    {
        var style = CoverPageTemplate.Reference.Styles[CoverSection.DocumentTitle];

        Assert.False(style.FontSizePt.HasValue);
        Assert.False(style.IsBold.HasValue);
    }

    [Fact]
    public void Spacing_CoversEverySection()
    {
        var template = CoverPageTemplate.Reference;
        var sections = Enum.GetValues<CoverSection>();

        Assert.Equal(sections.Length, template.SpacingAfterMm.Count);
        foreach (CoverSection section in sections)
        {
            Assert.Contains(section, template.SpacingAfterMm.Keys);
        }
    }

    [Fact]
    public void Spacing_IsUnspecified_ForDefaultTemplate()
    {
        var template = new CoverPageTemplate();

        foreach (var spacing in template.SpacingAfterMm.Values)
        {
            Assert.False(spacing.HasValue);
        }
    }

    [Fact]
    public void Reference_SpacingAfter_MatchesDocumentedSpacing_ForEverySection()
    {
        foreach (CoverSection section in Enum.GetValues<CoverSection>())
        {
            Assert.Equal(1.693d, CoverPageTemplate.Reference.SpacingAfterMm[section]);
        }
    }

    [Fact]
    public void SectionStyle_DefaultsAreUnspecified()
    {
        var style = new SectionStyle();

        Assert.False(style.FontSizePt.HasValue);
        Assert.False(style.IsBold.HasValue);
    }

    [Theory]
    [InlineData(DocumentType.LabReport, "LAB REPORT")]
    [InlineData(DocumentType.ProjectReport, "PROJECT REPORT")]
    [InlineData(DocumentType.Assignment, "ASSIGNMENT")]
    public void ResolveDocumentTitle_ReturnsCanonicalTitle_ForKnownDocumentType(
        DocumentType documentType,
        string expected)
    {
        Assert.Equal(expected, CoverPageTemplate.ResolveDocumentTitle(documentType, null));
    }

    [Fact]
    public void ResolveDocumentTitle_UsesCustomTitle_ForCustomDocumentType()
    {
        var title = CoverPageTemplate.ResolveDocumentTitle(DocumentType.Custom, "  Smart Campus Navigation  ");

        Assert.Equal("Smart Campus Navigation", title);
    }

    [Fact]
    public void ResolveDocumentTitle_Throws_ForBlankCustomTitle()
    {
        Assert.Throws<ArgumentException>(
            () => CoverPageTemplate.ResolveDocumentTitle(DocumentType.Custom, "   "));
    }

    [Fact]
    public void ResolveDocumentTitle_Throws_ForUnknownDocumentType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CoverPageTemplate.ResolveDocumentTitle((DocumentType)42, null));
    }

    [Fact]
    public void FormatSubmittedByLines_ReturnsEmpty_ForNoStudents()
    {
        var lines = CoverPageTemplate.FormatSubmittedByLines(Array.Empty<Student>());

        Assert.Empty(lines);
    }

    [Fact]
    public void FormatSubmittedByLines_PreservesOrder_ForAnyStudentCount()
    {
        IReadOnlyList<Student> students =
        [
            new Student { Name = "Jane Doe" },
            new Student { Name = "John Smith" },
            new Student { Name = "Amina Noor" },
            new Student { Name = "Rakib Hasan" },
        ];

        string[] expected = ["Jane Doe", "John Smith", "Amina Noor", "Rakib Hasan"];

        var lines = CoverPageTemplate.FormatSubmittedByLines(students);

        Assert.Equal(expected.Length, lines.Count);
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void FormatSubmittedByLines_ReturnsOneLine_ForSingleStudent()
    {
        IReadOnlyList<Student> students = [new Student { Name = "Moontasir Al Mansur" }];

        string[] expected = ["Moontasir Al Mansur"];

        var lines = CoverPageTemplate.FormatSubmittedByLines(students);

        Assert.Equal(expected, lines);
    }

    [Fact]
    public void FormatSubmittedByLines_Throws_WhenStudentsIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => CoverPageTemplate.FormatSubmittedByLines(null!));
    }
}
