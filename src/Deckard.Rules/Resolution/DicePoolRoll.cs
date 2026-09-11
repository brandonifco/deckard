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
    /// <see cref="Hits"/>, and the pool size; see <see cref="Roll"/> for the exact rule.
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
