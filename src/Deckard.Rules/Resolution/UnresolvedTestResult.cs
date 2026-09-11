namespace Deckard.Rules.Resolution;

/// <summary>
/// An explicit "this engine cannot resolve this" outcome, per ADR 0004
/// (docs/decisions/0004-unresolved-rule-taxonomy.md) and docs/architecture.md's
/// "Unresolved rules" section: never nothing, never a default, never a guess. A caller
/// that receives one knows exactly what was attempted, why it was not resolved, and
/// where in the source the real rule lives (when one exists to point at).
///
/// This is the first concrete shape given to ADR 0004's taxonomy; the ADR fixed the
/// five-value <see cref="UnresolvedReason"/> vocabulary and left the carrying type to
/// whichever Issue needed it first.
/// </summary>
public sealed class UnresolvedTestResult
{
    /// <summary>Which of the closed ADR 0004 reasons this result could not be resolved for.</summary>
    public UnresolvedReason Reason { get; }

    /// <summary>What the caller asked the engine to resolve, in plain language.</summary>
    public string Attempted { get; }

    /// <summary>
    /// Where the underlying rule lives, cited as "SR6 Core / &lt;section&gt; / printed p. X
    /// / PDF p. Y" per CLAUDE.md's provenance requirement -- even when unresolved, the
    /// locator is what makes the gap actionable rather than mysterious.
    /// </summary>
    public string SourceLocator { get; }

    public UnresolvedTestResult(UnresolvedReason reason, string attempted, string sourceLocator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attempted);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLocator);

        Reason = reason;
        Attempted = attempted;
        SourceLocator = sourceLocator;
    }
}
