using Deckard.Rules.Resolution;
using Deckard.Testing.Randomness;

namespace Deckard.Rules.Tests.Resolution;

/// <summary>
/// Pins <see cref="SimpleTest"/> against SR6 Core / Game Concepts / Tests / Simple Tests /
/// printed p. 35 / PDF p. 36: hits meeting or beating the threshold succeed; net hits are
/// the hits above the threshold. Also pins the printed Threshold Guidelines table (same
/// pages, values 1-7) end to end -- the mechanical rule is identical at every threshold
/// the table lists, so the whole table is exercised rather than a sample of it.
/// </summary>
public sealed class SimpleTestTests
{
    private static FixedSequenceRandomSource ForFaces(params int[] faces) =>
        new(faces.Select(f => (uint)(f - 1)));

    // -------------------------------------------------------------- argument validation

    [Fact]
    public void Resolve_throws_on_null_source()
    {
        Assert.Throws<ArgumentNullException>(() => SimpleTest.Resolve(null!, 3, 2));
    }

    [Fact]
    public void Resolve_throws_on_negative_dice_pool()
    {
        var source = ForFaces();
        Assert.Throws<ArgumentOutOfRangeException>(() => SimpleTest.Resolve(source, -1, 2));
    }

    [Fact]
    public void Resolve_throws_on_negative_threshold()
    {
        var source = ForFaces();
        Assert.Throws<ArgumentOutOfRangeException>(() => SimpleTest.Resolve(source, 3, -1));
    }

    // -------------------------------------------------------------------- success/failure

    [Fact]
    public void Hits_exactly_at_threshold_succeeds_with_zero_net_hits()
    {
        var source = ForFaces(5, 5, 2); // 2 hits
        SimpleTestResult result = SimpleTest.Resolve(source, dicePool: 3, threshold: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Roll.Hits);
        Assert.Equal(0, result.NetHits);
    }

    [Fact]
    public void One_hit_below_threshold_fails_with_zero_net_hits()
    {
        var source = ForFaces(5, 2, 2); // 1 hit
        SimpleTestResult result = SimpleTest.Resolve(source, dicePool: 3, threshold: 2);

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.Roll.Hits);
        // "hits above the threshold" is not defined below the threshold; reported as 0
        // rather than a negative shortfall.
        Assert.Equal(0, result.NetHits);
    }

    [Fact]
    public void One_hit_above_threshold_succeeds_with_one_net_hit()
    {
        var source = ForFaces(5, 5, 5); // 3 hits
        SimpleTestResult result = SimpleTest.Resolve(source, dicePool: 3, threshold: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Roll.Hits);
        Assert.Equal(1, result.NetHits);
    }

    [Fact]
    public void Zero_hits_against_a_positive_threshold_fails()
    {
        var source = ForFaces(2, 3, 4);
        SimpleTestResult result = SimpleTest.Resolve(source, dicePool: 3, threshold: 1);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.Roll.Hits);
        Assert.Equal(0, result.NetHits);
    }

    [Fact]
    public void Zero_threshold_always_succeeds_even_with_zero_hits()
    {
        // The general rule ("hits equal to or greater than the threshold succeed") holds
        // for a threshold of 0 exactly as for any other value -- no special-casing.
        var source = ForFaces(2, 3, 4);
        SimpleTestResult result = SimpleTest.Resolve(source, dicePool: 3, threshold: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NetHits);
    }

    // ------------------------------------------------------------------- glitch interplay

    [Fact]
    public void A_glitch_does_not_cancel_a_success()
    {
        // 3 of 4 dice are 1s (a glitch) but the 4th is a hit, and threshold 1 is met.
        var source = ForFaces(1, 1, 1, 6);
        SimpleTestResult result = SimpleTest.Resolve(source, dicePool: 4, threshold: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(GlitchSeverity.Glitch, result.Roll.Glitch);
    }

    [Fact]
    public void A_critical_glitch_can_still_succeed_against_a_zero_threshold()
    {
        // A critical glitch always has zero hits, so it can only succeed against a
        // threshold of 0 -- the general >= rule applies to a critical glitch exactly as
        // to any other roll; nothing here special-cases it into an automatic failure.
        var source = ForFaces(1, 1, 1);
        SimpleTestResult result = SimpleTest.Resolve(source, dicePool: 3, threshold: 0);

        Assert.Equal(GlitchSeverity.CriticalGlitch, result.Roll.Glitch);
        Assert.True(result.Succeeded);
        Assert.Equal(0, result.NetHits);
    }

    // --------------------------------------------------------- threshold guidelines table

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Every_threshold_in_the_printed_guidelines_table_succeeds_when_hits_meet_it(int threshold)
    {
        // Threshold Guidelines table, printed p. 36 / PDF p. 37, lists difficulty
        // descriptions for thresholds 1 through 7. A dice pool one larger than the
        // threshold, entirely 5s, always produces one more hit than the threshold.
        var faces = Enumerable.Repeat(5, threshold + 1).ToArray();
        var source = ForFaces(faces);

        SimpleTestResult result = SimpleTest.Resolve(source, dicePool: threshold + 1, threshold: threshold);

        Assert.True(result.Succeeded);
        Assert.Equal(threshold + 1, result.Roll.Hits);
        Assert.Equal(1, result.NetHits);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Every_threshold_in_the_printed_guidelines_table_fails_when_hits_fall_one_short(int threshold)
    {
        var faces = Enumerable.Repeat(5, threshold - 1).Append(2).ToArray();
        var source = ForFaces(faces);

        SimpleTestResult result = SimpleTest.Resolve(source, dicePool: threshold, threshold: threshold);

        Assert.False(result.Succeeded);
        Assert.Equal(threshold - 1, result.Roll.Hits);
        Assert.Equal(0, result.NetHits);
    }
}
