using System.Windows;
using System.Windows.Media;
using UCoverCraft.Core.Models;
using UCoverCraft.Core.Mvvm;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.App.ViewModels;

public sealed class CoverPreviewViewModel : ObservableObject
{
    private readonly CoverPageTemplate _template;
    private IReadOnlyList<CoverPreviewSection> _sections = [];

    public CoverPreviewViewModel()
        : this(CoverPageTemplate.Reference)
    {
    }

    public CoverPreviewViewModel(CoverPageTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        _template = template;
        PageWidth = PreviewUnits.FromMillimetres(CoverPageTemplate.A4WidthMm);
        PageHeight = PreviewUnits.FromMillimetres(CoverPageTemplate.A4HeightMm);
        PageMargin = new Thickness(
            PreviewUnits.FromMillimetres(template.MarginLeftMm ?? 0),
            PreviewUnits.FromMillimetres(template.MarginTopMm ?? 0),
            PreviewUnits.FromMillimetres(template.MarginRightMm ?? 0),
            PreviewUnits.FromMillimetres(template.MarginBottomMm ?? 0));
        LogoWidth = PreviewUnits.FromMillimetres(template.LogoWidthMm ?? 0);
        LogoHeight = PreviewUnits.FromMillimetres(template.LogoHeightMm ?? 0);
        LogoTopOffset = PreviewUnits.FromMillimetres(template.LogoTopOffsetMm ?? 0);
        FontFamily = new FontFamily(template.FontFamily ?? CoverContentBuilder.DefaultFontFamily);
    }

    public double PageWidth { get; }

    public double PageHeight { get; }

    public Thickness PageMargin { get; }

    public double LogoWidth { get; }

    public double LogoHeight { get; }

    public double LogoTopOffset { get; }

    public FontFamily FontFamily { get; }

    public IReadOnlyList<CoverPreviewSection> Sections
    {
        get => _sections;
        private set => SetProperty(ref _sections, value);
    }

    public void Update(CoverPage coverPage)
    {
        ArgumentNullException.ThrowIfNull(coverPage);

        var contents = CoverContentBuilder.Build(coverPage, _template);
        var sections = new List<CoverPreviewSection>(contents.Count);
        foreach (var content in contents)
        {
            sections.Add(new CoverPreviewSection(content, LogoWidth, LogoHeight, LogoTopOffset));
        }

        Sections = sections;
    }
}
