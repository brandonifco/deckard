using Deckard.Rules.Resolution;

namespace Deckard.Rules.Edge;

/// <summary>
/// "Edge can also be gained in social situations, based on the attitudes the different
/// participants have toward each other, their bearing, and other factors." SR6 Core /
/// Game Concepts / Edge / Gaining Edge / printed p. 45 / PDF p. 46 -- which immediately
/// points at "the Social Edge table on p. 98" for the actual mechanic, rather than
/// printing a formula itself.
///
/// Printed p. 98 is outside this Issue's source packet (pp. 45-48) and has not been
/// transcribed into <c>Deckard.Data</c>: there is no table or formula available to model,
/// only a citation to one. Unlike <see cref="AttackDefenseRatingEdgeGain"/> -- whose
/// formula is fully printed within this packet and needed no additional data -- this
/// mechanic genuinely cannot be expressed without content this packet does not carry, so
/// it returns an explicit <see cref="UnresolvedReason.MissingRulesData"/> result (ADR
/// 0004) instead of quietly having no method at all, matching the stub pattern
/// <c>ExtendedTest</c>/<c>TeamworkTest</c> (Issue #4) established for a named mechanic
/// this engine does not yet resolve. It rolls no dice and mutates no <see cref="EdgePool"/>:
/// refusal happens before anything would be awarded.
/// </summary>
public static class SocialSituationEdgeGain
{
    public static UnresolvedTestResult Resolve() =>
        new(
            UnresolvedReason.MissingRulesData,
            attempted: "Edge gain from a social situation",
            sourceLocator: "SR6 Core / Game Concepts / Edge / Gaining Edge / printed p. 45 / "
                + "PDF p. 46, referencing the Social Edge table on printed p. 98 (outside this "
                + "Issue's packet)");
}
