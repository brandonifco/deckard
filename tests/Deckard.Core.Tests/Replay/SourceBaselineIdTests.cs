using Deckard.Core.Replay;

namespace Deckard.Core.Tests.Replay;

/// <summary>
/// See ADR 0005. The test that matters here is
/// <see cref="Same_source_id_but_different_hash_compares_unequal"/>: a source-baseline
/// re-pin edits the manifest's <c>sha256</c> in place while leaving <c>sourceId</c>
/// unchanged, so an identifier that ignored <c>sha256</c> would compare equal across
/// exactly the event it exists to detect -- see Issue #38's corrected "Known ambiguity"
/// section.
/// </summary>
public sealed class SourceBaselineIdTests
{
    private const string FirstPrintingHash = "7db88be98dbc2a4d1f777b7ff10034af3ea35e20bb3899b2aad95b7232bbac41";
    private const string RepinnedHash = "0000000000000000000000000000000000000000000000000000000000000000";

    [Fact]
    public void Same_source_id_and_hash_compare_equal()
    {
        var a = new SourceBaselineId("sr6-core", FirstPrintingHash);
        var b = new SourceBaselineId("sr6-core", FirstPrintingHash);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Different_source_id_compares_unequal()
    {
        var a = new SourceBaselineId("sr6-core", FirstPrintingHash);
        var b = new SourceBaselineId("sr6-seattle-city-edition", FirstPrintingHash);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Same_source_id_but_different_hash_compares_unequal()
    {
        // The acceptance test for this type: a re-pin (a different printing, a corrected
        // hash) edits sha256 in place and never touches sourceId. Before this fix,
        // SourceBaselineId carried only sourceId, so this exact pair compared equal --
        // the identity would have silently missed the one event it exists to catch.
        var beforeRepin = new SourceBaselineId("sr6-core", FirstPrintingHash);
        var afterRepin = new SourceBaselineId("sr6-core", RepinnedHash);

        Assert.NotEqual(beforeRepin, afterRepin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Null_empty_or_whitespace_source_id_throws(string? sourceId)
    {
        // Null specifically throws ArgumentNullException (a subtype); ThrowsAny covers
        // the whole theory without over-specifying which exact subtype null gets.
        Assert.ThrowsAny<ArgumentException>(() => new SourceBaselineId(sourceId!, FirstPrintingHash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Null_empty_or_whitespace_hash_throws(string? sha256)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SourceBaselineId("sr6-core", sha256!));
    }

    [Fact]
    public void Default_bypasses_the_constructor_and_yields_null_fields()
    {
        // Pins the documented gap (ADR 0005 Consequences): a struct's implicit
        // parameterless constructor -- default(T), new T(), or a deserializer setting
        // fields directly -- bypasses SourceBaselineId's constructor entirely. Nothing
        // consumes this type yet, so there is no second entry point to re-validate at
        // (contrast Pcg32.FromState, which re-checks Pcg32State for the same reason a
        // real consumer exists for it). This test exists so the gap is a recorded fact,
        // not a surprise for whoever writes the first consumer.
        var value = default(SourceBaselineId);

        Assert.Null(value.SourceId);
        Assert.Null(value.Sha256);
    }
}
