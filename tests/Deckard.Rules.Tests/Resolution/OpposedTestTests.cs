using Deckard.Rules.Resolution;
using Deckard.Testing.Randomness;

namespace Deckard.Rules.Tests.Resolution;

/// <summary>
/// Pins <see cref="OpposedTest"/> against SR6 Core / Game Concepts / Tests / Opposed Tests
/// / printed p. 35-36 / PDF p. 36-37: both sides roll, the higher hit count wins, ties go
/// to the aggressor (modeled as the acting side), and net hits are the difference between
/// the two hit counts.
/// </summary>
public sealed class OpposedTestTests
{
    private static FixedSequenceRandomSource ForFaces(params int[] faces) =>
        new(faces.Select(f => (uint)(f - 1)));

    // -------------------------------------------------------------- argument validation

    [Fact]
    public void Resolve_throws_on_null_source()
    {
        Assert.Throws<ArgumentNullException>(() => OpposedTest.Resolve(null!, 3, 3));
    }

    [Fact]
    public void Resolve_throws_on_negative_actor_pool()
    {
        var source = ForFaces();
        Assert.Throws<ArgumentOutOfRangeException>(() => OpposedTest.Resolve(source, -1, 3));
    }

    [Fact]
    public void Resolve_throws_on_negative_defender_pool()
    {
        var source = ForFaces(5, 5, 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => OpposedTest.Resolve(source, 3, -1));
    }

    // --------------------------------------------------------------------------- winner

    [Fact]
    public void Actor_wins_outright_with_more_hits()
    {
        var source = ForFaces(5, 5, 5, /* actor: 3 hits */ 2, 3, 4 /* defender: 0 hits */);

        OpposedTestResult result = OpposedTest.Resolve(source, actorPool: 3, defenderPool: 3);

        Assert.Equal(OpposedTestWinner.Actor, result.Winner);
        Assert.False(result.Tied);
        Assert.Equal(3, result.NetHits);
    }

    [Fact]
    public void Defender_wins_with_more_hits()
    {
        var source = ForFaces(2, 3, 4, /* actor: 0 hits */ 5, 5, 5 /* defender: 3 hits */);

        OpposedTestResult result = OpposedTest.Resolve(source, actorPool: 3, defenderPool: 3);

        Assert.Equal(OpposedTestWinner.Defender, result.Winner);
        Assert.False(result.Tied);
        Assert.Equal(3, result.NetHits);
    }

    [Fact]
    public void Ties_go_to_the_actor_with_zero_net_hits()
    {
        var source = ForFaces(5, 5, 2, /* actor: 2 hits */ 6, 6, 3 /* defender: 2 hits */);

        OpposedTestResult result = OpposedTest.Resolve(source, actorPool: 3, defenderPool: 3);

        Assert.True(result.Tied);
        Assert.Equal(OpposedTestWinner.Actor, result.Winner);
        Assert.Equal(0, result.NetHits);
    }

    [Fact]
    public void A_zero_zero_tie_still_resolves_to_the_actor()
    {
        // Both sides roll an empty pool: 0 hits apiece is still a tie, and nothing is
        // ever drawn from the source to produce it.
        var source = ForFaces();

        OpposedTestResult result = OpposedTest.Resolve(source, actorPool: 0, defenderPool: 0);

        Assert.True(result.Tied);
        Assert.Equal(OpposedTestWinner.Actor, result.Winner);
        Assert.Equal(0, result.NetHits);
        Assert.Equal(0, source.Consumed);
    }

    // ------------------------------------------------------------------------ net hits

    [Fact]
    public void Net_hits_is_the_absolute_difference_regardless_of_direction()
    {
        var actorMore = ForFaces(5, 5, 5, 5, /* actor: 4 hits */ 2, 3 /* defender: 0 hits */);
        OpposedTestResult a = OpposedTest.Resolve(actorMore, actorPool: 4, defenderPool: 2);
        Assert.Equal(4, a.NetHits);

        var defenderMore = ForFaces(2, 3, /* actor: 0 hits */ 5, 5, 5, 5 /* defender: 4 hits */);
        OpposedTestResult d = OpposedTest.Resolve(defenderMore, actorPool: 2, defenderPool: 4);
        Assert.Equal(4, d.NetHits);
    }

    // --------------------------------------------------------------------- draw order

    [Fact]
    public void The_actor_rolls_before_the_defender()
    {
        // Actor's pool (2 dice) is scripted as unambiguous hits (5,5); defender's pool
        // (1 die) is scripted as an unambiguous one (1). If the draw order were reversed,
        // the actor would instead see [1, 5] and the defender would see [5] -- a
        // completely different hit count on each side -- so this only passes under the
        // documented actor-first order.
        var source = ForFaces(5, 5, 1);

        OpposedTestResult result = OpposedTest.Resolve(source, actorPool: 2, defenderPool: 1);

        Assert.Equal(new[] { 5, 5 }, result.Actor.DiceRolled);
        Assert.Equal(2, result.Actor.Hits);
        Assert.Equal(new[] { 1 }, result.Defender.DiceRolled);
        Assert.Equal(1, result.Defender.Ones);
        Assert.Equal(3, source.Consumed);
    }

    // ------------------------------------------------------------------- independent glitches

    [Fact]
    public void Each_side_glitches_independently_of_who_wins()
    {
        // Actor's pool has 3 of 4 dice as 1s (a glitch) but also a hit, so it is a
        // plain glitch, not critical; the actor still wins on hits. Defender's pool has
        // no 1s and no hits, so it neither glitches nor wins.
        var source = ForFaces(1, 1, 1, 6, 2, 3, 4);

        OpposedTestResult result = OpposedTest.Resolve(source, actorPool: 4, defenderPool: 3);

        Assert.Equal(GlitchSeverity.Glitch, result.Actor.Glitch);
        Assert.Equal(GlitchSeverity.None, result.Defender.Glitch);
        Assert.Equal(OpposedTestWinner.Actor, result.Winner);
    }
}
