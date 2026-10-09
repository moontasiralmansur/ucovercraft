using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;
using UCoverCraft.Docx;

namespace UCoverCraft.Tests.Docx;

public class CoverPageRendererTests : IDisposable
{
    private const string DocumentPartPath = "word/document.xml";
    private const string StylesPartPath = "word/styles.xml";
    private const string DocumentRelationshipsPartPath = "word/_rels/document.xml.rels";
    private const string ContentTypesPartPath = "[Content_Types].xml";
    private const string ImagePartPath = "word/media/image1.png";
    private const string ImageRelationshipType =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
    private const string TimesNewRoman = "Times New Roman";

    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Wp =
        "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace A =
        "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace ContentTypes =
        "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Relationships =
        "http://schemas.openxmlformats.org/package/2006/relationships";

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
    public void Render_CreatesValidDocxPackage()
    {
        var path = Render(CreateCoverPage());
        var bytes = File.ReadAllBytes(path);

        Assert.NotEmpty(bytes);
        Assert.Equal(0x50, (int)bytes[0]);
        Assert.Equal(0x4B, (int)bytes[1]);

        string[] requiredParts =
        [
            ContentTypesPartPath,
            "_rels/.rels",
            "docProps/core.xml",
            "docProps/app.xml",
            DocumentPartPath,
            DocumentRelationshipsPartPath,
            StylesPartPath,
            ImagePartPath,
        ];

        using (var archive = ZipFile.OpenRead(path))
        {
            foreach (var part in requiredParts)
            {
                Assert.Contains(archive.Entries, entry => entry.FullName == part);
            }
        }

        var document = ReadDocument(path);
        Assert.Equal(W + "document", document.Root!.Name);
        Assert.NotNull(document.Root.Element(W + "body"));

        var contentTypes = ReadXml(path, ContentTypesPartPath);
        Assert.Contains(
            contentTypes.Root!.Elements(ContentTypes + "Override"),
            element => (string?)element.Attribute("PartName") == "/word/document.xml");

        var packageRelationships = ReadXml(path, "_rels/.rels");
        Assert.Contains(
            packageRelationships.Root!.Elements(Relationships + "Relationship"),
            element => (string?)element.Attribute("Target") == "word/document.xml");
    }

    [Fact]
    public void Render_UsesSingleA4Page_WithReferenceMargins()
    {
        var document = ReadDocument(Render(CreateCoverPage()));
        var body = document.Root!.Element(W + "body")!;
        var sectionProperties = body.Element(W + "sectPr");

        Assert.NotNull(sectionProperties);
        Assert.Single(body.Elements(W + "sectPr"));

        var pageSize = sectionProperties.Element(W + "pgSz");
        Assert.Equal("11906", pageSize!.Attribute(W + "w")!.Value);
        Assert.Equal("16838", pageSize.Attribute(W + "h")!.Value);

        var margins = sectionProperties.Element(W + "pgMar");
        Assert.NotNull(margins);
        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            Assert.Equal("1440", margins.Attribute(W + side)!.Value);
        }

        Assert.Equal("708", margins.Attribute(W + "header")!.Value);
        Assert.Equal("708", margins.Attribute(W + "footer")!.Value);
        Assert.Empty(document.Descendants(W + "br"));
        Assert.Empty(document.Descendants(W + "lastRenderedPageBreak"));
    }

    [Fact]
    public void Render_WritesEveryCoverPageField()
    {
        var text = ReadAllText(Render(CreateCoverPage()));

        Assert.Contains("PROJECT REPORT", text);
        Assert.Contains("Course Title: ", text);
        Assert.Contains("Software Engineering", text);
        Assert.Contains("Course Code: ", text);
        Assert.Contains("CSE-4402", text);
        Assert.Contains("Section: ", text);
        Assert.Contains("7", text);
        Assert.Contains("Submitted to:", text);
        Assert.Contains("Wahida Ferdose Urmi", text);
        Assert.Contains("Department of Computer Science and Engineering", text);
        Assert.Contains("Submitted by:", text);
        Assert.Contains("Jane Doe (S-1001)", text);
        Assert.Contains("John Smith (S-1002)", text);
        Assert.Contains("Date of Submission: ", text);
        Assert.Contains("13 June, 2026", text);
    }

    [Fact]
    public void Render_KeepsTheSectionLine_WhenSectionIsMissing()
    {
        var coverPage = CreateCoverPage();
        coverPage.Section = null;

        var text = ReadAllText(Render(coverPage, "no-section.docx"));

        Assert.Contains("Section: ", text);
        Assert.Contains("Course Code: ", text);
    }

    [Fact]
    public void Render_EmbedsUlabLogo_WithNativeSizeAndTemplateExtent()
    {
        var path = Render(CreateCoverPage());
        var image = ReadEntry(path, ImagePartPath);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, image[..8]);
        Assert.Equal("IHDR", Encoding.ASCII.GetString(image, 12, 4));
        Assert.Equal(363, BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(16)));
        Assert.Equal(139, BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20)));

        var document = ReadDocument(path);
        var extent = document.Descendants(Wp + "extent").Single();
        var transform = document.Descendants(A + "ext").Single();
        var template = CoverPageTemplate.Reference;
        var expectedWidth = (long)Math.Round(template.LogoWidthMm!.Value * 36000);
        var expectedHeight = (long)Math.Round(template.LogoHeightMm!.Value * 36000);

        Assert.InRange(long.Parse(extent.Attribute("cx")!.Value), expectedWidth - 100, expectedWidth + 100);
        Assert.InRange(long.Parse(extent.Attribute("cy")!.Value), expectedHeight - 100, expectedHeight + 100);
        Assert.Equal(extent.Attribute("cx")!.Value, transform.Attribute("cx")!.Value);
        Assert.Equal(extent.Attribute("cy")!.Value, transform.Attribute("cy")!.Value);

        var documentRelationships = ReadXml(path, DocumentRelationshipsPartPath);
        var imageRelationship = documentRelationships.Root!
            .Elements(Relationships + "Relationship")
            .Single(element => (string?)element.Attribute("Type") == ImageRelationshipType);
        Assert.Equal("media/image1.png", imageRelationship.Attribute("Target")!.Value);
        Assert.Equal(imageRelationship.Attribute("Id")!.Value, document.Descendants(A + "blip").Single().Attribute(R + "embed")!.Value);
    }

    [Fact]
    public void Render_UsesTimesNewRoman_ForEveryRunAndParagraphMark()
    {
        var path = Render(CreateCoverPage());
        var document = ReadDocument(path);

        foreach (var fonts in document.Descendants(W + "rFonts"))
        {
            Assert.Equal(TimesNewRoman, fonts.Attribute(W + "ascii")!.Value);
            Assert.Equal(TimesNewRoman, fonts.Attribute(W + "hAnsi")!.Value);
            Assert.Equal(TimesNewRoman, fonts.Attribute(W + "cs")!.Value);
        }

        var styles = ReadXml(path, StylesPartPath);
        var defaultFonts = styles.Root!.Descendants(W + "rFonts").First();
        Assert.Equal(TimesNewRoman, defaultFonts.Attribute(W + "ascii")!.Value);

        foreach (var run in document.Descendants(W + "r").Where(run => run.Element(W + "t") is not null))
        {
            Assert.Equal("28", run.Element(W + "rPr")!.Element(W + "sz")!.Attribute(W + "val")!.Value);
        }

        var logoRun = document.Descendants(W + "r").Single(run => run.Element(W + "drawing") is not null);
        Assert.Equal("22", logoRun.Element(W + "rPr")!.Element(W + "sz")!.Attribute(W + "val")!.Value);
    }

    [Fact]
    public void Render_RendersLabelsBoldAndValuesRegular()
    {
        var (bold, regular) = SplitRunsByWeight(Render(CreateCoverPage()));

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

        Assert.Equal(expectedBold, bold);
        Assert.Equal(expectedRegular, regular);
    }

    [Fact]
    public void Render_PreservesTrailingSpaceInLabelRuns()
    {
        var label = ReadDocument(Render(CreateCoverPage()))
            .Descendants(W + "r")
            .Single(candidate => ParagraphText(candidate) == "Course Title: ");

        Assert.Equal("preserve", label.Element(W + "t")!.Attribute(XNamespace.Xml + "space")!.Value);
    }

    [Fact]
    public void Render_CentersEveryParagraph_WithCalculatedLayoutSpacing()
    {
        var coverPage = CreateCoverPage();
        var layout = CoverPageLayout.Calculate(
            CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference),
            CoverPageTemplate.Reference);
        var gapTwips = (int)Math.Round(layout.GapMm * 1440 / 25.4, MidpointRounding.AwayFromZero);

        var paragraphs = ReadParagraphs(Render(coverPage));
        int[] expectedSpaceAfter = [gapTwips, gapTwips, 0, 0, gapTwips, 0, 0, gapTwips, 0, 0, gapTwips, 0];

        Assert.Equal(expectedSpaceAfter.Length, paragraphs.Count);

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var properties = paragraphs[i].Element(W + "pPr");
            Assert.NotNull(properties);

            var spacing = properties.Element(W + "spacing");
            Assert.NotNull(spacing);
            var justification = properties.Element(W + "jc");
            Assert.NotNull(justification);

            Assert.Equal(expectedSpaceAfter[i], int.Parse(spacing.Attribute(W + "after")!.Value));
            Assert.Equal(i == 0 ? "96" : "0", spacing.Attribute(W + "before")!.Value);
            Assert.Equal("276", spacing.Attribute(W + "line")!.Value);
            Assert.Equal("auto", spacing.Attribute(W + "lineRule")!.Value);
            Assert.Equal("center", justification.Attribute(W + "val")!.Value);
        }
    }

    [Fact]
    public void Render_WritesDocumentTitleAfterLogoAndBeforeCourseInformation()
    {
        var paragraphs = ReadParagraphs(Render(CreateCoverPage()));

        var logoIndex = paragraphs.FindIndex(paragraph => paragraph.Descendants(W + "drawing").Any());
        var titleIndex = paragraphs.FindIndex(paragraph => ParagraphText(paragraph) == "PROJECT REPORT");
        var courseIndex = paragraphs.FindIndex(paragraph =>
            ParagraphText(paragraph).StartsWith("Course Title: ", StringComparison.Ordinal));

        Assert.Equal(0, logoIndex);
        Assert.Equal(1, titleIndex);
        Assert.True(courseIndex > titleIndex, "The course information must be drawn after the document title.");
    }

    [Fact]
    public void Render_FollowsTemplateSectionOrder()
    {
        var paragraphs = ReadParagraphs(Render(CreateCoverPage()));
        var texts = paragraphs.Select(ParagraphText).ToList();

        int[] anchors =
        [
            texts.FindIndex(text => text == "PROJECT REPORT"),
            texts.FindIndex(text => text.StartsWith("Course Title: ", StringComparison.Ordinal)),
            texts.FindIndex(text => text == "Submitted to:"),
            texts.FindIndex(text => text == "Submitted by:"),
            texts.FindIndex(text => text.StartsWith("Date of Submission: ", StringComparison.Ordinal)),
        ];

        Assert.True(paragraphs[0].Descendants(W + "drawing").Any(), "The logo must come first.");
        Assert.DoesNotContain(-1, anchors);
        Assert.Equal(anchors.OrderBy(index => index), anchors);
    }

    [Theory]
    [InlineData(DocumentType.LabReport, null, "LAB REPORT")]
    [InlineData(DocumentType.ProjectReport, null, "PROJECT REPORT")]
    [InlineData(DocumentType.Assignment, null, "ASSIGNMENT")]
    [InlineData(DocumentType.Custom, "Thesis Progress Review", "Thesis Progress Review")]
    public void Render_WritesResolvedDocumentTitle_InTitlePosition(
        DocumentType documentType,
        string? customTitle,
        string expectedTitle)
    {
        var coverPage = CreateCoverPage();
        coverPage.DocumentTitle = CoverPageTemplate.ResolveDocumentTitle(documentType, customTitle);

        var paragraphs = ReadParagraphs(Render(coverPage, $"{documentType}.docx"));

        Assert.Equal(0, paragraphs.FindIndex(paragraph => paragraph.Descendants(W + "drawing").Any()));
        Assert.Equal(expectedTitle, ParagraphText(paragraphs[1]));
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
    public void Render_WritesSubmissionDateAsDayMonthYear()
    {
        var text = ReadAllText(Render(CreateCoverPage()));

        Assert.Contains("Date of Submission: 13 June, 2026", text);
    }

    [Fact]
    public void Render_WritesTheSubmissionDateAsTheFinalParagraph_WhenADateIsSupplied()
    {
        var paragraphs = ReadParagraphs(Render(CreateCoverPage(), "with-date.docx"));

        Assert.Equal(12, paragraphs.Count);
        Assert.Equal("Date of Submission: 13 June, 2026", ParagraphText(paragraphs[^1]));
    }

    [Fact]
    public void Render_WritesTheDateLabelAsTheFinalParagraph_WhenNoDateIsSupplied()
    {
        var coverPage = CreateCoverPage();
        coverPage.SubmissionDate = default;

        var path = Render(coverPage, "no-date.docx");
        var text = ReadAllText(path);
        var paragraphs = ReadParagraphs(path);

        Assert.Contains("Date of Submission: ", text);
        Assert.DoesNotContain("1 January", text);
        Assert.DoesNotContain("0001", text);
        Assert.Equal(12, paragraphs.Count);
        Assert.Equal("Date of Submission: ", ParagraphText(paragraphs[^1]));
        Assert.Equal("John Smith (S-1002)", ParagraphText(paragraphs[^2]));
    }

    [Fact]
    public void Render_CentersEveryParagraph_WithCalculatedLayoutSpacing_WhenNoDateIsSupplied()
    {
        var coverPage = CreateCoverPage();
        coverPage.SubmissionDate = default;
        var layout = CoverPageLayout.Calculate(
            CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference),
            CoverPageTemplate.Reference);
        var gapTwips = (int)Math.Round(layout.GapMm * 1440 / 25.4, MidpointRounding.AwayFromZero);

        var paragraphs = ReadParagraphs(Render(coverPage, "no-date-spacing.docx"));
        int[] expectedSpaceAfter = [gapTwips, gapTwips, 0, 0, gapTwips, 0, 0, gapTwips, 0, 0, gapTwips, 0];

        Assert.Equal(expectedSpaceAfter.Length, paragraphs.Count);

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var spacing = paragraphs[i].Element(W + "pPr")!.Element(W + "spacing");
            Assert.NotNull(spacing);
            Assert.Equal(expectedSpaceAfter[i], int.Parse(spacing!.Attribute(W + "after")!.Value));
            Assert.Equal(i == 0 ? "96" : "0", spacing.Attribute(W + "before")!.Value);
            Assert.Equal("center", paragraphs[i].Element(W + "pPr")!.Element(W + "jc")!.Attribute(W + "val")!.Value);
        }
    }

    [Fact]
    public void Render_WritesThirtyDecember2026_AsTheDateValue()
    {
        var coverPage = CreateCoverPage();
        coverPage.SubmissionDate = new DateOnly(2026, 12, 30);

        var paragraphs = ReadParagraphs(Render(coverPage, "december-date.docx"));

        Assert.Equal("Date of Submission: 30 December, 2026", ParagraphText(paragraphs[^1]));
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

        var singlePath = Render(single, "single.docx");
        var manyPath = Render(many, "many.docx");
        var singleText = ReadAllText(singlePath);
        var manyText = ReadAllText(manyPath);

        Assert.Contains("Solo Student (S-0001)", singleText);
        foreach (var student in many.Students)
        {
            Assert.Contains(student.Name, manyText);
        }

        var singleParagraphs = ReadParagraphs(singlePath);
        var manyParagraphs = ReadParagraphs(manyPath);
        Assert.Equal(singleParagraphs.Count + 3, manyParagraphs.Count);
        Assert.Equal(CountRuns(singleParagraphs) + 3, CountRuns(manyParagraphs));
    }

    [Fact]
    public void Render_WithMaximumStudentCount_KeepsASinglePage()
    {
        var coverPage = CreateWorstCaseCoverPage(CoverPageLayout.MaxStudents);
        var template = CoverPageTemplate.Reference;
        var layout = CoverPageLayout.Calculate(CoverContentBuilder.Build(coverPage, template), template);
        Assert.True(layout.FitsInTextArea);
        Assert.Equal(layout.TextHeightMm, layout.ContentHeightMm, 6);

        var path = Render(coverPage, "max-students.docx");
        var document = ReadDocument(path);
        var body = document.Root!.Element(W + "body")!;

        Assert.Single(body.Elements(W + "sectPr"));
        Assert.Empty(document.Descendants(W + "br"));
        Assert.Empty(document.Descendants(W + "lastRenderedPageBreak"));

        var paragraphs = ReadParagraphs(path);
        Assert.Equal(1 + 1 + 3 + 4 + (CoverPageLayout.MaxStudents + 1) + 1, paragraphs.Count);
        Assert.Equal(
            CoverPageLayout.MaxStudents,
            paragraphs.Count(paragraph => ParagraphText(paragraph).Contains("(241000", StringComparison.Ordinal)));
        Assert.Equal("Date of Submission: 13 June, 2026", ParagraphText(paragraphs[^1]));
    }

    [Fact]
    public void Render_Throws_WhenTheStudentCountExceedsTheMaximum()
    {
        var coverPage = CreateWorstCaseCoverPage(CoverPageLayout.MaxStudents + 1);
        var path = Path.Combine(_directory, "too-many-students.docx");

        Assert.Throws<InvalidOperationException>(() => CoverPageRenderer.Render(coverPage, path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Render_OverwritesExistingOutputFile()
    {
        var path = Path.Combine(_directory, "cover.docx");
        File.WriteAllText(path, "stale content");

        CoverPageRenderer.Render(CreateCoverPage(), path);

        Assert.Equal(W + "document", ReadDocument(path).Root!.Name);
    }

    [Fact]
    public void Render_Throws_WhenCoverPageIsNull()
    {
        var path = Path.Combine(_directory, "cover.docx");

        Assert.Throws<ArgumentNullException>(() => CoverPageRenderer.Render(null!, path));
    }

    [Fact]
    public void Render_Throws_WhenOutputPathIsBlank()
    {
        Assert.Throws<ArgumentException>(() => CoverPageRenderer.Render(CreateCoverPage(), "   "));
    }

    [Fact]
    public void Render_WritesTheNumberInsideTheDocumentTitleParagraph()
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = "03";

        var paragraphs = ReadParagraphs(Render(coverPage, "number.docx"));

        Assert.Equal(12, paragraphs.Count);
        Assert.Equal("PROJECT REPORT 03", ParagraphText(paragraphs[1]));
    }

    [Fact]
    public void Render_WritesTheTitleTopicParagraph_ImmediatelyAfterTheDocumentTitle()
    {
        var coverPage = CreateCoverPage();
        coverPage.TitleTopic = "Distributed Systems";

        var paragraphs = ReadParagraphs(Render(coverPage, "title-topic.docx"));

        Assert.Equal(13, paragraphs.Count);
        Assert.Equal(0, paragraphs.FindIndex(paragraph => paragraph.Descendants(W + "drawing").Any()));
        Assert.Equal("PROJECT REPORT", ParagraphText(paragraphs[1]));
        Assert.Equal("Title: Distributed Systems", ParagraphText(paragraphs[2]));
        Assert.StartsWith("Course Title: ", ParagraphText(paragraphs[3]));
    }

    [Fact]
    public void Render_WritesTheNumberAndTheTitleTopicLine_WhenBothAreSupplied()
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = "4";
        coverPage.TitleTopic = "Distributed Systems";

        var paragraphs = ReadParagraphs(Render(coverPage, "number-title.docx"));

        Assert.Equal(13, paragraphs.Count);
        Assert.Equal("PROJECT REPORT 04", ParagraphText(paragraphs[1]));
        Assert.Equal("Title: Distributed Systems", ParagraphText(paragraphs[2]));
    }

    [Fact]
    public void Render_OmitsTheOptionalParagraphs_WhenBothFieldsAreEmpty()
    {
        var path = Render(CreateCoverPage(), "no-optionals.docx");
        var text = ReadAllText(path);

        Assert.Contains("PROJECT REPORT", text);
        Assert.DoesNotContain(
            ReadParagraphs(path),
            paragraph => ParagraphText(paragraph).StartsWith("Title: ", StringComparison.Ordinal));
        Assert.Equal(12, ReadParagraphs(path).Count);
    }

    [Fact]
    public void Render_RendersTheTitleTopicLabelBoldAndValueRegular()
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = "2";
        coverPage.TitleTopic = "Distributed Systems";

        var (bold, regular) = SplitRunsByWeight(Render(coverPage, "optional-weights.docx"));

        Assert.Contains("PROJECT REPORT 02", bold);
        Assert.Contains("Title: ", bold);
        Assert.Contains("Distributed Systems", regular);
        Assert.DoesNotContain("Distributed Systems", bold);
    }

    [Fact]
    public void Render_CentersEveryParagraph_WithCalculatedLayoutSpacing_ForNumberAndTitleTopic()
    {
        var coverPage = CreateCoverPage();
        coverPage.Number = "4";
        coverPage.TitleTopic = "Distributed Systems";
        var layout = CoverPageLayout.Calculate(
            CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference),
            CoverPageTemplate.Reference);
        var gapTwips = (int)Math.Round(layout.GapMm * 1440 / 25.4, MidpointRounding.AwayFromZero);

        var paragraphs = ReadParagraphs(Render(coverPage, "number-title-spacing.docx"));
        int[] expectedSpaceAfter = [gapTwips, 0, gapTwips, 0, 0, gapTwips, 0, 0, gapTwips, 0, 0, gapTwips, 0];

        Assert.Equal(expectedSpaceAfter.Length, paragraphs.Count);

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var spacing = paragraphs[i].Element(W + "pPr")!.Element(W + "spacing");
            Assert.NotNull(spacing);
            Assert.Equal(expectedSpaceAfter[i], int.Parse(spacing!.Attribute(W + "after")!.Value));
            Assert.Equal(i == 0 ? "96" : "0", spacing.Attribute(W + "before")!.Value);
            Assert.Equal("276", spacing.Attribute(W + "line")!.Value);
            Assert.Equal("center", paragraphs[i].Element(W + "pPr")!.Element(W + "jc")!.Attribute(W + "val")!.Value);
        }
    }

    [Fact]
    public void Render_WithTenStudentsNumberAndTitleTopic_KeepsASinglePage()
    {
        var coverPage = CreateWorstCaseCoverPage(CoverPage.MaxStudents);
        coverPage.Number = "12";
        coverPage.TitleTopic = "Distributed Systems";
        var layout = CoverPageLayout.Calculate(
            CoverContentBuilder.Build(coverPage, CoverPageTemplate.Reference),
            CoverPageTemplate.Reference);
        Assert.True(layout.FitsInTextArea);
        Assert.Equal(layout.TextHeightMm, layout.ContentHeightMm, 6);

        var path = Render(coverPage, "product-max-optionals.docx");
        var document = ReadDocument(path);
        var body = document.Root!.Element(W + "body")!;

        Assert.Single(body.Elements(W + "sectPr"));
        Assert.Empty(document.Descendants(W + "br"));

        var paragraphs = ReadParagraphs(path);
        Assert.Equal(1 + 2 + 3 + 4 + (CoverPage.MaxStudents + 1) + 1, paragraphs.Count);
        Assert.Equal("PROJECT REPORT 12", ParagraphText(paragraphs[1]));
        Assert.Equal("Title: Distributed Systems", ParagraphText(paragraphs[2]));
        Assert.Equal("Date of Submission: 13 June, 2026", ParagraphText(paragraphs[^1]));
    }

    private string Render(CoverPage coverPage, string fileName = "cover.docx")
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

    private static List<XElement> ReadParagraphs(string path)
    {
        return ReadDocument(path).Root!.Element(W + "body")!.Elements(W + "p").ToList();
    }

    private static XDocument ReadDocument(string path)
    {
        return ReadXml(path, DocumentPartPath);
    }

    private static XDocument ReadXml(string path, string partPath)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry(partPath);
        Assert.NotNull(entry);
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static byte[] ReadEntry(string path, string partPath)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry(partPath);
        Assert.NotNull(entry);
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string ReadAllText(string path)
    {
        return string.Join("\n", ReadParagraphs(path).Select(ParagraphText));
    }

    private static string ParagraphText(XElement paragraph)
    {
        return string.Concat(paragraph.Descendants(W + "t").Select(node => node.Value));
    }

    private static int CountRuns(IEnumerable<XElement> paragraphs)
    {
        return paragraphs.Sum(paragraph => paragraph.Elements(W + "r").Count());
    }

    private static (List<string> Bold, List<string> Regular) SplitRunsByWeight(string path)
    {
        var bold = new List<string>();
        var regular = new List<string>();

        foreach (var run in ReadDocument(path).Descendants(W + "r"))
        {
            var text = string.Concat(run.Elements(W + "t").Select(node => node.Value));
            if (text.Length == 0)
            {
                continue;
            }

            var isBold = run.Element(W + "rPr")?.Element(W + "b") is not null;
            (isBold ? bold : regular).Add(text);
        }

        return (bold, regular);
    }
}
