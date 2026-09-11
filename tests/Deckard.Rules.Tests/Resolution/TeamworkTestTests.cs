using Deckard.Rules.Resolution;

namespace Deckard.Rules.Tests.Resolution;

/// <summary>
/// Teamwork tests are a named variant of "Test" (printed p. 36 / PDF p. 37) this Issue
/// deliberately does not implement (Issue #4 Non-goals). <see cref="TeamworkTest"/> exists
/// so asking for one fails visibly (ADR 0004) instead of doing nothing.
/// </summary>
public sealed class TeamworkTestTests
{
    [Fact]
    public void Resolve_is_explicitly_unresolved_as_an_unsupported_rule()
    {
        UnresolvedTestResult result = TeamworkTest.Resolve();

        Assert.Equal(UnresolvedReason.UnsupportedRule, result.Reason);
        Assert.Equal("Teamwork test", result.Attempted);
        Assert.Contains("printed p. 36", result.SourceLocator);
    }
}
