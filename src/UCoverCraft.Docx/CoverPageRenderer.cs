using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Docx;

public static class CoverPageRenderer
{
    private const string LogoResourceName = "UCoverCraft.Docx.ulab-logo.png";
    private const int LineSpacingHundredths = 276;
    private const int TwipsPerInch = 1440;
    private const int EmuPerMillimetre = 36000;
    private const int HalfPointsPerPoint = 2;
    private const int ReferenceHeaderFooterTwips = 708;
    private const int ReferenceColumnSpaceTwips = 708;
    private const int ReferenceLinePitchTwips = 360;
    private const string ImageRelationshipId = "rId1";
    private const string ImagePartPath = "word/media/image1.png";
    private const string PictureDataUri = "http://schemas.openxmlformats.org/drawingml/2006/picture";

    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Wp =
        "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace A =
        "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Pic =
        "http://schemas.openxmlformats.org/drawingml/2006/picture";
    private static readonly XNamespace ContentTypes =
        "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Relationships =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace CoreProperties =
        "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace DublinCore =
        "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace DublinTerms =
        "http://purl.org/dc/terms/";
    private static readonly XNamespace Xsi =
        "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace ExtendedProperties =
        "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

    public static string FormatSubmissionDate(DateOnly submissionDate)
    {
        return CoverContentBuilder.FormatSubmissionDate(submissionDate);
    }

    public static void Render(CoverPage coverPage, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(coverPage);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var template = CoverPageTemplate.Reference;
        var sections = CoverContentBuilder.Build(coverPage, template);
        CoverPageLayout.Calculate(sections, template).EnsureFitsInTextArea();
        EnsureOutputDirectoryExists(outputPath);

        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteXmlPart(archive, "[Content_Types].xml", CreateContentTypesDocument());
        WriteXmlPart(archive, "_rels/.rels", CreatePackageRelationshipsDocument());
        WriteXmlPart(archive, "docProps/core.xml", CreateCorePropertiesDocument(coverPage));
        WriteXmlPart(archive, "docProps/app.xml", CreateAppPropertiesDocument());
        WriteXmlPart(archive, "word/document.xml", CreateDocument(sections, template));
        WriteXmlPart(archive, "word/_rels/document.xml.rels", CreateDocumentRelationshipsDocument());
        WriteXmlPart(archive, "word/styles.xml", CreateStylesDocument(template));
        WriteBinaryPart(archive, ImagePartPath, OpenLogo());
    }

    private static XDocument CreateDocument(
        IReadOnlyList<CoverSectionContent> sections,
        CoverPageTemplate template)
    {
        var body = new XElement(W + "body");
        foreach (var paragraph in BuildParagraphs(sections, template))
        {
            body.Add(paragraph);
        }

        body.Add(CreateSectionProperties(template));

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                W + "document",
                new XAttribute(XNamespace.Xmlns + "w", W),
                new XAttribute(XNamespace.Xmlns + "r", R),
                new XAttribute(XNamespace.Xmlns + "wp", Wp),
                new XAttribute(XNamespace.Xmlns + "a", A),
                new XAttribute(XNamespace.Xmlns + "pic", Pic),
                body));
    }

    private static IReadOnlyList<XElement> BuildParagraphs(
        IReadOnlyList<CoverSectionContent> sections,
        CoverPageTemplate template)
    {
        var paragraphs = new List<XElement>();

        foreach (var section in sections)
        {
            var spaceAfter = section.SpacingAfterMm is { } gap ? MmToTwips(gap) : 0;

            if (section.Section == CoverSection.Logo)
            {
                paragraphs.Add(CreateLogoParagraph(template, spaceAfter));
                continue;
            }

            var style = ResolveStyle(template, section.Section);

            for (var i = 0; i < section.Lines.Count; i++)
            {
                paragraphs.Add(CreateTextParagraph(
                    template,
                    style,
                    section.Lines[i],
                    spaceBeforeTwips: 0,
                    spaceAfterTwips: i == section.Lines.Count - 1 ? spaceAfter : 0));
            }
        }

        return paragraphs;
    }

    private static XElement CreateLogoParagraph(CoverPageTemplate template, int spaceAfterTwips)
    {
        var style = ResolveStyle(template, CoverSection.Logo);
        var sizePt = style.FontSizePt ?? CoverContentBuilder.DefaultFontSizePt;
        var isBold = style.IsBold ?? false;
        var spaceBefore = template.LogoTopOffsetMm is { } offset ? MmToTwips(offset) : 0;

        return new XElement(
            W + "p",
            new XElement(
                W + "pPr",
                CreateSpacing(spaceBefore, spaceAfterTwips),
                CreateJustification(template),
                CreateRunProperties(template, sizePt, isBold)),
            new XElement(
                W + "r",
                CreateRunProperties(template, sizePt, isBold),
                CreateLogoDrawing(template)));
    }

    private static XElement CreateTextParagraph(
        CoverPageTemplate template,
        SectionStyle style,
        IReadOnlyList<CoverTextRun> runs,
        int spaceBeforeTwips,
        int spaceAfterTwips)
    {
        var sizePt = style.FontSizePt ?? CoverContentBuilder.DefaultFontSizePt;
        var markBold = runs.Count > 0 && runs.All(run => run.IsBold);

        var paragraph = new XElement(
            W + "p",
            new XElement(
                W + "pPr",
                CreateSpacing(spaceBeforeTwips, spaceAfterTwips),
                CreateJustification(template),
                CreateRunProperties(template, sizePt, markBold)));

        foreach (var run in runs)
        {
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            paragraph.Add(
                new XElement(
                    W + "r",
                    CreateRunProperties(template, run.FontSizePt, run.IsBold),
                    new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), run.Text)));
        }

        return paragraph;
    }

    private static XElement CreateLogoDrawing(CoverPageTemplate template)
    {
        var widthEmu = MmToEmu(template.LogoWidthMm ?? 0);
        var heightEmu = MmToEmu(template.LogoHeightMm ?? 0);

        return new XElement(
            W + "drawing",
            new XElement(
                Wp + "inline",
                new XAttribute("distT", "0"),
                new XAttribute("distB", "0"),
                new XAttribute("distL", "0"),
                new XAttribute("distR", "0"),
                new XElement(Wp + "extent", new XAttribute("cx", widthEmu), new XAttribute("cy", heightEmu)),
                new XElement(
                    Wp + "effectExtent",
                    new XAttribute("l", "0"),
                    new XAttribute("t", "0"),
                    new XAttribute("r", "0"),
                    new XAttribute("b", "0")),
                new XElement(Wp + "docPr", new XAttribute("id", "1"), new XAttribute("name", "ULAB Logo")),
                new XElement(
                    Wp + "cNvGraphicFramePr",
                    new XElement(A + "graphicFrameLocks", new XAttribute("noChangeAspect", "1"))),
                new XElement(
                    A + "graphic",
                    new XElement(
                        A + "graphicData",
                        new XAttribute("uri", PictureDataUri),
                        new XElement(
                            Pic + "pic",
                            new XElement(
                                Pic + "nvPicPr",
                                new XElement(Pic + "cNvPr", new XAttribute("id", "1"), new XAttribute("name", "ulab-logo.png")),
                                new XElement(
                                    Pic + "cNvPicPr",
                                    new XElement(
                                        A + "picLocks",
                                        new XAttribute("noChangeAspect", "1"),
                                        new XAttribute("noChangeArrowheads", "1")))),
                            new XElement(
                                Pic + "blipFill",
                                new XElement(A + "blip", new XAttribute(R + "embed", ImageRelationshipId)),
                                new XElement(A + "stretch", new XElement(A + "fillRect"))),
                            new XElement(
                                Pic + "spPr",
                                new XElement(
                                    A + "xfrm",
                                    new XElement(A + "off", new XAttribute("x", "0"), new XAttribute("y", "0")),
                                    new XElement(A + "ext", new XAttribute("cx", widthEmu), new XAttribute("cy", heightEmu))),
                                new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst")),
                                new XElement(A + "noFill"),
                                new XElement(A + "ln", new XElement(A + "noFill"))))))));
    }

    private static XElement CreateSpacing(int spaceBeforeTwips, int spaceAfterTwips)
    {
        return new XElement(
            W + "spacing",
            new XAttribute(W + "before", spaceBeforeTwips),
            new XAttribute(W + "after", spaceAfterTwips),
            new XAttribute(W + "line", LineSpacingHundredths),
            new XAttribute(W + "lineRule", "auto"));
    }

    private static XElement CreateJustification(CoverPageTemplate template)
    {
        var value = template.Alignment == TemplateAlignment.Center ? "center" : "left";
        return new XElement(W + "jc", new XAttribute(W + "val", value));
    }

    private static XElement CreateRunProperties(CoverPageTemplate template, double sizePt, bool isBold)
    {
        var fontFamily = template.FontFamily ?? CoverContentBuilder.DefaultFontFamily;
        var properties = new XElement(
            W + "rPr",
            new XElement(
                W + "rFonts",
                new XAttribute(W + "ascii", fontFamily),
                new XAttribute(W + "hAnsi", fontFamily),
                new XAttribute(W + "cs", fontFamily)));

        if (isBold)
        {
            properties.Add(new XElement(W + "b"));
            properties.Add(new XElement(W + "bCs"));
        }

        properties.Add(new XElement(W + "sz", new XAttribute(W + "val", HalfPoints(sizePt))));
        properties.Add(new XElement(W + "szCs", new XAttribute(W + "val", HalfPoints(sizePt))));
        return properties;
    }

    private static XElement CreateSectionProperties(CoverPageTemplate template)
    {
        return new XElement(
            W + "sectPr",
            new XElement(
                W + "pgSz",
                new XAttribute(W + "w", MmToTwips(CoverPageTemplate.A4WidthMm)),
                new XAttribute(W + "h", MmToTwips(CoverPageTemplate.A4HeightMm))),
            new XElement(
                W + "pgMar",
                new XAttribute(W + "top", MmToTwips(template.MarginTopMm ?? 0)),
                new XAttribute(W + "right", MmToTwips(template.MarginRightMm ?? 0)),
                new XAttribute(W + "bottom", MmToTwips(template.MarginBottomMm ?? 0)),
                new XAttribute(W + "left", MmToTwips(template.MarginLeftMm ?? 0)),
                new XAttribute(W + "header", ReferenceHeaderFooterTwips),
                new XAttribute(W + "footer", ReferenceHeaderFooterTwips),
                new XAttribute(W + "gutter", "0")),
            new XElement(W + "cols", new XAttribute(W + "space", ReferenceColumnSpaceTwips)),
            new XElement(W + "docGrid", new XAttribute(W + "linePitch", ReferenceLinePitchTwips)));
    }

    private static XDocument CreateContentTypesDocument()
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                ContentTypes + "Types",
                new XElement(
                    ContentTypes + "Default",
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(
                    ContentTypes + "Default",
                    new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(
                    ContentTypes + "Default",
                    new XAttribute("Extension", "png"),
                    new XAttribute("ContentType", "image/png")),
                new XElement(
                    ContentTypes + "Override",
                    new XAttribute("PartName", "/word/document.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")),
                new XElement(
                    ContentTypes + "Override",
                    new XAttribute("PartName", "/word/styles.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")),
                new XElement(
                    ContentTypes + "Override",
                    new XAttribute("PartName", "/docProps/core.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.core-properties+xml")),
                new XElement(
                    ContentTypes + "Override",
                    new XAttribute("PartName", "/docProps/app.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.extended-properties+xml"))));
    }

    private static XDocument CreatePackageRelationshipsDocument()
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                Relationships + "Relationships",
                new XElement(
                    Relationships + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "word/document.xml")),
                new XElement(
                    Relationships + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"),
                    new XAttribute("Target", "docProps/core.xml")),
                new XElement(
                    Relationships + "Relationship",
                    new XAttribute("Id", "rId3"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"),
                    new XAttribute("Target", "docProps/app.xml"))));
    }

    private static XDocument CreateDocumentRelationshipsDocument()
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                Relationships + "Relationships",
                new XElement(
                    Relationships + "Relationship",
                    new XAttribute("Id", ImageRelationshipId),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"),
                    new XAttribute("Target", "media/image1.png")),
                new XElement(
                    Relationships + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                    new XAttribute("Target", "styles.xml"))));
    }

    private static XDocument CreateStylesDocument(CoverPageTemplate template)
    {
        var fontFamily = template.FontFamily ?? CoverContentBuilder.DefaultFontFamily;
        var logoSizePt = template.Styles.TryGetValue(CoverSection.Logo, out var logoStyle)
            ? logoStyle.FontSizePt ?? CoverContentBuilder.DefaultFontSizePt
            : CoverContentBuilder.DefaultFontSizePt;

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                W + "styles",
                new XAttribute(XNamespace.Xmlns + "w", W),
                new XElement(
                    W + "docDefaults",
                    new XElement(
                        W + "rPrDefault",
                        new XElement(
                            W + "rPr",
                            new XElement(
                                W + "rFonts",
                                new XAttribute(W + "ascii", fontFamily),
                                new XAttribute(W + "hAnsi", fontFamily),
                                new XAttribute(W + "cs", fontFamily)),
                            new XElement(W + "kern", new XAttribute(W + "val", "0")),
                            new XElement(W + "sz", new XAttribute(W + "val", HalfPoints(logoSizePt))),
                            new XElement(W + "szCs", new XAttribute(W + "val", HalfPoints(logoSizePt))))),
                    new XElement(
                        W + "pPrDefault",
                        new XElement(
                            W + "pPr",
                            new XElement(
                                W + "spacing",
                                new XAttribute(W + "before", "0"),
                                new XAttribute(W + "after", "0"),
                                new XAttribute(W + "line", LineSpacingHundredths),
                                new XAttribute(W + "lineRule", "auto"))))),
                new XElement(
                    W + "style",
                    new XAttribute(W + "type", "paragraph"),
                    new XAttribute(W + "default", "1"),
                    new XAttribute(W + "styleId", "Normal"),
                    new XElement(W + "name", new XAttribute(W + "val", "Normal")),
                    new XElement(W + "qFormat"))));
    }

    private static XDocument CreateCorePropertiesDocument(CoverPage coverPage)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                CoreProperties + "coreProperties",
                new XAttribute(XNamespace.Xmlns + "cp", CoreProperties),
                new XAttribute(XNamespace.Xmlns + "dc", DublinCore),
                new XAttribute(XNamespace.Xmlns + "dcterms", DublinTerms),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XElement(DublinCore + "title", coverPage.DocumentTitle),
                new XElement(DublinCore + "creator", "UCoverCraft"),
                new XElement(CoreProperties + "lastModifiedBy", "UCoverCraft"),
                new XElement(DublinTerms + "created", new XAttribute(Xsi + "type", "dcterms:W3CDTF"), timestamp),
                new XElement(DublinTerms + "modified", new XAttribute(Xsi + "type", "dcterms:W3CDTF"), timestamp)));
    }

    private static XDocument CreateAppPropertiesDocument()
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                ExtendedProperties + "Properties",
                new XElement(ExtendedProperties + "Application", "UCoverCraft")));
    }

    private static SectionStyle ResolveStyle(CoverPageTemplate template, CoverSection section)
    {
        return template.Styles.TryGetValue(section, out var style) ? style : new SectionStyle();
    }

    private static void WriteXmlPart(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
            });
        document.Save(writer);
    }

    private static void WriteBinaryPart(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static byte[] OpenLogo()
    {
        using var resource = typeof(CoverPageRenderer).Assembly.GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException($"Logo resource '{LogoResourceName}' was not found.");

        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void EnsureOutputDirectoryExists(string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static int MmToTwips(double millimetres) =>
        (int)Math.Round(millimetres * TwipsPerInch / 25.4, MidpointRounding.AwayFromZero);

    private static long MmToEmu(double millimetres) =>
        (long)Math.Round(millimetres * EmuPerMillimetre, MidpointRounding.AwayFromZero);

    private static int HalfPoints(double points) =>
        (int)Math.Round(points * HalfPointsPerPoint, MidpointRounding.AwayFromZero);
}
