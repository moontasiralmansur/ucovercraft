namespace UCoverCraft.Core.Templates;

public sealed record CoverSectionContent(
    CoverSection Section,
    IReadOnlyList<IReadOnlyList<CoverTextRun>> Lines,
    double? SpacingAfterMm);
