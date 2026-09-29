using UCoverCraft.Core.Templates;

namespace UCoverCraft.App.ViewModels;

public sealed class CoverPreviewRun
{
    public CoverPreviewRun(CoverTextRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        Text = run.Text;
        FontSize = PreviewUnits.FromPoints(run.FontSizePt);
        LineHeight = PreviewUnits.LineHeight(run.FontSizePt);
        IsBold = run.IsBold;
    }

    public string Text { get; }

    public double FontSize { get; }

    public double LineHeight { get; }

    public bool IsBold { get; }
}
