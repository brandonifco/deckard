namespace Deckard.Rules.Resolution;

/// <summary>
/// Extended tests are named as one of "the three basic types of tests" alongside Simple
/// and Opposed (SR6 Core / Game Concepts / Tests / printed p. 35 / PDF p. 36), and are
/// described further under Extended Tests (printed p. 36 / PDF p. 37). Their resolution
/// shape is not a variant <see cref="SimpleTest"/> can express -- Issue #4's own
/// Non-goals defers Extended tests explicitly, reserving their design for a separate
/// Issue with its own reasoning rather than guessed at here.
///
/// This type exists so a caller asking Deckard to resolve an Extended test gets an
/// explicit, structured unresolved result (ADR 0004) instead of either nothing (no such
/// method existing) or a silently wrong answer (an Extended test quietly resolved as if
/// it were Simple). It rolls no dice and consumes no randomness: refusal happens before
/// anything is drawn.
/// </summary>
public static class ExtendedTest
{
    public static UnresolvedTestResult Resolve() =>
        new(
            UnresolvedReason.UnsupportedRule,
            attempted: "Extended test",
            sourceLocator: "SR6 Core / Game Concepts / Tests / Extended Tests / printed p. 36 / PDF p. 37");
}
