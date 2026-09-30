using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf.IO;
using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;
using UCoverCraft.Pdf;

namespace UCoverCraft.Tests.Pdf;

public class CoverPageRendererTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "UCoverCraft.Tests",
        Guid.NewGuid().ToString("N"));

    public CoverPageRendererTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Render_WritesFile_WithPdfHeader()
    {
        var path = Render(CreateCoverPage());

        Assert.True(File.Exists(path));
        Assert.NotEmpty(File.ReadAllBytes(path));
        Assert.StartsWith("%PDF-", ReadRaw(path));
    }

    [Fact]
    public void Render_ProducesExactlyOneA4Page()
    {
        var path = Render(CreateCoverPage());

        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        var pageCount = document.PageCount;

        Assert.Equal(1, pageCount);
        Assert.Matches(@"\[0 0 595\.\d+ 841\.\d+\]", ReadRaw(path));
    }

    [Fact]
    public void Render_EmbedsUlabLogoImage_WithNativeDimensions()
    {
        var raw = ReadRaw(Render(CreateCoverPage()));

        Assert.Contains("/Subtype/Image", raw);
        Assert.Contains("/Width 363", raw);
        Assert.Contains("/Height 139", raw);
    }

    [Fact]
    public void Render_PlacesLogoUsingTemplateSizeAndMargins()
    {
        var content = ReadPageContent(Render(CreateCoverPage()));

        Assert.Matches(@"317\.\d+ 0 0 121\.\d+ 139\.\d+ 643\.\d+ cm /I0 Do", content);
    }

    [Fact]
    public void Render_EmbedsTimesNewRomanRegularAndBold()
    {
        var raw = ReadRaw(Render(CreateCoverPage()));

        Assert.Matches(@"Times#20New#20Roman(?!,)", raw);
        Assert.Contains("Times#20New#20Roman,Bold", raw);
    }

    [Fact]
    public void Render_WritesEveryCoverPageField()
    {
        var content = ReadPageContent(Render(CreateCoverPage()));

        Assert.Contains("(PROJECT REPORT)", content);
        Assert.Contains("(Course Title: )", content);
        Assert.Contains("(Software Engineering)", content);
        Assert.Contains("(Course Code: )", content);
        Assert.Contains("(CSE-4402)", content);
        Assert.Contains("(Section: )", content);
        Assert.Contains("(7)", content);
        Assert.Contains("(Submitted to:)", content);
        Assert.Contains("(Wahida Ferdose Urmi)", content);
        Assert.Contains("(Department of Computer Science and Engineering)", content);
        Assert.Contains("(Submitted by:)", content);
        Assert.Contains("(Jane Doe", content);
        Assert.Contains("S-1001", content);
        Assert.Contains("(John Smith", content);
        Assert.Contains("S-1002", content);
        Assert.Contains("(Date of Submission: )", content);
        Assert.Contains("(13 June, 2026)", content);
    }

    [Fact]
    public void Render_WritesDocumentTitleAfterLogoAndBeforeCourseInformation()
    {
        var content = ReadPageContent(Render(CreateCoverPage()));

        var logoIndex = content.IndexOf("/I0 Do", StringComparison.Ordinal);
        var titleIndex = content.IndexOf("(PROJECT REPORT)", StringComparison.Ordinal);
        var courseIndex = content.IndexOf("(Course Title: )", StringComparison.Ordinal);

        Assert.True(logoIndex >= 0, "The logo draw was not found.");
        Assert.True(titleIndex > logoIndex, "The document title must be drawn after the logo.");
        Assert.True(courseIndex > titleIndex, "The course information must be drawn after the document title.");
    }

    [Fact]
    public void Render_RendersLabelsBoldAndValuesRegular()
    {
        var path = Render(CreateCoverPage());
        var (bold, regular) = SplitTextByWeight(ReadRaw(path), ReadPageContent(path));

        string[] expectedBold =
        [
            "PROJECT REPORT",
            "Course Title: ",
            "Course Code: ",
            "Section: ",
            "Submitted to:",
            "Submitted by:",
            "Date of Submission: ",
        ];

        string[] expectedRegular =
        [
            "Software Engineering",
            "CSE-4402",
            "7",
            "Wahida Ferdose Urmi",
            "Department of Computer Science and Engineering",
            "Jane Doe (S-1001)",
            "John Smith (S-1002)",
            "13 June, 2026",
        ];

        foreach (var text in expectedBold)
        {
            Assert.Contains(text, bold);
        }

        foreach (var text in expectedRegular)
        {
            Assert.Contains(text, regular);
        }
    }

    [Fact]
    public void Render_WritesExpectedNumberOfTextRuns()
    {
        var content = ReadPageContent(Render(CreateCoverPage()));

        Assert.Equal(15, CountTextRuns(content));
    }

    [Fact]
    public void Render_SpreadsSectionsSoTheFinalDateEndsAtTheBottomOfTheTextArea()
    {
        var coverPage = CreateCoverPage();
        var template = CoverPageTemplate.Reference;
        var sections = CoverContentBuilder.Build(coverPage, template);
        var layout = CoverPageLayout.Calculate(sections, template);
        var lineMm = CoverPageLayout.LineHeightMm(CoverContentBuilder.DefaultFontSizePt);

        var baselines = ReadBaselines(ReadPageContent(Render(coverPage)));
        var measuredDeltaMm = (baselines[0] - baselines[^1]) * 25.4 / 72;

        var ordered = sections.ToList();
        var firstTextIndex = ordered.FindIndex(section => section.Lines.Count > 0);
        var lastTextIndex = ordered.FindLastIndex(section => section.Lines.Count > 0);
        var lineCount = sections.Sum(section => section.Lines.Count);
        var expectedDeltaMm = ((lineCount - 1) * lineMm) + ((lastTextIndex - firstTextIndex) * layout.GapMm);

        Assert.Equal(expectedDeltaMm, measuredDeltaMm, 2);

        var titleTopMm = (template.MarginTopMm ?? 0)
            + (template.LogoTopOffsetMm ?? 0)
            + (template.LogoHeightMm ?? 0)
            + layout.GapMm;
        var dateBottomMm = titleTopMm + measuredDeltaMm + lineMm;

        Assert.Equal(CoverPageTemplate.A4HeightMm - (template.MarginBottomMm ?? 0), dateBottomMm, 2);
    }

    [Fact]
    public void Render_RendersEveryStudent_WhenStudentCountVaries()
    {
        var single = CreateCoverPage();
        single.Students = [new Student { Name = "Solo Student", StudentId = "S-0001" }];

        var many = CreateCoverPage();
        many.Students =
        [
            new Student { Name = "Ada Lovelace", StudentId = "S-2001" },
            new Student { Name = "Grace Hopper", StudentId = "S-2002" },
            new Student { Name = "Alan Turing", StudentId = "S-2003" },
            new Student { Name = "Edsger Dijkstra", StudentId = "S-2004" },
        ];

        var singleContent = ReadPageContent(Render(single, "single.pdf"));
        var manyContent = ReadPageContent(Render(many, "many.pdf"));

        Assert.Contains("(Solo Student", singleContent);
        foreach (var student in many.Students)
        {
            Assert.Contains(student.Name, manyContent);
        }

        Assert.Equal(CountTextRuns(singleContent) + 3, CountTextRuns(manyContent));
    }

    [Fact]
    public void Render_WithMaximumStudentCount_EndsTheDateInsideTheBottomMargin()
    {
        var coverPage = CreateWorstCaseCoverPage(CoverPageLayout.MaxStudents);
        var template = CoverPageTemplate.Reference;
        var layout = CoverPageLayout.Calculate(
            CoverContentBuilder.Build(coverPage, template),
            template);
        Assert.True(layout.FitsInTextArea);

        var path = Render(coverPage, "max-students.pdf");
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);

        var lineMm = CoverPageLayout.LineHeightMm(CoverContentBuilder.DefaultFontSizePt);
        var baselines = ReadBaselines(ReadPageContent(path));
        var titleTopMm = (template.MarginTopMm ?? 0)
            + (template.LogoTopOffsetMm ?? 0)
            + (template.LogoHeightMm ?? 0)
            + layout.GapMm;
        var dateBottomMm = titleTopMm + ((baselines[0] - baselines[^1]) * 25.4 / 72) + lineMm;
        var usableBottomMm = CoverPageTemplate.A4HeightMm - (template.MarginBottomMm ?? 0);

        Assert.Equal(usableBottomMm, dateBottomMm, 2);
        Assert.True(
            dateBottomMm <= usableBottomMm + 0.01,
            $"The date ends at {dateBottomMm:F4} mm, past the usable bottom of {usableBottomMm:F4} mm.");
    }

    [Fact]
    public void Render_Throws_WhenTheStudentCountExceedsTheMaximum()
    {
        var coverPage = CreateWorstCaseCoverPage(CoverPageLayout.MaxStudents + 1);
        var path = Path.Combine(_directory, "too-many-students.pdf");

        Assert.Throws<InvalidOperationException>(() => CoverPageRenderer.Render(coverPage, path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Render_KeepsTheSectionLine_WhenSectionIsMissing()
    {
        var coverPage = CreateCoverPage();
        coverPage.Section = null;

        var content = ReadPageContent(Render(coverPage, "no-section.pdf"));

        Assert.Contains("(Section: ", content);
        Assert.Contains("(Course Code: )", content);
    }

    [Fact]
    public void Render_OverwritesExistingOutputFile()
    {
        var path = Path.Combine(_directory, "cover.pdf");
        File.WriteAllText(path, "stale content");

        CoverPageRenderer.Render(CreateCoverPage(), path);

        Assert.StartsWith("%PDF-", ReadRaw(path));
    }

    [Fact]
    public void Render_Throws_WhenCoverPageIsNull()
    {
        var path = Path.Combine(_directory, "cover.pdf");

        Assert.Throws<ArgumentNullException>(() => CoverPageRenderer.Render(null!, path));
    }

    [Fact]
    public void Render_Throws_WhenOutputPathIsBlank()
    {
        Assert.Throws<ArgumentException>(() => CoverPageRenderer.Render(CreateCoverPage(), "   "));
    }

    [Theory]
    [InlineData(2026, 6, 13, "13 June, 2026")]
    [InlineData(2026, 1, 1, "1 January, 2026")]
    [InlineData(2026, 2, 2, "2 February, 2026")]
    [InlineData(2026, 3, 3, "3 March, 2026")]
    [InlineData(2026, 4, 11, "11 April, 2026")]
    [InlineData(2026, 5, 21, "21 May, 2026")]
    [InlineData(2026, 7, 22, "22 July, 2026")]
    [InlineData(2026, 8, 23, "23 August, 2026")]
    [InlineData(2026, 12, 31, "31 December, 2026")]
    public void FormatSubmissionDate_FormatsDayMonthYear(int year, int month, int day, string expected)
    {
        var formatted = CoverPageRenderer.FormatSubmissionDate(new DateOnly(year, month, day));

        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void Render_WritesTheNumberInsideTheDocumentTitleLine()
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = "3";

        var content = ReadPageContent(Render(coverPage, "number.pdf"));

        Assert.Contains("(PROJECT REPORT 03)", content);
        Assert.DoesNotContain("(PROJECT REPORT)", content);
    }

    [Fact]
    public void Render_WritesTheTitleTopicLine_AfterTheDocumentTitle()
    {
        var coverPage = CreateCoverPage();
        coverPage.TitleTopic = "Distributed Systems";

        var content = ReadPageContent(Render(coverPage, "title-topic.pdf"));
        var titleIndex = content.IndexOf("(PROJECT REPORT)", StringComparison.Ordinal);
        var labelIndex = content.IndexOf("(Title: )", StringComparison.Ordinal);
        var valueIndex = content.IndexOf("(Distributed Systems)", StringComparison.Ordinal);
        var courseIndex = content.IndexOf("(Course Title: )", StringComparison.Ordinal);

        Assert.True(titleIndex >= 0, "The document title was not found.");
        Assert.True(labelIndex > titleIndex, "The title topic label must follow the document title.");
        Assert.True(valueIndex > labelIndex, "The title topic value must follow its label.");
        Assert.True(courseIndex > valueIndex, "The course information must follow the title topic.");
    }

    [Fact]
    public void Render_OmitsTheOptionalText_WhenBothFieldsAreEmpty()
    {
        var content = ReadPageContent(Render(CreateCoverPage(), "no-optionals.pdf"));

        Assert.Contains("(PROJECT REPORT)", content);
        Assert.DoesNotContain("(Title: ", content);
        Assert.Equal(15, CountTextRuns(content));
    }

    [Fact]
    public void Render_RendersTheNumberBold_AndTheTitleTopicWithABoldLabel()
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = "2";
        coverPage.TitleTopic = "Distributed Systems";
        var path = Render(coverPage, "optional-weights.pdf");

        var (bold, regular) = SplitTextByWeight(ReadRaw(path), ReadPageContent(path));

        Assert.Contains("PROJECT REPORT 02", bold);
        Assert.DoesNotContain("PROJECT REPORT 02", regular);
        Assert.Contains("Title: ", bold);
        Assert.Contains("Distributed Systems", regular);
        Assert.DoesNotContain("Distributed Systems", bold);
    }

    [Fact]
    public void Render_WithNumberAndTitleTopic_SpreadsSectionsToTheBottomOfTheTextArea()
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = "4";
        coverPage.TitleTopic = "Distributed Systems";
        var template = CoverPageTemplate.Reference;
        var sections = CoverContentBuilder.Build(coverPage, template);
        var layout = CoverPageLayout.Calculate(sections, template);
        Assert.True(layout.FitsInTextArea);

        var path = Render(coverPage, "number-title.pdf");
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);

        var lineMm = CoverPageLayout.LineHeightMm(CoverContentBuilder.DefaultFontSizePt);
        var content = ReadPageContent(path);
        var baselines = ReadBaselines(content);
        var titleTopMm = (template.MarginTopMm ?? 0)
            + (template.LogoTopOffsetMm ?? 0)
            + (template.LogoHeightMm ?? 0)
            + layout.GapMm;
        var dateBottomMm = titleTopMm + ((baselines[0] - baselines[^1]) * 25.4 / 72) + lineMm;

        Assert.Equal(CoverPageTemplate.A4HeightMm - (template.MarginBottomMm ?? 0), dateBottomMm, 2);
        Assert.Equal(17, CountTextRuns(content));
        Assert.Equal(
            layout.TextHeightMm,
            layout.OccupiedHeightMm + (layout.GapMm * 5),
            6);
    }

    [Fact]
    public void Render_WithTenStudentsNumberAndTitleTopic_KeepsASinglePage()
    {
        var coverPage = CreateWorstCaseCoverPage(CoverPage.MaxStudents);
        coverPage.Number = "12";
        coverPage.TitleTopic = "Distributed Systems";
        var template = CoverPageTemplate.Reference;
        var layout = CoverPageLayout.Calculate(CoverContentBuilder.Build(coverPage, template), template);
        Assert.True(layout.FitsInTextArea);

        var path = Render(coverPage, "product-max-optionals.pdf");
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        var content = ReadPageContent(path);

        Assert.Equal(1, document.PageCount);
        Assert.Contains("(PROJECT REPORT 12)", content);
        Assert.Contains("(Title: )", content);
        Assert.Contains("(Distributed Systems)", content);
    }

    private string Render(CoverPage coverPage, string fileName = "cover.pdf")
    {
        var path = Path.Combine(_directory, fileName);
        CoverPageRenderer.Render(coverPage, path);
        return path;
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

    private static CoverPage CreateWorstCaseCoverPage(int studentCount) => new()
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
            .Select(index => new Student
            {
                Name = $"Student Number {index}",
                StudentId = $"241000{index:000}",
            })
            .ToList(),
        SubmissionDate = new DateOnly(2026, 6, 13),
    };

    private static string ReadRaw(string path)
    {
        return Encoding.Latin1.GetString(File.ReadAllBytes(path));
    }

    private static string ReadPageContent(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var raw = Encoding.Latin1.GetString(bytes);

        var reference = Regex.Match(raw, @"/Contents (\d+) 0 R");
        Assert.True(reference.Success, "The page content reference was not found.");

        var objectNumber = reference.Groups[1].Value;
        var objectStart = Regex.Match(raw, $@"\b{objectNumber} 0 obj");
        Assert.True(objectStart.Success, "The page content object was not found.");

        var streamStart = raw.IndexOf("stream", objectStart.Index, StringComparison.Ordinal);
        Assert.True(streamStart >= 0, "The page content stream was not found.");

        var dataStart = streamStart + "stream".Length;
        if (raw[dataStart] == '\r')
        {
            dataStart++;
        }

        if (raw[dataStart] == '\n')
        {
            dataStart++;
        }

        var dataEnd = raw.IndexOf("endstream", dataStart, StringComparison.Ordinal);
        Assert.True(dataEnd > dataStart, "The page content stream was truncated.");

        var data = bytes.AsSpan(dataStart, dataEnd - dataStart);
        return TryInflate(data) ?? Encoding.Latin1.GetString(data);
    }

    private static string? TryInflate(ReadOnlySpan<byte> data)
    {
        try
        {
            using var input = new MemoryStream(data.ToArray());
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return Encoding.Latin1.GetString(output.ToArray());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int CountTextRuns(string content)
    {
        return Regex.Matches(content, @"\) Tj").Count;
    }

    private static List<double> ReadBaselines(string content)
    {
        var baselines = new List<double>();
        var y = 0d;

        foreach (var line in content.Split('\n'))
        {
            var text = line.Trim();
            if (text == "BT")
            {
                y = 0d;
                continue;
            }

            var move = Regex.Match(text, @"^([\d.-]+) ([\d.-]+) Td$");
            if (move.Success)
            {
                y += double.Parse(move.Groups[2].Value, CultureInfo.InvariantCulture);
                continue;
            }

            if (Regex.IsMatch(text, @"^\((?:[^()\\]|\\.)*\) Tj$"))
            {
                baselines.Add(y);
            }
        }

        return baselines;
    }

    private static (List<string> Bold, List<string> Regular) SplitTextByWeight(string raw, string content)
    {
        var fontNames = ReadFontNames(raw);
        var bold = new List<string>();
        var regular = new List<string>();
        string? currentFont = null;

        foreach (var line in content.Split('\n'))
        {
            var text = line.Trim();
            var font = Regex.Match(text, @"^/(F\d+) [\d.]+ Tf$");
            if (font.Success)
            {
                currentFont = font.Groups[1].Value;
                continue;
            }

            var show = Regex.Match(text, @"^\((?<text>(?:[^()\\]|\\.)*)\) Tj$");
            if (!show.Success)
            {
                continue;
            }

            var value = UnescapePdfString(show.Groups["text"].Value);
            var isBold = currentFont is not null
                && fontNames.TryGetValue(currentFont, out var fontName)
                && fontName.EndsWith(",Bold", StringComparison.Ordinal);
            (isBold ? bold : regular).Add(value);
        }

        return (bold, regular);
    }

    private static Dictionary<string, string> ReadFontNames(string raw)
    {
        var names = new Dictionary<string, string>();
        var resources = Regex.Match(raw, @"/Font<<([^>]*)>>");
        Assert.True(resources.Success, "The font resources were not found.");

        foreach (Match entry in Regex.Matches(resources.Groups[1].Value, @"/(F\d+) (\d+) 0 R"))
        {
            var fontObject = Regex.Match(raw, $@"\b{entry.Groups[2].Value} 0 obj[\s\S]*?endobj");
            Assert.True(fontObject.Success, "The font object was not found.");

            var baseFont = Regex.Match(fontObject.Value, @"/BaseFont/([^/>\s]+)");
            Assert.True(baseFont.Success, "The font base name was not found.");

            names[entry.Groups[1].Value] = baseFont.Groups[1].Value;
        }

        return names;
    }

    private static string UnescapePdfString(string value)
    {
        return value
            .Replace("\\(", "(", StringComparison.Ordinal)
            .Replace("\\)", ")", StringComparison.Ordinal);
    }
}
