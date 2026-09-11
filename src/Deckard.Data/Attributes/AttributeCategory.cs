namespace Deckard.Data.Attributes;

/// <summary>
/// The three attribute groups SR6 Core / Game Concepts / Character Traits / Attributes /
/// printed p. 37 / PDF p. 38 names directly: "Attributes come in three groups: Physical,
/// Mental, and Special." No fourth group exists in the source; this is the complete,
/// closed vocabulary. See <see cref="AttributeCatalog"/> for which
/// <see cref="CharacterAttribute"/> belongs to each.
/// </summary>
public enum AttributeCategory
{
    /// <summary>Body, Agility, Reaction, Strength. Printed p. 37 / PDF p. 38.</summary>
    Physical,

    /// <summary>Willpower, Logic, Intuition, Charisma. Printed p. 38 / PDF p. 39.</summary>
    Mental,

    /// <summary>Edge, Magic, Resonance, Essence. Printed p. 38 / PDF p. 39.</summary>
    Special,
}
