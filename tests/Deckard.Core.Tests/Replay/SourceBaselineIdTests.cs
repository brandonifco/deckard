using Deckard.Core.Replay;

namespace Deckard.Core.Tests.Replay;

/// <summary>See ADR 0005.</summary>
public sealed class SourceBaselineIdTests
{
    [Fact]
    public void Same_source_id_compares_equal()
    {
        var a = new SourceBaselineId("sr6-core");
        var b = new SourceBaselineId("sr6-core");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Different_source_id_compares_unequal()
    {
        var a = new SourceBaselineId("sr6-core");
        var b = new SourceBaselineId("sr6-seattle-city-edition");

        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Null_empty_or_whitespace_source_id_throws(string? sourceId)
    {
        // Null specifically throws ArgumentNullException (a subtype); ThrowsAny covers
        // the whole theory without over-specifying which exact subtype null gets.
        Assert.ThrowsAny<ArgumentException>(() => new SourceBaselineId(sourceId!));
    }
}
