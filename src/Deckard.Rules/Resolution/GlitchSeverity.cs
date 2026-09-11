namespace Deckard.Rules.Resolution;

/// <summary>
/// Whether a rolled dice pool produced a glitch, and how severe it was.
///
/// SR6 Core / Game Concepts / Glitches and Critical Glitches / printed p. 44 / PDF p. 45:
/// a glitch is rolled when more than half the dice in a test come up 1; a critical glitch
/// is a glitch rolled with not a single hit. These three values are the entire vocabulary
/// the book defines here -- there is no partial or graduated glitch severity.
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
