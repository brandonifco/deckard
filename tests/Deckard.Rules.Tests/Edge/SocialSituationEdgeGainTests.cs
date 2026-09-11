using Deckard.Rules.Edge;
using Deckard.Rules.Resolution;

namespace Deckard.Rules.Tests.Edge;

/// <summary>
/// Pins that <see cref="SocialSituationEdgeGain"/> fails visibly (ADR 0004) instead of
/// silently having no way to ask about Social Edge gain at all. See the type's doc
/// comment for why <see cref="UnresolvedReason.MissingRulesData"/> is the correct reason:
/// the formula lives on printed p. 98, outside this Issue's packet (pp. 45-48).
/// </summary>
public sealed class SocialSituationEdgeGainTests
{
    [Fact]
    public void Resolve_returns_missing_rules_data_citing_the_social_edge_table()
    {
        UnresolvedTestResult result = SocialSituationEdgeGain.Resolve();

        Assert.Equal(UnresolvedReason.MissingRulesData, result.Reason);
        Assert.Equal("Edge gain from a social situation", result.Attempted);
        Assert.Contains("Social Edge table", result.SourceLocator);
        Assert.Contains("printed p. 98", result.SourceLocator);
    }
}
