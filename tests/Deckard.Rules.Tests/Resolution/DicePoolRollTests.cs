using Deckard.Rules.Resolution;
using Deckard.Testing.Randomness;

namespace Deckard.Rules.Tests.Resolution;

/// <summary>
/// Pins <see cref="DicePoolRoll"/> against SR6 Core / Game Concepts / Tests / printed
/// p. 35 / PDF p. 36 (hits = 5s and 6s, ones counted separately) and Glitches and Critical
/// Glitches / printed p. 44 / PDF p. 45 (glitch = more than half the pool is 1s; critical
/// glitch = a glitch with zero hits). Every scripted sequence is a face list, converted to
/// the exact raw <c>uint32</c> values <c>D6.Roll</c> accepts (face - 1) so expectations are
/// exact rather than statistical, per the Issue's required evidence.
/// </summary>
public sealed class DicePoolRollTests
{
    private static FixedSequenceRandomSource ForFaces(params int[] faces) =>
        new(faces.Select(f => (uint)(f - 1)));

    // -------------------------------------------------------------- argument validation

    [Fact]
    public void Roll_throws_on_null_source()
    {
        Assert.Throws<ArgumentNullException>(() => DicePoolRoll.Roll(null!, 3));
    }

    [Fact]
    public void Roll_throws_on_negative_dice_pool()
    {
        var source = ForFaces();
        Assert.Throws<ArgumentOutOfRangeException>(() => DicePoolRoll.Roll(source, -1));
    }

    [Fact]
    public void Zero_dice_pool_rolls_nothing_and_never_glitches()
    {
        // "More than half of zero dice are 1s" is vacuously false: 0 dice can never
        // satisfy a strict majority. This is the zero-hits boundary at its most extreme.
        var source = ForFaces();

        DicePoolRoll roll = DicePoolRoll.Roll(source, 0);

        Assert.Empty(roll.DiceRolled);
        Assert.Equal(0, roll.Hits);
        Assert.Equal(0, roll.Ones);
        Assert.Equal(GlitchSeverity.None, roll.Glitch);
        Assert.Equal(0, source.Consumed);
    }

    // ------------------------------------------------------------------- hit counting

    [Fact]
    public void Hits_count_only_5s_and_6s_ones_count_only_1s_the_rest_count_as_neither()
    {
        // One die of every face: exercises the full 1-6 domain in a single pool so hit
        // counting, one counting, and "counts as neither" are all pinned in one place.
        var source = ForFaces(1, 2, 3, 4, 5, 6);

        DicePoolRoll roll = DicePoolRoll.Roll(source, 6);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, roll.DiceRolled);
        Assert.Equal(2, roll.Hits); // 5, 6
        Assert.Equal(1, roll.Ones); // 1
        // 1 one out of 6 dice: 1*2=2 is not > 6, so no glitch even though a 1 was rolled.
        Assert.Equal(GlitchSeverity.None, roll.Glitch);
        Assert.Equal(6, source.Consumed);
    }

    [Fact]
    public void Dice_rolled_preserves_draw_order_not_sorted_order()
    {
        // Faces deliberately out of ascending order (ADR 0006): a bug that silently
        // sorted or grouped dice would be indistinguishable from correct behaviour on an
        // already-ordered script.
        var source = ForFaces(6, 1, 4, 5, 2, 1);

        DicePoolRoll roll = DicePoolRoll.Roll(source, 6);

        Assert.Equal(new[] { 6, 1, 4, 5, 2, 1 }, roll.DiceRolled);
        Assert.Equal(2, roll.Hits); // 6, 5
        Assert.Equal(2, roll.Ones); // the two 1s
    }

    [Fact]
    public void Zero_hits_without_a_glitch_is_representable()
    {
        // No 1s, no 5s or 6s at all: the plainest possible failed, non-glitching roll.
        var source = ForFaces(2, 3, 4, 2, 3, 4);

        DicePoolRoll roll = DicePoolRoll.Roll(source, 6);

        Assert.Equal(0, roll.Hits);
        Assert.Equal(0, roll.Ones);
        Assert.Equal(GlitchSeverity.None, roll.Glitch);
    }

    // ------------------------------------------------------------- glitch boundary

    [Theory]
    [InlineData(new[] { 1, 1, 2, 3 }, 4)] // 2 of 4: exactly half -- not a glitch
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 5)] // 1 of 5: below half -- not a glitch
    [InlineData(new[] { 1, 1, 3, 4, 5, 6 }, 6)] // 2 of 6: exactly a third -- not a glitch
    public void Exactly_half_or_fewer_ones_never_glitches(int[] faces, int poolSize)
    {
        var source = ForFaces(faces);

        DicePoolRoll roll = DicePoolRoll.Roll(source, poolSize);

        Assert.Equal(GlitchSeverity.None, roll.Glitch);
    }

    [Theory]
    [InlineData(new[] { 1, 1, 1, 4 }, 4)] // 3 of 4: one more than half -- glitches
    [InlineData(new[] { 1, 1, 1, 2, 5 }, 5)] // 3 of 5: majority -- glitches
    [InlineData(new[] { 1, 1, 2 }, 3)] // 2 of 3: majority -- glitches
    public void More_than_half_ones_always_glitches(int[] faces, int poolSize)
    {
        var source = ForFaces(faces);

        DicePoolRoll roll = DicePoolRoll.Roll(source, poolSize);

        Assert.NotEqual(GlitchSeverity.None, roll.Glitch);
    }

    [Fact]
    public void Single_die_pool_of_a_1_is_a_critical_glitch()
    {
        // A one-die pool cannot show both a 1 and a hit, so any glitching single die is
        // automatically a critical glitch -- the sharpest instance of the majority rule.
        var source = ForFaces(1);

        DicePoolRoll roll = DicePoolRoll.Roll(source, 1);

        Assert.Equal(1, roll.Ones);
        Assert.Equal(0, roll.Hits);
        Assert.Equal(GlitchSeverity.CriticalGlitch, roll.Glitch);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Single_die_pool_of_anything_but_a_1_never_glitches(int face)
    {
        var source = ForFaces(face);

        DicePoolRoll roll = DicePoolRoll.Roll(source, 1);

        Assert.Equal(GlitchSeverity.None, roll.Glitch);
    }

    // ------------------------------------------------------------ critical glitch

    [Fact]
    public void All_ones_is_always_a_critical_glitch()
    {
        var source = ForFaces(1, 1, 1, 1, 1);

        DicePoolRoll roll = DicePoolRoll.Roll(source, 5);

        Assert.Equal(5, roll.Ones);
        Assert.Equal(0, roll.Hits);
        Assert.Equal(GlitchSeverity.CriticalGlitch, roll.Glitch);
    }

    [Fact]
    public void A_glitch_with_at_least_one_hit_is_a_glitch_not_a_critical_glitch()
    {
        // Majority ones (3 of 4), but one of the remaining dice is a hit: "a glitch
        // without a single hit" is specifically false here, so this must NOT be critical.
        var source = ForFaces(1, 1, 1, 6);

        DicePoolRoll roll = DicePoolRoll.Roll(source, 4);

        Assert.Equal(1, roll.Hits);
        Assert.Equal(GlitchSeverity.Glitch, roll.Glitch);
    }

    [Fact]
    public void Zero_hits_with_exactly_half_ones_is_not_a_critical_glitch()
    {
        // Zero hits alone does not imply critical glitch: the majority-ones condition
        // must independently hold. 2 of 4 ones (exactly half) plus zero hits is a
        // straightforward failure, not any severity of glitch.
        var source = ForFaces(1, 1, 2, 3);

        DicePoolRoll roll = DicePoolRoll.Roll(source, 4);

        Assert.Equal(0, roll.Hits);
        Assert.Equal(2, roll.Ones);
        Assert.Equal(GlitchSeverity.None, roll.Glitch);
    }

    // --------------------------------------------------------------- draw accounting

    [Fact]
    public void Rolling_N_dice_with_no_rejections_consumes_exactly_N_draws()
    {
        var source = ForFaces(1, 2, 3, 4, 5, 6, 1, 2);

        DicePoolRoll.Roll(source, 8);

        Assert.Equal(8, source.Consumed);
    }
}
