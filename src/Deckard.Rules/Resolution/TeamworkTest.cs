namespace Deckard.Rules.Resolution;

/// <summary>
/// Teamwork tests (SR6 Core / Game Concepts / Tests / Teamwork Tests / printed p. 36 /
/// PDF p. 37) are not one of the "three basic types of tests" named on printed p. 35 --
/// they complement another test: helpers roll first, and their hits become extra dice
/// added to a leader's pool, capped by the leader's applicable skill rank. That composite,
/// multi-roll, capped-pool-augmentation shape needs its own reasoning, exactly as Issue
/// #4's Non-goals anticipates ("No Teamwork ... tests unless the packet makes them
/// trivially expressible ... file separate Issues").
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
