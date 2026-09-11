namespace Deckard.Core.Replay;

/// <summary>
/// Carries a source manifest's <c>sourceId</c> (e.g. <c>"sr6-core"</c>, from
/// <c>.github/source-manifest.json</c>) into a replay identity. A value passed in, never
/// read: <c>Deckard.Core</c> touches no filesystem (ADR 0001; see docs/architecture.md), so
/// this type has no knowledge of the manifest's shape or location -- the caller resolves
/// the manifest and hands the identifier across that boundary. See ADR 0005.
/// </summary>
public readonly record struct SourceBaselineId
{
    /// <summary>The manifest's <c>sourceId</c>, e.g. <c>"sr6-core"</c>.</summary>
    public string SourceId { get; }

    /// <exception cref="ArgumentException">
    /// <paramref name="sourceId"/> is null, empty, or whitespace.
    /// </exception>
    public SourceBaselineId(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        SourceId = sourceId;
    }
}
