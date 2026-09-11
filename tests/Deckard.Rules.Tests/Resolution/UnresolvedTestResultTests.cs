using Deckard.Rules.Resolution;

namespace Deckard.Rules.Tests.Resolution;

/// <summary>
/// The first concrete shape given to ADR 0004's unresolved-result taxonomy
/// (docs/decisions/0004-unresolved-rule-taxonomy.md). Pins that its fields are set as
/// given and that it refuses to be constructed without the context that makes an
/// unresolved result actionable rather than mysterious.
/// </summary>
public sealed class UnresolvedTestResultTests
{
    [Fact]
    public void Constructor_stores_all_three_fields_as_given()
    {
        var result = new UnresolvedTestResult(
            UnresolvedReason.UnsupportedRule,
            attempted: "Extended test",
            sourceLocator: "SR6 Core / Game Concepts / Tests / Extended Tests / printed p. 36 / PDF p. 37");

        Assert.Equal(UnresolvedReason.UnsupportedRule, result.Reason);
        Assert.Equal("Extended test", result.Attempted);
        Assert.Equal(
            "SR6 Core / Game Concepts / Tests / Extended Tests / printed p. 36 / PDF p. 37",
            result.SourceLocator);
    }

    [Fact]
    public void Constructor_rejects_a_null_attempted_description()
    {
        // ArgumentException.ThrowIfNullOrWhiteSpace throws the ArgumentNullException
        // subtype for null specifically; xUnit's Assert.Throws<T> requires an exact
        // type match, so null and merely-blank inputs are asserted separately.
        Assert.Throws<ArgumentNullException>(() =>
            new UnresolvedTestResult(UnresolvedReason.UnsupportedRule, null!, "locator"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_attempted_description(string attempted)
    {
        Assert.Throws<ArgumentException>(() =>
            new UnresolvedTestResult(UnresolvedReason.UnsupportedRule, attempted, "locator"));
    }

    [Fact]
    public void Constructor_rejects_a_null_source_locator()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new UnresolvedTestResult(UnresolvedReason.UnsupportedRule, "attempted", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_source_locator(string sourceLocator)
    {
        Assert.Throws<ArgumentException>(() =>
            new UnresolvedTestResult(UnresolvedReason.UnsupportedRule, "attempted", sourceLocator));
    }
}
