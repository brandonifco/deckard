using Deckard.Rules.Resolution;

namespace Deckard.Rules.Tests.Resolution;

/// <summary>
/// Extended tests are a named variant of "Test" (printed p. 35 / PDF p. 36) this Issue
/// deliberately does not implement (Issue #4 Non-goals). <see cref="ExtendedTest"/> exists
/// so asking for one fails visibly (ADR 0004) instead of doing nothing or silently
/// resolving as a Simple test.
/// </summary>
public sealed class ExtendedTestTests
{
    [Fact]
    public void Resolve_is_explicitly_unresolved_as_an_unsupported_rule()
    {
        UnresolvedTestResult result = ExtendedTest.Resolve();

        Assert.Equal(UnresolvedReason.UnsupportedRule, result.Reason);
        Assert.Equal("Extended test", result.Attempted);
        Assert.Contains("printed p. 36", result.SourceLocator);
    }
}
