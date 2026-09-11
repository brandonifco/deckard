using System;
using Deckard.Rules.Edge;

namespace Deckard.Rules.Tests.Edge;

/// <summary>
/// Pins <see cref="AttackDefenseRatingEdgeGain"/> against SR6 Core / Game Concepts / Edge
/// / Gaining Edge / printed p. 45 / PDF p. 46: "If either is 4 or more greater than the
/// other, that player gets a point of Edge."
/// </summary>
public sealed class AttackDefenseRatingEdgeGainTests
{
    // ------------------------------------------------------------------ argument validation

    [Fact]
    public void Evaluate_throws_on_negative_attack_rating()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AttackDefenseRatingEdgeGain.Evaluate(-1, 3));
    }

    [Fact]
    public void Evaluate_throws_on_negative_defense_rating()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AttackDefenseRatingEdgeGain.Evaluate(3, -1));
    }

    // ------------------------------------------------------------------------- the margin

    [Fact]
    public void Equal_ratings_favor_neither_side()
    {
        Assert.Equal(AttackDefenseEdgeWinner.Neither, AttackDefenseRatingEdgeGain.Evaluate(5, 5));
    }

    [Fact]
    public void A_difference_of_three_is_below_the_margin_and_favors_neither_side()
    {
        // Side 1 of the margin boundary: one short of 4 must not grant a point. An
        // implementation using `> GainMargin` or a margin of 3 would wrongly grant here.
        Assert.Equal(AttackDefenseEdgeWinner.Neither, AttackDefenseRatingEdgeGain.Evaluate(8, 5));
        Assert.Equal(AttackDefenseEdgeWinner.Neither, AttackDefenseRatingEdgeGain.Evaluate(5, 8));
    }

    [Fact]
    public void A_difference_of_exactly_four_grants_the_attacker_the_point()
    {
        // Side 2 of the margin boundary: exactly 4 must grant the point ("4 or more"),
        // not require 5. An implementation using a strict `>` comparison against the
        // margin would wrongly refuse this.
        Assert.Equal(AttackDefenseEdgeWinner.Attacker, AttackDefenseRatingEdgeGain.Evaluate(9, 5));
    }

    [Fact]
    public void A_difference_of_exactly_four_grants_the_defender_the_point()
    {
        Assert.Equal(AttackDefenseEdgeWinner.Defender, AttackDefenseRatingEdgeGain.Evaluate(5, 9));
    }

    [Fact]
    public void A_difference_greater_than_four_still_grants_exactly_one_point_to_the_higher_side()
    {
        Assert.Equal(AttackDefenseEdgeWinner.Attacker, AttackDefenseRatingEdgeGain.Evaluate(12, 2));
        Assert.Equal(AttackDefenseEdgeWinner.Defender, AttackDefenseRatingEdgeGain.Evaluate(2, 12));
    }

    [Fact]
    public void Zero_ratings_on_both_sides_favor_neither()
    {
        Assert.Equal(AttackDefenseEdgeWinner.Neither, AttackDefenseRatingEdgeGain.Evaluate(0, 0));
    }
}
