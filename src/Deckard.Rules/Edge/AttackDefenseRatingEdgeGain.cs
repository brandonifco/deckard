using System;

namespace Deckard.Rules.Edge;

/// <summary>
/// Which side, if either, an Attack/Defense Rating comparison awards a bonus Edge point
/// to. Never a value a resolved <see cref="AttackDefenseRatingEdgeGain.Evaluate"/> call
/// returns by omission -- <see cref="Neither"/> is a genuine resolved outcome here (the
/// margin was not met), not merely a zero-initialized placeholder, unlike
/// <c>OpposedTestWinner.Undetermined</c> or <c>GlitchSeverity.None</c> elsewhere in this
/// assembly.
/// </summary>
public enum AttackDefenseEdgeWinner
{
    /// <summary>Neither rating exceeded the other by <see cref="AttackDefenseRatingEdgeGain.GainMargin"/>.</summary>
    Neither = 0,

    /// <summary>The Attack Rating exceeded the Defense Rating by the margin.</summary>
    Attacker,

    /// <summary>The Defense Rating exceeded the Attack Rating by the margin.</summary>
    Defender,
}

/// <summary>
/// SR6 Core / Game Concepts / Edge / Gaining Edge / printed p. 45 / PDF p. 46: "At the
/// beginning of an Attack or hack action, compare the Attack Rating and Defense Rating of
/// the opponents (or, if there are multiple targets, the highest of the combatants). If
/// either is 4 or more greater than the other, that player gets a point of Edge. If the
/// attack is area-effect or attacking multiple targets, compare to the highest Defense
/// Rating among them."
///
/// This models only the comparison-to-gain formula, not the Combat-phase machinery that
/// produces an Attack Rating or a Defense Rating in the first place -- actions,
/// initiative, weapons, armor, or picking "the highest of the combatants" out of multiple
/// targets. All of that is explicitly out of this Issue's scope (its Non-goals: "No
/// initiative, no combat... model the *gain* rule, not the combat machinery that triggers
/// it"). Both ratings are accepted as plain integers the caller has already resolved down
/// to a single comparison -- including, per the quoted "multiple targets" and
/// "area-effect" sentences, already picking the highest Defense Rating among several --
/// exactly the posture <c>SimpleTest.Resolve(source, dicePool, threshold)</c> takes
/// toward its own int parameters (Issue #4), and <see cref="Attributes.ConditionMonitor"/>
/// takes toward attribute ranks (Issue #64).
///
/// "That player gets a point of Edge" (singular) is the same unit <see cref="EdgePool.GainBonusPoint"/>
/// grants; a caller that resolves <see cref="Winner"/> other than <see cref="AttackDefenseEdgeWinner.Neither"/>
/// is expected to call <c>GainBonusPoint()</c> on the winning side's <see cref="EdgePool"/>
/// itself -- this type does not hold or mutate a pool, only decides who, if anyone, earned
/// the point.
/// </summary>
public static class AttackDefenseRatingEdgeGain
{
    /// <summary>
    /// "If either is 4 or more greater than the other" -- SR6 Core / Game Concepts / Edge
    /// / Gaining Edge / printed p. 45 / PDF p. 46.
    /// </summary>
    public const int GainMargin = 4;

    /// <summary>
    /// Compares <paramref name="attackRating"/> and <paramref name="defenseRating"/> and
    /// decides which side, if either, gains a bonus point of Edge. "That player" (the one
    /// whose rating turned out to be the greater one) is the side awarded the point --
    /// either side of the comparison can be the one that wins it, per "if either is 4 or
    /// more greater than the other".
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attackRating"/> or <paramref name="defenseRating"/> is negative.
    /// Neither rating's valid range or upper bound is stated anywhere in this packet --
    /// they are produced by Combat-phase machinery this Issue does not build -- so, as
    /// with <see cref="Attributes.ConditionMonitor"/>'s attribute ranks, only a negative
    /// value (which has no reading in the source at all) is rejected as invalid input.
    /// </exception>
    public static AttackDefenseEdgeWinner Evaluate(int attackRating, int defenseRating)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attackRating);
        ArgumentOutOfRangeException.ThrowIfNegative(defenseRating);

        if (attackRating - defenseRating >= GainMargin)
        {
            return AttackDefenseEdgeWinner.Attacker;
        }

        if (defenseRating - attackRating >= GainMargin)
        {
            return AttackDefenseEdgeWinner.Defender;
        }

        return AttackDefenseEdgeWinner.Neither;
    }
}
