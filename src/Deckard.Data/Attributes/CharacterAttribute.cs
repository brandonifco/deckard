namespace Deckard.Data.Attributes;

/// <summary>
/// The twelve named attributes SR6 Core / Game Concepts / Character Traits / Attributes /
/// printed p. 37 / PDF p. 38 defines: "the core stats for a character ... the abilities
/// characters were born with and developed through hard work, the tools they combine with
/// individual skills to accomplish tasks." This is the closed vocabulary itself, exactly as
/// printed -- no attribute outside this list exists in SR6 Core, and none of these twelve
/// is omitted. See <see cref="AttributeCatalog"/> for the printed Physical/Mental/Special
/// grouping and iteration order.
///
/// Not named <c>Attribute</c>: that identifier collides with <see cref="System.Attribute"/>,
/// the CLR's custom-attribute base type, which every C# file has in scope implicitly.
///
/// Deliberately excluded, per Issue #64's non-goals and printed p. 37's own forward
/// reference to the Character Creation chapter (p. 58, outside this packet's pp. 37-38 and
/// outside this Issue's scope): the numeric rank each attribute may hold, the +4 cap on an
/// *adjusted* attribute (which depends on attribute modifiers from gear/magic/augmentation
/// -- also out of scope here), and Essence's rule that a fractional Essence loss costs
/// Magic (an attribute-interaction effect, not vocabulary). None of these is a defaulted or
/// guessed value; they are simply not part of what this Issue asks the vocabulary to state.
/// </summary>
public enum CharacterAttribute
{
    // Physical -- printed p. 37 / PDF p. 38, printed in this order.
    Body,
    Agility,
    Reaction,
    Strength,

    // Mental -- printed p. 38 / PDF p. 39, printed in this order.
    Willpower,
    Logic,
    Intuition,
    Charisma,

    // Special -- printed p. 38 / PDF p. 39, printed in this order.
    Edge,
    Magic,
    Resonance,
    Essence,
}
