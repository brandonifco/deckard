namespace Deckard.Rules.Resolution;

/// <summary>
/// Extended tests are named alongside Simple and Opposed as one of "the three basic types
/// of tests" (SR6 Core / Game Concepts / Tests / printed p. 35 / PDF p. 36), and are
/// described further under Extended Tests (printed p. 36 / PDF p. 37): a dice pool and a
/// threshold like a Simple test, but resolved over multiple rolls -- the pool shrinking by
/// one die each roll -- accumulating hits across an interval of time until the threshold
/// is met.
///
/// That accumulation-over-time shape is not a variant <see cref="SimpleTest"/> can express
/// -- Issue #4's own Non-goals says so explicitly ("No ... Extended tests unless the
/// packet makes them trivially expressible ... file separate Issues"), reserving its
/// resolution shape for its own Issue rather than guessed at here. This type exists so a
/// caller asking Deckard to resolve an Extended test gets an explicit, structured
/// unresolved result (ADR 0004) instead of either nothing (no such method existing) or a
/// silently wrong answer (an Extended test quietly resolved as if it were Simple). It
/// rolls no dice and consumes no randomness: refusal happens before anything is drawn.
/// </summary>
public static class ExtendedTest
{
    public static UnresolvedTestResult Resolve() =>
        new(
            UnresolvedReason.UnsupportedRule,
            attempted: "Extended test",
            sourceLocator: "SR6 Core / Game Concepts / Tests / Extended Tests / printed p. 36 / PDF p. 37");
}
