using UCoverCraft.Core.Templates;

namespace UCoverCraft.App.ViewModels;

public sealed class CoverPreviewLine
{
    public CoverPreviewLine(IReadOnlyList<CoverTextRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);

        Runs = runs.Select(run => new CoverPreviewRun(run)).ToList();
    }

    public IReadOnlyList<CoverPreviewRun> Runs { get; }
}
