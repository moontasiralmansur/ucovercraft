using UCoverCraft.Core.Templates;

namespace UCoverCraft.App.ViewModels;

internal static class PreviewUnits
{
    private const double DipPerInch = 96d;
    private const double MillimetresPerInch = 25.4d;

    public static double FromMillimetres(double millimetres) => millimetres * DipPerInch / MillimetresPerInch;

    public static double FromPoints(double points) => points * DipPerInch / 72d;

    public static double LineHeight(double fontSizePt) => FromPoints(CoverPageLayout.LineHeightPt(fontSizePt));
}
