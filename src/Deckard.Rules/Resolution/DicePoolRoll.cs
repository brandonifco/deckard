using Deckard.Core.Dice;
using Deckard.Core.Randomness;

namespace Deckard.Rules.Resolution;

/// <summary>
/// One rolled and counted SR6 dice pool: the dice as rolled, in draw order, and the two
/// counts every SR6 test is built from -- hits and ones -- plus the glitch severity those
/// counts determine. This is the shared building block <see cref="SimpleTest"/> and
/// <see cref="OpposedTest"/> both resolve against; an Opposed test rolls two of these.
///
/// SR6 Core / Game Concepts / Tests / printed p. 35 / PDF p. 36: hits are the dice that
/// come up 5 or 6; ones are counted separately to determine glitches. Glitch severity:
/// SR6 Core / Game Concepts / Glitches and Critical Glitches / printed p. 44 / PDF p. 45.
///
/// <b>Glitch is scoped per roll here, not per test (Issue #61).</b> The book states both
/// glitch conditions in terms of the test, not the roll: a glitch is "more than half of
/// the dice that you roll on a <i>test</i>" coming up 1s, and a critical glitch is a
/// glitch rolled "without a single hit on the <i>test</i>" (SR6 Core / Game Concepts /
/// Glitches and Critical Glitches / printed p. 44 / PDF p. 45; emphasis on the scoping
/// phrase "on a test"). <see cref="Glitch"/> is instead derived from exactly the dice one
/// <see cref="Roll"/> call drew. That is the same thing as the book's test-level severity
/// only when the test itself consists of a single roll -- which is true for
/// <see cref="SimpleTest"/> and <see cref="OpposedTest"/> (SR6 Core / Game Concepts /
/// Tests / Simple Tests, Opposed Tests / printed pp. 35-36 / PDF pp. 36-37): both resolve
/// in exactly one <see cref="Roll"/>, so roll and test coincide and this field already is
/// the book's rule.
///
/// It stops being equivalent the moment a test spans more than one roll. Two variants the
/// book prints on the same pages do exactly that (SR6 Core / Game Concepts / Tests /
/// Extended Tests, Teamwork Tests / printed p. 36 / PDF p. 37): an Extended test
/// accumulates hits across repeated rolls of a dice pool that shrinks by one die each
/// time, so a test-level glitch has to be judged against ones and dice totaled over every
/// roll made so far, not one roll's own count; a Teamwork test's dice span a leader's roll
/// plus one or more helpers' rolls, so "the test" is not any single roll either. Neither
/// is implemented yet -- <see cref="ExtendedTest"/> and <see cref="TeamworkTest"/> both
/// refuse explicitly rather than guess -- but whichever Issue implements one must compute
/// that test-level severity from the test's own accumulated dice. Reusing a single
/// <see cref="DicePoolRoll.Glitch"/> for a multi-roll test would judge the wrong dice
/// against both conditions above.
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
    /// the exact rule. A <em>per-roll</em> severity: it equals the book's test-level
    /// severity (SR6 Core / Game Concepts / Glitches and Critical Glitches / printed
    /// p. 44 / PDF p. 45) only for a test that consists of a single roll, which is every
    /// test type implemented today. A test spanning more than one roll -- Extended,
    /// Teamwork -- must compute its own severity from ones and hits accumulated across
    /// all of the test's rolls rather than reuse this field; see the class remarks above.
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
