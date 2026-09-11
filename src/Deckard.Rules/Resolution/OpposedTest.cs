using Deckard.Core.Randomness;

namespace Deckard.Rules.Resolution;

/// <summary>
/// Which side of an <see cref="OpposedTest"/> won.
/// </summary>
public enum OpposedTestWinner
{
    /// <summary>
    /// No winner has been determined. Never the value of a resolved
    /// <see cref="OpposedTestResult.Winner"/> -- exists only so an uninitialized
    /// (<see langword="default"/>) enum value reads as "not decided" rather than
    /// silently reading as a real outcome (CLAUDE.md's fail-visibly invariant).
    /// <see cref="GlitchSeverity.None"/> makes the same choice for the same reason.
    /// </summary>
    Undetermined = 0,

    /// <summary>
    /// The acting side -- the one written first in the test's notation, e.g. "Stealth +
    /// Agility" of "Stealth + Agility vs. Perception + Intuition" (printed p. 36 / PDF
    /// p. 37). Also the side a tie resolves to; see ADR 0007
    /// (docs/decisions/0007-opposed-test-tie-break-interpretation.md) for why that is
    /// Deckard's interpretation and not a sourced rule.
    /// </summary>
    Actor,

    /// <summary>The resisting side -- written second, e.g. "Perception + Intuition" above.</summary>
    Defender,
}

/// <summary>
/// SR6 Core / Game Concepts / Tests / Opposed Tests / printed p. 35 / PDF p. 36: two
/// parties roll dice pools and compare hits; the higher hit count wins; the difference
/// between the two hit counts is net hits. See ADR 0007 for how a tie (equal hits) is
/// resolved -- the book hedges that rule enough that Deckard's tie-break is documented
/// there as a deliberate interpretation, not restated here as settled fact.
/// </summary>
public static class OpposedTest
{
    /// <summary>
    /// Rolls both sides' dice pools and resolves the Opposed test between them. The actor
    /// rolls first, then the defender -- an explicit, fixed draw order (ADR 0006), chosen
    /// to match the order the book itself writes an Opposed test in ("Stealth + Agility
    /// vs. Perception + Intuition", printed p. 36 / PDF p. 37, acting side first).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="actorPool"/> or <paramref name="defenderPool"/> is negative. Both
    /// are validated before either side is rolled, so a rejected call never advances
    /// <paramref name="source"/> -- a failure must not leave the source in a state that
    /// depends on which argument failed.
    /// </exception>
    public static OpposedTestResult Resolve(IRandomSource source, int actorPool, int defenderPool)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(actorPool);
        ArgumentOutOfRangeException.ThrowIfNegative(defenderPool);

        DicePoolRoll actor = DicePoolRoll.Roll(source, actorPool);
        DicePoolRoll defender = DicePoolRoll.Roll(source, defenderPool);

        bool tied = actor.Hits == defender.Hits;
        // ADR 0007: ties resolve to the actor by deliberate interpretation, not because
        // the book names the actor as "the aggressor" -- it never defines that term. This
        // keeps Resolve total (every call produces a winner) at the cost of an outcome the
        // book does not itself pin down for the tied case.
        OpposedTestWinner winner = actor.Hits >= defender.Hits
            ? OpposedTestWinner.Actor
            : OpposedTestWinner.Defender;
        int netHits = Math.Abs(actor.Hits - defender.Hits);

        return new OpposedTestResult(actor, defender, winner, tied, netHits);
    }
}

/// <summary>The structured, explainable outcome of a resolved <see cref="OpposedTest"/>.</summary>
public sealed class OpposedTestResult
{
    /// <summary>The acting side's dice pool, with its hits, ones, and glitch severity.</summary>
    public DicePoolRoll Actor { get; }

    /// <summary>The resisting side's dice pool, with its hits, ones, and glitch severity.</summary>
    public DicePoolRoll Defender { get; }

    /// <summary>
    /// Which side won -- never <see cref="OpposedTestWinner.Undetermined"/> for a
    /// resolved result. Equal hits resolve to <see cref="OpposedTestWinner.Actor"/>
    /// per ADR 0007.
    /// </summary>
    public OpposedTestWinner Winner { get; }

    /// <summary><see langword="true"/> when both sides rolled the same number of hits.</summary>
    public bool Tied { get; }

    /// <summary>The absolute difference between the two sides' hit counts.</summary>
    public int NetHits { get; }

    internal OpposedTestResult(
        DicePoolRoll actor,
        DicePoolRoll defender,
        OpposedTestWinner winner,
        bool tied,
        int netHits)
    {
        Actor = actor;
        Defender = defender;
        Winner = winner;
        Tied = tied;
        NetHits = netHits;
    }
}
