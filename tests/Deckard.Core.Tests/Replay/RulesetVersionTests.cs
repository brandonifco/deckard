using Deckard.Core.Replay;

namespace Deckard.Core.Tests.Replay;

/// <summary>See ADR 0005.</summary>
public sealed class RulesetVersionTests
{
    [Fact]
    public void Same_id_and_version_compare_equal()
    {
        var a = new RulesetVersion("sr6-core-ruleset", 1);
        var b = new RulesetVersion("sr6-core-ruleset", 1);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Different_id_compares_unequal()
    {
        var a = new RulesetVersion("sr6-core-ruleset", 1);
        var b = new RulesetVersion("some-house-ruleset", 1);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Different_version_compares_unequal()
    {
        var a = new RulesetVersion("sr6-core-ruleset", 1);
        var b = new RulesetVersion("sr6-core-ruleset", 2);

        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Null_empty_or_whitespace_id_throws(string? id)
    {
        // Null specifically throws ArgumentNullException (a subtype); ThrowsAny covers
        // the whole theory without over-specifying which exact subtype null gets.
        Assert.ThrowsAny<ArgumentException>(() => new RulesetVersion(id!, 1));
    }

    [Fact]
    public void Negative_version_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RulesetVersion("sr6-core-ruleset", -1));
    }
}
