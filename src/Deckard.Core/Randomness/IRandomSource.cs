namespace Deckard.Core.Randomness;

/// <summary>
/// The single deterministic randomness primitive every SR6 mechanic ultimately draws
/// through. Deliberately one member: ADR 0002 layers the d6 mapping, dice pools, and
/// test resolution on top of this as separate, separately-testable types, rather than
/// folding a distribution or a bound into the primitive itself.
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Draws the next raw 32-bit value. No bound, no distribution, no rejection
    /// sampling -- those decisions belong to whatever layer consumes this value, because
    /// a mixed-in bias-correction rule here would be untestable in isolation from the
    /// generator itself.
    /// </summary>
    uint NextUInt32();
}
