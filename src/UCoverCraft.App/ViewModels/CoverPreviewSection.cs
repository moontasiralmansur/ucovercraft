using System.Windows;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.App.ViewModels;

public sealed class CoverPreviewSection
{
    public CoverPreviewSection(CoverSectionContent content, double logoWidth, double logoHeight, double logoTopOffset)
    {
        ArgumentNullException.ThrowIfNull(content);

        Section = content.Section;
        IsLogo = content.Section == CoverSection.Logo;
        Margin = new Thickness(0, 0, 0, PreviewUnits.FromMillimetres(content.SpacingAfterMm ?? 0));
        LogoWidth = logoWidth;
        LogoHeight = logoHeight;
        LogoMargin = new Thickness(0, IsLogo ? logoTopOffset : 0, 0, 0);
        Lines = content.Lines.Select(line => new CoverPreviewLine(line)).ToList();
    }

    public CoverSection Section { get; }

    public bool IsLogo { get; }

    public Thickness Margin { get; }

    public double LogoWidth { get; }

    public double LogoHeight { get; }

    public Thickness LogoMargin { get; }

    public IReadOnlyList<CoverPreviewLine> Lines { get; }
}
