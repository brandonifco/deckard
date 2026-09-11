namespace Deckard.Core.Replay;

/// <summary>
/// Carries a source manifest's <c>sourceId</c> and <c>sha256</c> (from
/// <c>.github/source-manifest.json</c>) into a replay identity. Both values passed in,
/// never read: <c>Deckard.Core</c> touches no filesystem (ADR 0001; see
/// docs/architecture.md), so this type has no knowledge of the manifest's shape or
/// location -- the caller resolves the manifest and hands both identifiers across that
/// boundary. See ADR 0005.
///
/// Both fields are required, not just <c>sourceId</c>: a source-baseline re-pin edits the
/// <c>sha256</c> field of the existing manifest entry in place and leaves <c>sourceId</c>
/// unchanged (ADR 0003; <c>.github/source-manifest.json</c>'s own comment reads "Changing
/// a sha256 here is a deliberate source-baseline change and requires its own Issue, its
/// own PR, and an ADR"). An identifier carrying only <c>sourceId</c> would compare equal
/// across exactly the event it exists to detect -- see Issue #38's corrected "Known
/// ambiguity" section and ADR 0005.
/// </summary>
public readonly record struct SourceBaselineId
{
    /// <summary>The manifest's <c>sourceId</c>, e.g. <c>"sr6-core"</c>.</summary>
    public string SourceId { get; }

    /// <summary>
    /// The manifest's <c>sha256</c> for this source, verbatim. This is the field a
    /// source-baseline re-pin actually changes -- see the type doc.
    /// </summary>
    public string Sha256 { get; }

    /// <exception cref="ArgumentException">
    /// <paramref name="sourceId"/> or <paramref name="sha256"/> is null, empty, or
    /// whitespace.
    /// </exception>
    public SourceBaselineId(string sourceId, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        SourceId = sourceId;
        Sha256 = sha256;
    }
}
