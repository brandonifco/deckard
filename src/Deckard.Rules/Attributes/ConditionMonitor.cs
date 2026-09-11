using System;

namespace Deckard.Rules.Attributes;

/// <summary>
/// Derives the box count of a character's two Condition Monitor tracks from the attribute
/// each is keyed to. SR6 Core / Game Concepts / Character Traits / Condition Monitors /
/// printed p. 38 / PDF p. 39: "Your Physical Condition Monitor has (Body/2, rounded up) + 8
/// boxes, while the Stun Condition Monitor has (Willpower/2, rounded up) + 8 boxes." Both
/// tracks share one formula, differing only in which attribute rank feeds it.
///
/// This is a computation over <c>Deckard.Data.Attributes</c> vocabulary, so it belongs in
/// <c>Deckard.Rules</c> rather than <c>Deckard.Data</c> per docs/architecture.md's layering
/// ("Data" is structured values; "Rules" is "Shadowrun-specific mechanics ... attributes and
/// skills"). It derives only a track's *capacity* -- Issue #64 is explicit that filling,
/// clearing, the per-row -1 dice pool penalty, unconsciousness, and Stun-overflow-to-Physical
/// (all printed on the same page) are Phase 7 (damage application and healing), not this
/// Issue.
///
/// Attribute rank's own valid range is not encoded here. Printed p. 37 points to "the
/// Character Creation chapter (p. 58)" for "the ranges for Attribute ranks", and that page
/// is both outside this Issue's source packet (pp. 37-38 only) and outside its scope (Issue
/// #64's non-goals: "No character creation, priorities, metatypes, or point costs"). Only a
/// negative rank is rejected -- ordinary argument validation with no reading in the source
/// at all, the same posture <c>SimpleTest.Resolve</c> (Issue #4) takes for a negative
/// threshold. There is deliberately no encoded upper bound.
/// </summary>
public static class ConditionMonitor
{
    private const int BaseBoxes = 8;

    /// <summary>
    /// Boxes in a character's Physical Condition Monitor: (<paramref name="bodyRank"/> / 2,
    /// rounded up) + 8. SR6 Core / Game Concepts / Character Traits / Condition Monitors /
    /// printed p. 38 / PDF p. 39.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bodyRank"/> is negative.</exception>
    public static int PhysicalBoxes(int bodyRank) => BoxesFor(bodyRank);

    /// <summary>
    /// Boxes in a character's Stun Condition Monitor: (<paramref name="willpowerRank"/> / 2,
    /// rounded up) + 8. SR6 Core / Game Concepts / Character Traits / Condition Monitors /
    /// printed p. 38 / PDF p. 39.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="willpowerRank"/> is negative.</exception>
    public static int StunBoxes(int willpowerRank) => BoxesFor(willpowerRank);

    private static int BoxesFor(int attributeRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attributeRank);

        // "Rounded up" (printed p. 38) is ceiling division, computed with integer
        // arithmetic only -- per docs/architecture.md's ban on floating point for discrete
        // rules -- as (n + 1) / 2 under C#'s truncating integer division. That expression
        // is exact for every non-negative n, and the two residues mod 2 are the whole
        // domain the rounding rule can ever distinguish:
        //   n even (n = 2k): (2k + 1) / 2 truncates to k, i.e. n / 2 exactly -- there is
        //     nothing to round, and this expression rounds nothing.
        //   n odd  (n = 2k + 1): (2k + 2) / 2 == k + 1, one more than the k that plain
        //     truncating division (n / 2) would give -- this is the "rounded up" the book
        //     states, and the entire reason this is not just `attributeRank / 2`.
        // Both residues, at both a low boundary pair (0/1) and every following pair, are
        // enumerated exhaustively in ConditionMonitorTests rather than sampled.
        int roundedHalf = (attributeRank + 1) / 2;
        return roundedHalf + BaseBoxes;
    }
}
