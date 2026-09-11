using Deckard.Rules.Resolution;

namespace Deckard.Rules.Edge;

/// <summary>
/// Burning Edge (SR6 Core / Game Concepts / Edge / Burning Edge / printed p. 48 / PDF
/// p. 49) names exactly two uses, introduced by the book as a closed list. Each is its
/// own type here rather than a case inside <see cref="EdgePool.Burn"/>, because
/// <see cref="EdgePool.Burn"/> is only the pool-and-rank *cost* of burning -- what the
/// burn actually *does* is a distinct effect this Issue's pool economy does not build the
/// machinery for: automatic-success test resolution and a "capable of performing" skill
/// check for <see cref="Smackdown"/>, imminent-death/damage state for
/// <see cref="NotDeadYet"/>. Both follow the <c>ExtendedTest</c>/<c>TeamworkTest</c>
/// pattern (Issue #4): an explicit, named, enumerable ADR 0004 <see cref="UnresolvedReason.UnsupportedRule"/>
/// result instead of silently having no way to ask about either effect at all.
/// </summary>
public static class Smackdown
{
    /// <summary>
    /// Smackdown: an automatic success with four net hits on a test the character is
    /// capable of performing. SR6 Core / Game Concepts / Edge / Burning Edge / printed
    /// p. 48 / PDF p. 49. The "capable of performing" precondition alone needs machinery
    /// this Issue does not build (a character's skill/spellcasting capability), so this
    /// is not approximated as an unconditional automatic success.
    /// </summary>
    public static UnresolvedTestResult Resolve() =>
        new(
            UnresolvedReason.UnsupportedRule,
            attempted: "Smackdown (a use of Burning Edge)",
            sourceLocator: "SR6 Core / Game Concepts / Edge / Burning Edge / printed p. 48 / PDF p. 49");
}

/// <summary>
/// Not Dead Yet: burning a point of Edge lets a character survive an otherwise-killing
/// blow. SR6 Core / Game Concepts / Edge / Burning Edge / printed p. 48 / PDF p. 49. This
/// needs damage/death-state machinery this Issue does not build.
/// </summary>
public static class NotDeadYet
{
    public static UnresolvedTestResult Resolve() =>
        new(
            UnresolvedReason.UnsupportedRule,
            attempted: "Not Dead Yet (a use of Burning Edge)",
            sourceLocator: "SR6 Core / Game Concepts / Edge / Burning Edge / printed p. 48 / PDF p. 49");
}
