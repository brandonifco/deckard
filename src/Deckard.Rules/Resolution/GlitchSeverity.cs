namespace Deckard.Rules.Resolution;

/// <summary>
/// Whether a rolled dice pool produced a glitch, and how severe it was.
///
/// SR6 Core / Game Concepts / Glitches and Critical Glitches / printed p. 44 / PDF p. 45:
/// more than half the dice rolled coming up 1s is a glitch; a glitch with no hits at all
/// on the test is a critical glitch. These three values are the entire vocabulary the book
/// defines here -- there is no partial or graduated glitch severity.
///
/// The glitch trigger's own denominator is not always the same set of dice.
/// <see cref="DicePoolRoll.Glitch"/> judges a single roll's own dice, which is also the
/// book's answer for any test that consists of one roll (Simple; each side of an Opposed
/// test). ADR 0012 (docs/decisions/0012-multi-roll-glitch-scoping-hybrid.md) settles a
/// different scope for a test spanning more than one roll -- the glitch trigger stays per
/// roll, but criticality stays scoped to the test, exactly as printed above, judged
/// against hits accumulated across every roll of the test. See that ADR and
/// <see cref="DicePoolRoll"/>'s remarks before assuming which scope applies.
/// </summary>
public enum GlitchSeverity
{
    /// <summary>At most half the dice rolled were 1s. No glitch.</summary>
    None = 0,

    /// <summary>More than half the dice rolled were 1s, and at least one hit was also rolled.</summary>
    Glitch,

    /// <summary>More than half the dice rolled were 1s, and no hit was rolled at all.</summary>
    CriticalGlitch,
}
