namespace Deckard.Core.Replay;

/// <summary>
/// Identifies which ruleset -- and which revision of its mechanics -- was in force when a
/// sequence of decisions was recorded. Distinct from <see cref="SourceBaselineId"/>: the
/// source baseline names the pinned rulebook Deckard encodes (ADR 0003), while this names
/// Deckard's own implementation of it, which revises independently as mechanics are added,
/// corrected, or reinterpreted. See ADR 0005.
///
/// <c>default(RulesetVersion)</c> bypasses the constructor below entirely and yields a
/// <see langword="null"/> <see cref="Id"/> paired with a valid-looking <c>Version</c> of
/// <c>0</c> rather than throwing -- see ADR 0005's Consequences for why nothing closes
/// that gap yet.
/// </summary>
public readonly record struct RulesetVersion
{
    /// <summary>
    /// Which ruleset this is. A single value today (Deckard implements one ruleset), kept
    /// distinct from <see cref="Version"/> so a future variant or house-rule ruleset can be
    /// distinguished from a revision of the same one.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// This ruleset's revision. Bumping it is routine engineering, expected whenever a
    /// change could alter how a recorded decision resolves -- see ADR 0005's "replay
    /// compatibility events" section for why a bump alone does not require its own ADR.
    /// </summary>
    public int Version { get; }

    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is negative.</exception>
    public RulesetVersion(string id, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        Id = id;
        Version = version;
    }
}
