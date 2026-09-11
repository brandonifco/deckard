using Deckard.Core.Randomness;

namespace Deckard.Rules.Resolution;

/// <summary>
/// Which side of an <see cref="OpposedTest"/> won.
/// SR6 Core / Game Concepts / Tests / Opposed Tests / printed p. 35-36 / PDF p. 36-37.
/// </summary>
public enum OpposedTestWinner
{
    /// <summary>The acting side -- the one written first in the test, e.g. "Stealth + Agility" of "Stealth + Agility vs. Perception + Intuition".</summary>
    Actor,

    /// <summary>The resisting side -- written second, e.g. "Perception + Intuition" above.</summary>
    Defender,
}

/// <summary>
/// SR6 Core / Game Concepts / Tests / Opposed Tests / printed p. 35-36 / PDF p. 36-37: two
/// parties roll dice pools and compare hits. The higher hit count wins; the difference
/// between the two hit counts is net hits. Ties go to the aggressor -- modeled here as the
/// acting side, matching the book's own "Acting player's skill and attribute" /
/// "Defending player's skill and attribute" captions for the two halves of an Opposed
/// test's written notation.
/// </summary>
public static class OpposedTest
{
    /// <summary>
    /// Rolls both sides' dice pools and resolves the Opposed test between them. The actor
    /// rolls first, then the defender -- an explicit, fixed draw order (ADR 0006), chosen
    /// to match the order the book itself writes an Opposed test in
    /// ("Stealth + Agility vs. Perception + Intuition", acting side first).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="actorPool"/> or <paramref name="defenderPool"/> is negative.
    /// </exception>
    public static OpposedTestResult Resolve(IRandomSource source, int actorPool, int defenderPool)
    {
        ArgumentNullException.ThrowIfNull(source);

        DicePoolRoll actor = DicePoolRoll.Roll(source, actorPool);
        DicePoolRoll defender = DicePoolRoll.Roll(source, defenderPool);

        bool tied = actor.Hits == defender.Hits;
        // The book gives a tie to the aggressor by default (printed p. 35 / PDF p. 36,
        // modeled here as the acting side): the acting side wins outright on a tie, not
        // just "counts as not losing", and NetHits is 0 in that case. The book also
        // notes that default can be overridden when a specific effect needs net hits to
        // trigger -- a downstream concern this Issue's Non-goals exclude (no
        // combat/damage effects). A tie already reports 0 net hits, so nothing here
        // needs to model that override directly.
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

    /// <summary>Which side won. Equal hits resolve to <see cref="OpposedTestWinner.Actor"/>.</summary>
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
