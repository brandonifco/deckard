using System;
using Deckard.Rules.Attributes;

namespace Deckard.Rules.Tests.Attributes;

/// <summary>
/// Pins <see cref="ConditionMonitor"/> against SR6 Core / Game Concepts / Character Traits /
/// Condition Monitors / printed p. 38 / PDF p. 39: "(attribute/2, rounded up) + 8" boxes.
///
/// The book's own cross-reference for the *valid range* of an attribute rank points to the
/// Character Creation chapter (printed p. 58), which is outside this Issue's source packet
/// (pp. 37-38) and outside its scope (non-goals: no character creation). No such upper
/// bound is encoded or assumed by <see cref="ConditionMonitor"/>, so "the whole printed
/// domain" this Issue's required evidence asks for is verified here as: every non-negative
/// integer, exhaustively, up to a bound (200) chosen only to be far larger than any
/// attribute rank SR6 Core could plausibly assign -- not sampled, and not derived from an
/// assumed cap this packet does not state.
/// </summary>
public sealed class ConditionMonitorTests
{
    private const int GenerousUpperBound = 200;

    // ------------------------------------------------------------------- argument validation

    [Fact]
    public void PhysicalBoxes_throws_on_negative_body_rank()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConditionMonitor.PhysicalBoxes(-1));
    }

    [Fact]
    public void StunBoxes_throws_on_negative_willpower_rank()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConditionMonitor.StunBoxes(-1));
    }

    // ------------------------------------------------------------------------- both sides
    // of the rounding boundary, pinned explicitly (Issue #64's required evidence).

    [Theory]
    [InlineData(0, 8)] // 0/2 = 0 exactly: nothing to round.
    [InlineData(2, 9)] // 2/2 = 1 exactly: nothing to round.
    [InlineData(4, 10)] // 4/2 = 2 exactly: nothing to round.
    [InlineData(6, 11)] // 6/2 = 3 exactly: nothing to round.
    public void Even_body_rank_needs_no_rounding(int bodyRank, int expectedBoxes)
    {
        // Side 1 of the boundary: an implementation that over-rounds (adds the extra box
        // even when the half is already exact -- e.g. `(rank / 2) + 1 + 8` unconditionally)
        // fails here first, at the smallest case (0 -> 8, not 9).
        Assert.Equal(expectedBoxes, ConditionMonitor.PhysicalBoxes(bodyRank));
    }

    [Theory]
    [InlineData(1, 9)] // 1/2 = 0.5, rounded up to 1.
    [InlineData(3, 10)] // 3/2 = 1.5, rounded up to 2.
    [InlineData(5, 11)] // 5/2 = 2.5, rounded up to 3.
    [InlineData(7, 12)] // 7/2 = 3.5, rounded up to 4.
    public void Odd_body_rank_rounds_the_half_up_not_down(int bodyRank, int expectedBoxes)
    {
        // Side 2 of the boundary: an implementation that rounds down (plain truncating
        // `rank / 2`, i.e. floor instead of ceiling) fails here first, at the smallest
        // case (1 -> 8, not 9). This is the test named in the PR's determinism/rounding
        // report as the one a wrong rounding direction fails.
        Assert.Equal(expectedBoxes, ConditionMonitor.PhysicalBoxes(bodyRank));
    }

    // ------------------------------------------------------------------- the whole domain

    [Fact]
    public void PhysicalBoxes_matches_ceiling_division_for_every_rank_zero_to_the_generous_bound()
    {
        for (int bodyRank = 0; bodyRank <= GenerousUpperBound; bodyRank++)
        {
            // Independent oracle: `(n / 2) + (n % 2)` is a different arithmetic expression
            // from the production `(n + 1) / 2`, computed with C#'s own truncating integer
            // division and modulo -- not a restatement of the implementation under test.
            int expectedBoxes = (bodyRank / 2) + (bodyRank % 2) + 8;

            Assert.Equal(expectedBoxes, ConditionMonitor.PhysicalBoxes(bodyRank));
        }
    }

    // ------------------------------------------------------------ Stun shares the formula

    [Fact]
    public void StunBoxes_matches_PhysicalBoxes_for_every_rank_zero_to_the_generous_bound()
    {
        // Printed p. 38 gives Physical and Stun the identical formula, differing only in
        // which attribute feeds it (Body vs. Willpower). This pins that StunBoxes is not a
        // second, independently-drifting implementation of the same rule.
        for (int rank = 0; rank <= GenerousUpperBound; rank++)
        {
            Assert.Equal(ConditionMonitor.PhysicalBoxes(rank), ConditionMonitor.StunBoxes(rank));
        }
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(1, 9)]
    [InlineData(2, 9)]
    [InlineData(3, 10)]
    public void StunBoxes_boundary_is_pinned_directly_not_only_via_PhysicalBoxes(
        int willpowerRank, int expectedBoxes)
    {
        Assert.Equal(expectedBoxes, ConditionMonitor.StunBoxes(willpowerRank));
    }
}
