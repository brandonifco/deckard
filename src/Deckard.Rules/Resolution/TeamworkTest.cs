namespace Deckard.Rules.Resolution;

/// <summary>
/// Teamwork tests (SR6 Core / Game Concepts / Tests / Teamwork Tests / printed p. 36 /
/// PDF p. 37) are not one of the "three basic types of tests" named on printed p. 35 --
/// they complement another test rather than standing on their own. That composite shape
/// needs its own reasoning, exactly as Issue #4's Non-goals anticipates, reserving it
/// for a separate Issue rather than guessed at here.
///
/// This type exists so a caller asking Deckard to resolve a Teamwork test gets an
/// explicit, structured unresolved result (ADR 0004) instead of nothing (no such method
/// existing) or a silently wrong answer. It rolls no dice and consumes no randomness:
/// refusal happens before anything is drawn.
/// </summary>
public static class TeamworkTest
{
    public static UnresolvedTestResult Resolve() =>
        new(
            UnresolvedReason.UnsupportedRule,
            attempted: "Teamwork test",
            sourceLocator: "SR6 Core / Game Concepts / Tests / Teamwork Tests / printed p. 36 / PDF p. 37");
}
