using System.Globalization;
using UCoverCraft.Core.Models;

namespace UCoverCraft.Core.Templates;

public static class CoverContentBuilder
{
    public const string DefaultFontFamily = "Times New Roman";

    public const double DefaultFontSizePt = 14;

    public static string FormatSubmissionDate(DateOnly submissionDate)
    {
        return submissionDate.ToString("d MMMM, yyyy", CultureInfo.InvariantCulture);
    }

    public static string FormatDocumentTitle(CoverPage coverPage)
    {
        ArgumentNullException.ThrowIfNull(coverPage);

        var title = coverPage.DocumentTitle ?? string.Empty;
        var number = coverPage.Number?.Trim() ?? string.Empty;
        if (number.Length == 0)
        {
            return title;
        }

        var display = int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            value > 0
                ? value.ToString("D2", CultureInfo.InvariantCulture)
                : number;

        return string.Concat(title, " ", display);
    }

    public static IReadOnlyList<CoverSectionContent> Build(CoverPage coverPage, CoverPageTemplate template)
    {
        ArgumentNullException.ThrowIfNull(coverPage);
        ArgumentNullException.ThrowIfNull(template);

        var sections = new List<CoverSectionContent>(template.SectionOrder.Count);
        foreach (var section in template.SectionOrder)
        {
            var style = template.Styles.TryGetValue(section, out var sectionStyle)
                ? sectionStyle
                : new SectionStyle();

            sections.Add(new CoverSectionContent(section, BuildLines(section, coverPage, style), null));
        }

        var layout = CoverPageLayout.Calculate(sections, template);
        return sections
            .Select(section => section with { SpacingAfterMm = layout.SpacingAfterMm[section.Section] })
            .ToList();
    }

    private static IReadOnlyList<IReadOnlyList<CoverTextRun>> BuildLines(
        CoverSection section,
        CoverPage coverPage,
        SectionStyle style)
    {
        var size = style.FontSizePt ?? DefaultFontSizePt;
        var headingBold = style.IsBold ?? true;
        var lines = new List<IReadOnlyList<CoverTextRun>>();

        switch (section)
        {
            case CoverSection.DocumentTitle:
                lines.Add([new CoverTextRun(FormatDocumentTitle(coverPage), size, headingBold)]);
                if (!string.IsNullOrWhiteSpace(coverPage.TitleTopic))
                {
                    lines.Add(
                    [
                        new CoverTextRun("Title: ", size, headingBold),
                        new CoverTextRun(coverPage.TitleTopic, size, false),
                    ]);
                }
                break;

            case CoverSection.CourseInformation:
                lines.Add(
                [
                    new CoverTextRun("Course Title: ", size, headingBold),
                    new CoverTextRun(coverPage.CourseTitle, size, false),
                ]);
                lines.Add(
                [
                    new CoverTextRun("Course Code: ", size, headingBold),
                    new CoverTextRun(coverPage.CourseCode, size, false),
                ]);
                lines.Add(
                [
                    new CoverTextRun("Section: ", size, headingBold),
                    new CoverTextRun(
                        string.IsNullOrWhiteSpace(coverPage.Section) ? string.Empty : coverPage.Section,
                        size,
                        false),
                ]);
                break;

            case CoverSection.SubmittedTo:
                lines.Add([new CoverTextRun("Submitted to:", size, headingBold)]);
                if (coverPage.SubmittedTo is { } instructor)
                {
                    lines.Add([new CoverTextRun(instructor.Name, size, false)]);
                    if (!string.IsNullOrWhiteSpace(instructor.Designation))
                    {
                        lines.Add([new CoverTextRun(instructor.Designation, size, false)]);
                    }
                    if (!string.IsNullOrWhiteSpace(instructor.Department))
                    {
                        lines.Add([new CoverTextRun(instructor.Department, size, false)]);
                    }
                }
                break;

            case CoverSection.SubmittedBy:
                lines.Add([new CoverTextRun("Submitted by:", size, headingBold)]);
                var names = CoverPageTemplate.FormatSubmittedByLines(coverPage.Students);
                for (var i = 0; i < names.Count; i++)
                {
                    lines.Add([new CoverTextRun(FormatStudentLine(names[i], coverPage.Students[i]), size, false)]);
                }
                break;

            case CoverSection.SubmissionDate:
                lines.Add(
                [
                    new CoverTextRun("Date of Submission: ", size, headingBold),
                    new CoverTextRun(
                        coverPage.SubmissionDate == default
                            ? string.Empty
                            : FormatSubmissionDate(coverPage.SubmissionDate),
                        size,
                        false),
                ]);
                break;
        }

        return lines;
    }

    private static string FormatStudentLine(string name, Student student)
    {
        return string.IsNullOrWhiteSpace(student.StudentId) ? name : string.Concat(name, " (", student.StudentId, ")");
    }
}
