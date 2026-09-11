namespace Deckard.Core.Replay;

/// <summary>
/// The complete replay compatibility identity -- see ADR 0005. CLAUDE.md and
/// docs/architecture.md state the engine's invariant as "same rules version + same initial
/// state + same random seed/state + same ordered decisions = same outcomes and the same
/// ordered event/roll history"; this type is what makes "same rules version" checkable
/// rather than undefined prose.
///
/// Two identities are equal only when every component matches.
/// <see cref="RandomAlgorithm"/>, <see cref="Ruleset"/>, <see cref="ReplaySchema"/>, and
/// <see cref="SourceBaseline"/> each change for a different, independent reason (ADR
/// 0005), so a mismatch in any single one is a genuine incompatibility -- not something to
/// average away or ignore.
///
/// This type has no serialization, persistence, or replay-execution behaviour by design:
/// it exists to be compared, not stored or recorded. See ADR 0005's non-goals.
/// </summary>
public readonly record struct ReplayCompatibilityIdentity(
    RandomAlgorithmId RandomAlgorithm,
    RulesetVersion Ruleset,
    ReplaySchemaVersion ReplaySchema,
    SourceBaselineId SourceBaseline);
