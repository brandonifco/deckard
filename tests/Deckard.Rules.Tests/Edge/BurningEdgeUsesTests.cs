using Deckard.Rules.Edge;
using Deckard.Rules.Resolution;

namespace Deckard.Rules.Tests.Edge;

/// <summary>
/// Pins that <see cref="Smackdown"/> and <see cref="NotDeadYet"/> fail visibly (ADR 0004)
/// instead of silently having no way to ask about either named use of Burning Edge.
/// Both are the closed list of "uses of burning Edge" printed p. 48 / PDF p. 49.
/// </summary>
public sealed class BurningEdgeUsesTests
{
    [Fact]
    public void Smackdown_returns_unsupported_rule_citing_burning_edge()
    {
        UnresolvedTestResult result = Smackdown.Resolve();

        Assert.Equal(UnresolvedReason.UnsupportedRule, result.Reason);
        Assert.Equal("Smackdown (a use of Burning Edge)", result.Attempted);
        Assert.Contains("Burning Edge", result.SourceLocator);
        Assert.Contains("printed p. 48", result.SourceLocator);
    }

    [Fact]
    public void NotDeadYet_returns_unsupported_rule_citing_burning_edge()
    {
        UnresolvedTestResult result = NotDeadYet.Resolve();

        Assert.Equal(UnresolvedReason.UnsupportedRule, result.Reason);
        Assert.Equal("Not Dead Yet (a use of Burning Edge)", result.Attempted);
        Assert.Contains("Burning Edge", result.SourceLocator);
        Assert.Contains("printed p. 48", result.SourceLocator);
    }
}
