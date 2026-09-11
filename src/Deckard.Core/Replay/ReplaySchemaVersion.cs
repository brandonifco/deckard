namespace Deckard.Core.Replay;

/// <summary>
/// Versions the shape of a recorded replay -- its field layout, ordering, and contents --
/// independently of <see cref="RulesetVersion"/>, because the two change for unrelated
/// reasons: a schema revision (e.g. adding a diagnostic field to a recorded replay) can
/// happen with no mechanics change, and a mechanics change can happen with no schema
/// revision. Nothing in <c>Deckard.Core</c> reads or writes a replay in this shape yet --
/// serialization is explicitly out of scope for ADR 0005 -- this exists so the shape can be
/// named and compared once something does.
/// </summary>
public readonly record struct ReplaySchemaVersion
{
    /// <summary>The recorded-replay schema's revision number.</summary>
    public int Version { get; }

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is negative.</exception>
    public ReplaySchemaVersion(int version)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        Version = version;
    }
}
