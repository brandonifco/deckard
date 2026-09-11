using System;
using Deckard.Rules.Edge;

namespace Deckard.Rules.Tests.Edge;

/// <summary>
/// Pins <see cref="EdgePool"/> against SR6 Core / Game Concepts / Edge / printed pp.
/// 45-48 / PDF pp. 46-49. See the type's own doc comment for the exact citation each rule
/// comes from; this file cites the same locations at the point each rule is tested rather
/// than restating the whole list here.
/// </summary>
public sealed class EdgePoolTests
{
    // --------------------------------------------------------------------- StartSession

    [Fact]
    public void StartSession_throws_on_negative_edge_rank()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EdgePool.StartSession(-1));
    }

    [Fact]
    public void StartSession_sets_current_equal_to_edge_rank_with_no_combat_round_in_progress()
    {
        EdgePool pool = EdgePool.StartSession(3);

        Assert.Equal(3, pool.Current);
        Assert.Null(pool.BonusGainedThisRound);
    }

    // ------------------------------------------------------------- GainBonusPoint: round cap
    // (ADR 0008: only enforced while a combat round is in progress)

    [Fact]
    public void First_bonus_point_in_a_combat_round_is_granted()
    {
        EdgePool pool = EdgePool.StartSession(0).BeginCombatRound();

        EdgeGainResult result = pool.GainBonusPoint();

        Assert.True(result.Granted);
        Assert.False(result.BlockedByRoundCap);
        Assert.Equal(1, result.PoolAfter.Current);
        Assert.Equal(1, result.PoolAfter.BonusGainedThisRound);
    }

    [Fact]
    public void Second_bonus_point_in_the_same_round_reaches_the_cap_and_is_still_granted()
    {
        // Side 1 of the round-cap boundary: an implementation using a strict `>` bound
        // (or an off-by-one `> RoundGainCap - 1`) instead of `>=` against the *pre-gain*
        // count would wrongly refuse this, the exact boundary case.
        EdgePool pool = EdgePool.StartSession(0).BeginCombatRound();
        pool = pool.GainBonusPoint().PoolAfter;

        EdgeGainResult result = pool.GainBonusPoint();

        Assert.True(result.Granted);
        Assert.False(result.BlockedByRoundCap);
        Assert.Equal(2, result.PoolAfter.Current);
        Assert.Equal(2, result.PoolAfter.BonusGainedThisRound);
    }

    [Fact]
    public void Third_bonus_point_in_the_same_round_is_refused_by_the_round_cap()
    {
        // Side 2 of the round-cap boundary: an implementation that never enforces the
        // cap (or enforces it one point too late, e.g. `> RoundGainCap + 1`) would wrongly
        // grant this. "No player may gain more than two bonus points of Edge in a combat
        // round" -- SR6 Core / Game Concepts / Edge / printed p. 45 / PDF p. 46.
        EdgePool pool = EdgePool.StartSession(0).BeginCombatRound();
        pool = pool.GainBonusPoint().PoolAfter;
        pool = pool.GainBonusPoint().PoolAfter;
        Assert.Equal(2, pool.BonusGainedThisRound);

        EdgeGainResult result = pool.GainBonusPoint();

        Assert.False(result.Granted);
        Assert.True(result.BlockedByRoundCap);
        Assert.False(result.BlockedByHoldCap);
        // Refused: pool is unchanged.
        Assert.Equal(2, result.PoolAfter.Current);
        Assert.Equal(2, result.PoolAfter.BonusGainedThisRound);
        Assert.Same(result.PoolBefore, result.PoolAfter);
    }

    [Fact]
    public void BeginCombatRound_resets_the_round_counter_for_a_new_round()
    {
        EdgePool pool = EdgePool.StartSession(0).BeginCombatRound();
        pool = pool.GainBonusPoint().PoolAfter;
        pool = pool.GainBonusPoint().PoolAfter;
        Assert.Equal(2, pool.BonusGainedThisRound);

        pool = pool.BeginCombatRound(); // the next combat round
        Assert.Equal(0, pool.BonusGainedThisRound);
        Assert.Equal(2, pool.Current); // Current itself is untouched by a round boundary.

        EdgeGainResult result = pool.GainBonusPoint();

        Assert.True(result.Granted);
        Assert.Equal(3, result.PoolAfter.Current);
        Assert.Equal(1, result.PoolAfter.BonusGainedThisRound);
    }

    // ------------------------------------------------- ADR 0008: no cap outside a round

    [Fact]
    public void Gaining_with_no_combat_round_in_progress_is_not_limited_by_the_round_cap()
    {
        // ADR 0008: the printed cap is scoped "in a combat round". StartSession leaves
        // no combat round in progress, so three consecutive gains here must all succeed
        // -- an implementation that still applies RoundGainCap here is exactly the
        // "applied outside combat" defect ADR 0008 exists to rule out.
        EdgePool pool = EdgePool.StartSession(0);
        pool = pool.GainBonusPoint().PoolAfter;
        pool = pool.GainBonusPoint().PoolAfter;

        EdgeGainResult result = pool.GainBonusPoint();

        Assert.True(result.Granted);
        Assert.False(result.BlockedByRoundCap);
        Assert.Equal(3, result.PoolAfter.Current);
        Assert.Null(result.PoolAfter.BonusGainedThisRound);
    }

    [Fact]
    public void EndCombatRound_clears_the_round_counter_and_lifts_the_round_cap()
    {
        EdgePool pool = EdgePool.StartSession(0).BeginCombatRound();
        pool = pool.GainBonusPoint().PoolAfter; // 1
        pool = pool.GainBonusPoint().PoolAfter; // 2, at the round cap

        pool = pool.EndCombatRound();
        Assert.Null(pool.BonusGainedThisRound);

        // A third gain would have been refused mid-round (see the round-cap test above);
        // once the round has ended, it must succeed.
        EdgeGainResult result = pool.GainBonusPoint();

        Assert.True(result.Granted);
        Assert.False(result.BlockedByRoundCap);
        Assert.Null(result.PoolAfter.BonusGainedThisRound);
    }

    // -------------------------------------------------------------- GainBonusPoint: hold cap

    [Fact]
    public void Gaining_up_to_exactly_the_hold_cap_is_granted()
    {
        // Side 1 of the hold-cap boundary: landing exactly on HoldCap (7) must succeed.
        // "accumulated up to a limit of 7" -- SR6 Core / Game Concepts / Edge / printed
        // p. 45 / PDF p. 46.
        EdgePool pool = EdgePool.StartSession(6);

        EdgeGainResult result = pool.GainBonusPoint();

        Assert.True(result.Granted);
        Assert.False(result.BlockedByHoldCap);
        Assert.Equal(7, result.PoolAfter.Current);
    }

    [Fact]
    public void Gaining_past_the_hold_cap_is_refused()
    {
        // Side 2 of the hold-cap boundary: already at 7, one more must be refused, not
        // silently allowed to 8.
        EdgePool pool = EdgePool.StartSession(7);

        EdgeGainResult result = pool.GainBonusPoint();

        Assert.False(result.Granted);
        Assert.True(result.BlockedByHoldCap);
        Assert.False(result.BlockedByRoundCap);
        Assert.Equal(7, result.PoolAfter.Current);
    }

    [Fact]
    public void Hold_cap_refusal_leaves_no_combat_round_state_behind()
    {
        EdgePool pool = EdgePool.StartSession(7);

        EdgeGainResult result = pool.GainBonusPoint();

        Assert.Null(result.PoolAfter.BonusGainedThisRound);
    }

    [Fact]
    public void Both_caps_can_block_the_same_refusal_at_once()
    {
        // Manufacture a pool that is simultaneously at both boundaries: seven current,
        // two already gained this combat round.
        EdgePool pool = EdgePool.StartSession(5).BeginCombatRound();
        pool = pool.GainBonusPoint().PoolAfter; // 6, round=1
        pool = pool.GainBonusPoint().PoolAfter; // 7, round=2 -- at both boundaries now

        EdgeGainResult result = pool.GainBonusPoint();

        Assert.False(result.Granted);
        Assert.True(result.BlockedByRoundCap);
        Assert.True(result.BlockedByHoldCap);
    }

    // ------------------------------------------------------------------------------ Spend

    [Fact]
    public void Spend_throws_on_zero_amount()
    {
        EdgePool pool = EdgePool.StartSession(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Spend(0));
    }

    [Fact]
    public void Spend_throws_on_negative_amount()
    {
        EdgePool pool = EdgePool.StartSession(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Spend(-1));
    }

    [Fact]
    public void Spending_exactly_the_current_pool_succeeds_and_empties_it()
    {
        // Side 1 of the spend boundary: spending exactly what is held must succeed.
        EdgePool pool = EdgePool.StartSession(3);

        EdgeSpendResult result = pool.Spend(3);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.PoolAfter.Current);
    }

    [Fact]
    public void Spending_one_more_than_the_current_pool_is_refused()
    {
        // Side 2 of the spend boundary: one more than what is held must be refused, not
        // silently allowed to go negative.
        EdgePool pool = EdgePool.StartSession(3);

        EdgeSpendResult result = pool.Spend(4);

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.PoolAfter.Current);
        Assert.Same(result.PoolBefore, result.PoolAfter);
    }

    [Fact]
    public void Spend_does_not_touch_the_round_counter()
    {
        EdgePool pool = EdgePool.StartSession(3).BeginCombatRound();
        pool = pool.GainBonusPoint().PoolAfter; // Current=4, round=1

        EdgeSpendResult result = pool.Spend(2);

        Assert.Equal(1, result.PoolAfter.BonusGainedThisRound);
    }

    // --------------------------------------------------------------------- EndConfrontation

    [Fact]
    public void EndConfrontation_throws_on_negative_edge_rank()
    {
        EdgePool pool = EdgePool.StartSession(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.EndConfrontation(-1));
    }

    [Fact]
    public void EndConfrontation_drops_accumulated_edge_above_the_attribute_back_to_it()
    {
        // "Any Edge garnered over your base attribute goes away when you complete any
        // ongoing confrontation" -- SR6 Core / Game Concepts / Edge / printed p. 45 /
        // PDF p. 46.
        EdgePool pool = EdgePool.StartSession(2);
        pool = pool.GainBonusPoint().PoolAfter; // Current=3
        pool = pool.GainBonusPoint().PoolAfter; // Current=4

        EdgePool afterConfrontation = pool.EndConfrontation(edgeRank: 2);

        Assert.Equal(2, afterConfrontation.Current);
    }

    [Fact]
    public void EndConfrontation_leaves_a_pool_already_below_the_attribute_at_its_lower_level()
    {
        // "If, at the end of the confrontation, your current Edge points are less than
        // your Edge attribute, you stay at the lower level. If you want more Edge, you
        // have to earn it." Same citation as above -- this must not top the pool back up.
        EdgePool pool = EdgePool.StartSession(5);
        pool = pool.Spend(4).PoolAfter; // Current=1, well below rank 5

        EdgePool afterConfrontation = pool.EndConfrontation(edgeRank: 5);

        Assert.Equal(1, afterConfrontation.Current);
    }

    [Fact]
    public void EndConfrontation_leaves_a_pool_exactly_at_the_attribute_unchanged()
    {
        EdgePool pool = EdgePool.StartSession(4);

        EdgePool afterConfrontation = pool.EndConfrontation(edgeRank: 4);

        Assert.Equal(4, afterConfrontation.Current);
    }

    // ------------------------------------------------------------------------------- Burn

    [Fact]
    public void Burn_throws_on_negative_edge_rank()
    {
        EdgePool pool = EdgePool.StartSession(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Burn(-1));
    }

    [Fact]
    public void Burn_is_refused_when_rank_is_already_zero()
    {
        // The source frames burning as taking rank "down to zero", never below it --
        // burning at rank 0 has nothing left to spend and must be refused, not silently
        // succeed at 0 -> 0 while still emptying the pool.
        EdgePool pool = EdgePool.StartSession(4);

        EdgeBurnResult result = pool.Burn(edgeRank: 0);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.RankAfter);
        Assert.Same(result.PoolBefore, result.PoolAfter);
        Assert.Equal(4, result.PoolAfter.Current);
    }

    [Fact]
    public void Burn_from_rank_one_reaches_the_printed_floor_of_zero()
    {
        // The printed boundary case: "even taking it down to zero if you so choose" --
        // SR6 Core / Game Concepts / Edge / Burning Edge / printed p. 48 / PDF p. 49.
        // Rank 1 -> 0 is the only case that pins this floor without also being the
        // already-refused rank-0 case above.
        EdgePool pool = EdgePool.StartSession(1);

        EdgeBurnResult result = pool.Burn(edgeRank: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.RankBefore);
        Assert.Equal(0, result.RankAfter);
        Assert.Equal(0, result.PoolAfter.Current);
    }

    [Fact]
    public void Burn_permanently_reduces_rank_by_one_and_empties_the_pool()
    {
        EdgePool pool = EdgePool.StartSession(5);
        pool = pool.GainBonusPoint().PoolAfter; // Current=6

        EdgeBurnResult result = pool.Burn(edgeRank: 5);

        Assert.True(result.Succeeded);
        Assert.Equal(5, result.RankBefore);
        Assert.Equal(4, result.RankAfter);
        Assert.Equal(0, result.PoolAfter.Current);
    }

    [Fact]
    public void Burn_empties_the_pool_even_when_it_held_less_than_the_rank()
    {
        EdgePool pool = EdgePool.StartSession(5);
        pool = pool.Spend(5).PoolAfter; // Current=0 already

        EdgeBurnResult result = pool.Burn(edgeRank: 5);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.PoolAfter.Current);
    }

    [Fact]
    public void Burn_does_not_touch_the_round_counter()
    {
        EdgePool pool = EdgePool.StartSession(0).BeginCombatRound();
        pool = pool.GainBonusPoint().PoolAfter; // round=1

        EdgeBurnResult result = pool.Burn(edgeRank: 1);

        Assert.Equal(1, result.PoolAfter.BonusGainedThisRound);
    }
}
