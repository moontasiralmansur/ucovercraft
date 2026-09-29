using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Pdf;

public static class CoverPageRenderer
{
    private const string LogoResourceName = "UCoverCraft.Pdf.ulab-logo.png";

    static CoverPageRenderer()
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

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

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromMillimeter(CoverPageTemplate.A4WidthMm);
        page.Height = XUnit.FromMillimeter(CoverPageTemplate.A4HeightMm);

        using var logoStream = OpenLogo();
        using var logo = XImage.FromStream(logoStream);
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var fonts = new FontCache(template.FontFamily ?? CoverContentBuilder.DefaultFontFamily);
            var pageWidth = MmToPt(CoverPageTemplate.A4WidthMm);
            var y = MmToPt(template.MarginTopMm ?? 0);

            foreach (var section in sections)
            {
                if (section.Section == CoverSection.Logo)
                {
                    y += MmToPt(template.LogoTopOffsetMm ?? 0);
                    y += DrawLogo(gfx, logo, template, y, pageWidth);
                }
                else
                {
                    foreach (var line in section.Lines)
                    {
                        DrawLine(gfx, fonts, line, y, pageWidth);
                        y += LineHeight(line);
                    }
                }

                if (section.SpacingAfterMm is { } gap)
                {
                    y += MmToPt(gap);
                }
            }
        }

        document.Save(outputPath);
    }

    private static double DrawLogo(XGraphics gfx, XImage logo, CoverPageTemplate template, double topY, double pageWidth)
    {
        if (template.LogoWidthMm is not { } widthMm || template.LogoHeightMm is not { } heightMm)
        {
            return 0;
        }

        var width = MmToPt(widthMm);
        var height = MmToPt(heightMm);
        gfx.DrawImage(logo, (pageWidth - width) / 2, topY, width, height);
        return height;
    }

    private static void DrawLine(
        XGraphics gfx,
        FontCache fonts,
        IReadOnlyList<CoverTextRun> line,
        double topY,
        double pageWidth)
    {
        var widths = new double[line.Count];
        var totalWidth = 0d;
        for (var i = 0; i < line.Count; i++)
        {
            widths[i] = MeasureWidth(gfx, fonts, line[i]);
            totalWidth += widths[i];
        }

        var x = (pageWidth - totalWidth) / 2;
        var lineHeight = LineHeight(line);
        var format = new XStringFormat
        {
            Alignment = XStringAlignment.Near,
            LineAlignment = XLineAlignment.Near,
        };

        for (var i = 0; i < line.Count; i++)
        {
            if (widths[i] > 0)
            {
                var run = line[i];
                var rect = new XRect(x, topY, widths[i] + 1, lineHeight);
                gfx.DrawString(run.Text, fonts.Get(run.FontSizePt, run.IsBold), XBrushes.Black, rect, format);
            }

            x += widths[i];
        }
    }

    private static double MeasureWidth(XGraphics gfx, FontCache fonts, CoverTextRun run)
    {
        return string.IsNullOrEmpty(run.Text) ? 0 : gfx.MeasureString(run.Text, fonts.Get(run.FontSizePt, run.IsBold)).Width;
    }

    private static double LineHeight(IReadOnlyList<CoverTextRun> line)
    {
        return CoverPageLayout.LineHeightPt(line.Max(run => run.FontSizePt));
    }

    private static MemoryStream OpenLogo()
    {
        using var resource = typeof(CoverPageRenderer).Assembly.GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException($"Logo resource '{LogoResourceName}' was not found.");

        var logo = new MemoryStream();
        resource.CopyTo(logo);
        logo.Position = 0;
        return logo;
    }

    private static void EnsureOutputDirectoryExists(string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static double MmToPt(double millimetres) => millimetres * 72 / 25.4;

    private sealed class FontCache(string familyName)
    {
        private readonly Dictionary<(double SizePt, bool IsBold), XFont> _fonts = [];

        public XFont Get(double sizePt, bool isBold)
        {
            var key = (sizePt, isBold);
            if (!_fonts.TryGetValue(key, out var font))
            {
                font = new XFont(familyName, sizePt, isBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                _fonts[key] = font;
            }

            return font;
        }
    }
}
