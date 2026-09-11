using Deckard.Core.Dice;
using Deckard.Core.Randomness;

namespace Deckard.Rules.Resolution;

/// <summary>
/// One rolled and counted SR6 dice pool: the dice as rolled, in draw order, and the two
/// counts every SR6 test is built from -- hits and ones -- plus the glitch severity those
/// counts determine. This is the shared building block <see cref="SimpleTest"/> and
/// <see cref="OpposedTest"/> both resolve against; an Opposed test rolls two of these, one
/// per side.
///
/// SR6 Core / Game Concepts / Tests / printed p. 35 / PDF p. 36: hits are the dice that
/// come up 5 or 6; ones are counted separately to determine glitches. Glitch severity:
/// SR6 Core / Game Concepts / Glitches and Critical Glitches / printed p. 44 / PDF p. 45.
///
/// <b>Glitch is scoped per roll here, not per test (Issue #61).</b> Printed p. 44 / PDF
/// p. 45 scopes both glitch conditions to the test -- "on a test" for the glitch trigger,
/// "on the test" for critical glitch, two different phrases for the same scope.
/// <see cref="Glitch"/> is instead derived from exactly the dice one <see cref="Roll"/>
/// call drew.
///
/// For a test that is a single roll, that is not actually a discrepancy: a roll's dice are
/// the whole test's dice, so this field already is the book's rule. That covers
/// <see cref="SimpleTest"/> outright, and covers <see cref="OpposedTest"/> per side -- an
/// Opposed test is two rolls, one per party (printed p. 35 / PDF p. 36), so each side's own
/// <see cref="Glitch"/> already is that side's test-level severity; the two sides are never
/// combined into one severity for the test as a whole.
///
/// It stops being that simple once a single party's test spans more than one roll, which the
/// book's own Extended and Teamwork variants do (SR6 Core / Game Concepts / Tests / Extended
/// Tests, Teamwork Tests / printed p. 36 / PDF p. 37) -- neither implemented yet;
/// <see cref="ExtendedTest"/> and <see cref="TeamworkTest"/> both refuse explicitly rather
/// than guess. The book itself treats a glitch inside that kind of structure as a per-roll
/// event: a Teamwork helper's own glitch carries its own distinct consequence, separate from
/// the leader's roll (same pages). Printed p. 35 / PDF p. 36, meanwhile, states the plain
/// glitch trigger with no test qualifier at all. So only p. 44 actually scopes a glitch
/// condition to the test; p. 35 does not scope it, and p. 36's helper-glitch line
/// presupposes the condition rather than stating or scoping it. That inconsistency, for a
/// test spanning more than one roll, was a genuine source ambiguity that Brandon has since
/// settled as a deliberate interpretation: ADR 0012
/// (docs/decisions/0012-multi-roll-glitch-scoping-hybrid.md). Per that ADR, whether a roll
/// glitches stays scoped to that roll's own dice, consistent with the helper-glitch
/// treatment above and unchanged from today's rule -- but whether a glitch is critical is
/// scoped to the whole test's hits accumulated across every roll made so far, not the
/// glitching roll's own hits alone. A single <see cref="DicePoolRoll.Glitch"/> cannot
/// express that test-level half once more than one roll is in play: whichever Issue
/// implements Extended or Teamwork must compute criticality per ADR 0012, not by reusing one
/// roll's own <see cref="Glitch"/> value. (ADR 0012 also records, rather than invents, open
/// questions specific to Teamwork -- exactly how a helper's roll feeds the test's
/// accumulated hits, and what a leader-less Teamwork test means for criticality -- left for
/// whichever Issue implements it.)
///
/// (Buying Hits, printed p. 36 / PDF p. 37, resolves a test with no roll of any dice at
/// all, so neither glitch condition has anything to apply to there -- outside this remark's
/// one-roll/many-rolls framing entirely, not a third case of it.)
///
/// Built only by <see cref="Roll"/>, directly on top of
/// <see cref="D6.Roll(IRandomSource, int)"/> (Issue #3) -- this type adds no face
/// generation of its own. <see cref="DiceRolled"/> preserves D6's own draw-order
/// guarantee: it is the sequence exactly as rolled, never sorted or grouped, per ADR 0006
/// (docs/decisions/0006-deterministic-ordering-conventions.md).
/// </summary>
public sealed class DicePoolRoll
{
    /// <summary>The dice as rolled, in the order they were drawn.</summary>
    public IReadOnlyList<int> DiceRolled { get; }

    /// <summary>Count of dice showing 5 or 6.</summary>
    public int Hits { get; }

    /// <summary>Count of dice showing 1.</summary>
    public int Ones { get; }

    /// <summary>
    /// None, Glitch, or CriticalGlitch -- derived entirely from <see cref="Ones"/>,
    /// <see cref="Hits"/>, and the pool size of this one roll; see <see cref="Roll"/> for
    /// the exact rule. A <em>per-roll</em> value: for a test that consists of one roll --
    /// <see cref="SimpleTest"/> outright, and each side of <see cref="OpposedTest"/>, since
    /// each side makes exactly one roll -- this already is the book's test-level severity
    /// (SR6 Core / Game Concepts / Glitches and Critical Glitches / printed p. 44 /
    /// PDF p. 45). For a test spanning more than one roll, only the glitch trigger stays
    /// scoped to this one roll; the test's criticality does not, and must be computed
    /// separately per ADR 0012 (docs/decisions/0012-multi-roll-glitch-scoping-hybrid.md)
    /// rather than read off this property. See the class remarks above for the full rule.
    /// </summary>
    public GlitchSeverity Glitch { get; }

    private DicePoolRoll(IReadOnlyList<int> diceRolled, int hits, int ones, GlitchSeverity glitch)
    {
        DiceRolled = diceRolled;
        Hits = hits;
        Ones = ones;
        Glitch = glitch;
    }

    /// <summary>
    /// Rolls <paramref name="dicePool"/> dice via <see cref="D6.Roll(IRandomSource, int)"/>
    /// and derives hits, ones, and glitch severity from the result.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dicePool"/> is negative.</exception>
    public static DicePoolRoll Roll(IRandomSource source, int dicePool)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(dicePool);

        IReadOnlyList<int> dice = D6.Roll(source, dicePool);

        int hits = 0;
        int ones = 0;
        foreach (int face in dice)
        {
            if (face == 5 || face == 6)
            {
                hits++;
            }
            else if (face == 1)
            {
                ones++;
            }
        }

        // "More than half" (printed p. 44) is a strict majority. Checked as
        // `ones * 2 > dice.Count` rather than `ones > dice.Count / 2.0`: integer
        // arithmetic only, per docs/architecture.md's ban on floating point for discrete
        // rules, and it is exact for both even and odd pool sizes. An exact half --
        // e.g. 2 ones out of 4 dice -- is deliberately NOT a glitch; that is the precise
        // boundary the Issue's required evidence calls for pinning on both sides.
        bool isGlitch = ones * 2 > dice.Count;
        GlitchSeverity glitch = !isGlitch
            ? GlitchSeverity.None
            : hits == 0
                ? GlitchSeverity.CriticalGlitch
                : GlitchSeverity.Glitch;

        return new DicePoolRoll(dice, hits, ones, glitch);
    }
}
