namespace Deckard.Rules.Resolution;

/// <summary>
/// The closed reason vocabulary for a result the engine cannot resolve, exactly as fixed
/// by ADR 0004 (docs/decisions/0004-unresolved-rule-taxonomy.md). That ADR named the five
/// values and deliberately deferred their concrete type shape to "the first Issue that
/// needs it" -- this Issue (#4), the first to produce a test result that can genuinely go
/// unresolved (<see cref="ExtendedTest"/>, <see cref="TeamworkTest"/>).
///
/// The vocabulary is closed by design: adding a sixth value means superseding ADR 0004,
/// not extending this enum quietly.
/// </summary>
public enum UnresolvedReason
{
    /// <summary>The book defines this; Deckard has not implemented it yet.</summary>
    UnsupportedRule,

    /// <summary>The source is genuinely ambiguous; an ADR is needed.</summary>
    RequiresInterpretation,

    /// <summary>Defined in a supplement or another edition; deliberately not implemented.</summary>
    OutsideCurrentScope,

    /// <summary>Both mechanics exist, but their combination is not resolved.</summary>
    UnsupportedInteraction,

    /// <summary>The algorithm exists; the structured data it needs is absent.</summary>
    MissingRulesData,
}
