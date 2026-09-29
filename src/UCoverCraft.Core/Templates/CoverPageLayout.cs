using UCoverCraft.Core.Models;

namespace UCoverCraft.Core.Templates;

public sealed record CoverPageLayout(
    double TextHeightMm,
    double GapMm,
    IReadOnlyDictionary<CoverSection, double> SectionHeightMm,
    IReadOnlyDictionary<CoverSection, double> SpacingAfterMm)
{
    public const double LineSpacingFactor = 1.15;

    private const double MmPerPoint = 25.4 / 72;

    private const double FitToleranceMm = 1e-6;

    private const int MaxStudentsSearchLimit = 1000;

    public static int MaxStudents { get; } = CalculateMaxStudents(CoverPageTemplate.Reference);

    public double OccupiedHeightMm => SectionHeightMm.Values.Sum();

    public double ContentHeightMm => OccupiedHeightMm + SpacingAfterMm.Values.Sum();

    public bool FitsInTextArea => ContentHeightMm <= TextHeightMm + FitToleranceMm;

    public void EnsureFitsInTextArea()
    {
        if (!FitsInTextArea)
        {
            throw new InvalidOperationException(
                $"The cover page content is {ContentHeightMm:F2} mm tall but only " +
                $"{TextHeightMm:F2} mm fit on one A4 page.");
        }
    }

    public static double LineHeightPt(double fontSizePt) => fontSizePt * LineSpacingFactor;

    public static double LineHeightMm(double fontSizePt) => LineHeightPt(fontSizePt) * MmPerPoint;

    public static CoverPageLayout Calculate(
        IReadOnlyList<CoverSectionContent> sections,
        CoverPageTemplate template)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(template);

        var sectionHeights = new Dictionary<CoverSection, double>(sections.Count);
        foreach (var section in sections)
        {
            sectionHeights[section.Section] = MeasureSectionMm(section, template);
        }

        var textHeightMm = CoverPageTemplate.A4HeightMm
            - (template.MarginTopMm ?? 0)
            - (template.MarginBottomMm ?? 0);
        var gapCount = Math.Max(sections.Count - 1, 0);
        var gapMm = gapCount == 0
            ? 0
            : Math.Max(
                MinimumGapMm(sections, template, gapCount),
                (textHeightMm - sectionHeights.Values.Sum()) / gapCount);

        var spacing = new Dictionary<CoverSection, double>(sections.Count);
        for (var i = 0; i < sections.Count; i++)
        {
            spacing[sections[i].Section] = i < gapCount ? gapMm : 0;
        }

        return new CoverPageLayout(textHeightMm, gapMm, sectionHeights, spacing);
    }

    private static double MeasureSectionMm(CoverSectionContent section, CoverPageTemplate template)
    {
        if (section.Section == CoverSection.Logo)
        {
            return (template.LogoTopOffsetMm ?? 0) + (template.LogoHeightMm ?? 0);
        }

        var heightMm = 0d;
        foreach (var line in section.Lines)
        {
            if (line.Count > 0)
            {
                heightMm += LineHeightMm(line.Max(run => run.FontSizePt));
            }
        }

        return heightMm;
    }

    private static double MinimumGapMm(
        IReadOnlyList<CoverSectionContent> sections,
        CoverPageTemplate template,
        int gapCount)
    {
        var minimumMm = 0d;
        for (var i = 0; i < gapCount; i++)
        {
            if (template.SpacingAfterMm.TryGetValue(sections[i].Section, out var spacing) &&
                spacing is { } configured &&
                configured > minimumMm)
            {
                minimumMm = configured;
            }
        }

        return minimumMm;
    }

    private static int CalculateMaxStudents(CoverPageTemplate template)
    {
        var count = 0;
        while (count < MaxStudentsSearchLimit && Fits(WorstCaseCoverPage(count), template))
        {
            count++;
        }

        return Math.Max(count - 1, 0);
    }

    private static bool Fits(CoverPage coverPage, CoverPageTemplate template)
    {
        var sections = CoverContentBuilder.Build(coverPage, template);
        return Calculate(sections, template).FitsInTextArea;
    }

    private static CoverPage WorstCaseCoverPage(int studentCount)
    {
        var students = new List<Student>(studentCount);
        for (var i = 0; i < studentCount; i++)
        {
            students.Add(new Student { Name = "Student", StudentId = "00000000" });
        }

        return new CoverPage
        {
            DocumentTitle = CoverPageTemplate.LabReportTitle,
            CourseTitle = "Course",
            CourseCode = "Course Code",
            Section = "1",
            SubmittedTo = new Instructor
            {
                Name = "Instructor",
                Designation = "Designation",
                Department = "Department",
            },
            Students = students,
            SubmissionDate = new DateOnly(2000, 1, 1),
        };
    }
}
