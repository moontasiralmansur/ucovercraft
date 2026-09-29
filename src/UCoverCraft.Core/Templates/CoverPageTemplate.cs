using UCoverCraft.Core.Models;

namespace UCoverCraft.Core.Templates;

public sealed class CoverPageTemplate
{
    public const double A4WidthMm = 210;
    public const double A4HeightMm = 297;

    public const string LabReportTitle = "LAB REPORT";
    public const string ProjectReportTitle = "PROJECT REPORT";
    public const string AssignmentTitle = "ASSIGNMENT";

    private const double ReferenceMarginMm = 25.4;
    private const double ReferenceParagraphSpacingMm = 1.693;
    private const double ReferenceLogoFontPt = 11;
    private const double ReferenceContentFontPt = 14;

    public static readonly CoverPageTemplate Reference = new()
    {
        MarginTopMm = ReferenceMarginMm,
        MarginRightMm = ReferenceMarginMm,
        MarginBottomMm = ReferenceMarginMm,
        MarginLeftMm = ReferenceMarginMm,
        LogoWidthMm = 111.919,
        LogoHeightMm = 42.856,
        LogoTopOffsetMm = ReferenceParagraphSpacingMm,
        FontFamily = "Times New Roman",
        Alignment = TemplateAlignment.Center,
        Styles = CreateReferenceStyles(),
        SpacingAfterMm = CreateSectionMap<double?>(_ => ReferenceParagraphSpacingMm),
    };

    public double? MarginTopMm { get; init; }

    public double? MarginRightMm { get; init; }

    public double? MarginBottomMm { get; init; }

    public double? MarginLeftMm { get; init; }

    public double? LogoWidthMm { get; init; }

    public double? LogoHeightMm { get; init; }

    public double? LogoTopOffsetMm { get; init; }

    public string? FontFamily { get; init; }

    public TemplateAlignment Alignment { get; init; } = TemplateAlignment.Center;

    public IReadOnlyList<CoverSection> SectionOrder { get; init; } =
    [
        CoverSection.Logo,
        CoverSection.DocumentTitle,
        CoverSection.CourseInformation,
        CoverSection.SubmittedTo,
        CoverSection.SubmittedBy,
        CoverSection.SubmissionDate,
    ];

    public IReadOnlyDictionary<CoverSection, SectionStyle> Styles { get; init; } =
        CreateSectionMap(_ => new SectionStyle());

    public IReadOnlyDictionary<CoverSection, double?> SpacingAfterMm { get; init; } =
        CreateSectionMap<double?>(_ => null);

    public static string ResolveDocumentTitle(DocumentType documentType, string? customTitle)
    {
        return documentType switch
        {
            DocumentType.LabReport => LabReportTitle,
            DocumentType.ProjectReport => ProjectReportTitle,
            DocumentType.Assignment => AssignmentTitle,
            DocumentType.Custom => RequireCustomTitle(customTitle),
            _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown document type."),
        };
    }

    public static IReadOnlyList<string> FormatSubmittedByLines(IReadOnlyList<Student> students)
    {
        ArgumentNullException.ThrowIfNull(students);

        var lines = new List<string>(students.Count);
        foreach (var student in students)
        {
            lines.Add(student.Name);
        }

        return lines;
    }

    private static string RequireCustomTitle(string? customTitle)
    {
        var title = customTitle?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new ArgumentException("A custom document title is required.", nameof(customTitle));
        }

        return title;
    }

    private static IReadOnlyDictionary<CoverSection, SectionStyle> CreateReferenceStyles()
    {
        return new Dictionary<CoverSection, SectionStyle>
        {
            [CoverSection.Logo] = new SectionStyle
            {
                FontSizePt = ReferenceLogoFontPt,
                IsBold = false,
            },
            [CoverSection.DocumentTitle] = new SectionStyle(),
            [CoverSection.CourseInformation] = new SectionStyle
            {
                FontSizePt = ReferenceContentFontPt,
                IsBold = true,
            },
            [CoverSection.SubmittedTo] = new SectionStyle
            {
                FontSizePt = ReferenceContentFontPt,
                IsBold = true,
            },
            [CoverSection.SubmittedBy] = new SectionStyle
            {
                FontSizePt = ReferenceContentFontPt,
                IsBold = true,
            },
            [CoverSection.SubmissionDate] = new SectionStyle
            {
                FontSizePt = ReferenceContentFontPt,
                IsBold = true,
            },
        };
    }

    private static IReadOnlyDictionary<CoverSection, TValue> CreateSectionMap<TValue>(
        Func<CoverSection, TValue> createValue)
    {
        var map = new Dictionary<CoverSection, TValue>();
        foreach (CoverSection section in Enum.GetValues<CoverSection>())
        {
            map[section] = createValue(section);
        }

        return map;
    }
}
