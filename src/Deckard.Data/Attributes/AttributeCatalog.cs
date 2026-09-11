using System;
using System.Collections.Generic;
using System.Linq;

namespace Deckard.Data.Attributes;

/// <summary>
/// The attribute vocabulary grouped and ordered exactly as SR6 Core / Game Concepts /
/// Character Traits / Attributes / printed pp. 37-38 / PDF pp. 38-39 prints it: Physical
/// first, then Mental, then Special, each group's members in the order the book lists
/// them. This is the single source of truth both <see cref="Physical"/>/<see cref="Mental"/>/
/// <see cref="Special"/>/<see cref="All"/> and <see cref="CategoryOf"/> are built from, so
/// the grouping cannot drift out of sync with itself.
///
/// Every list here is ordered by construction (an array literal), never by enumerating a
/// keyed collection, per ADR 0006 (docs/decisions/0006-deterministic-ordering-conventions.md).
/// </summary>
public static class AttributeCatalog
{
    /// <summary>Body, Agility, Reaction, Strength -- printed p. 37 / PDF p. 38, in this order.</summary>
    public static IReadOnlyList<CharacterAttribute> Physical { get; } = new[]
    {
        CharacterAttribute.Body,
        CharacterAttribute.Agility,
        CharacterAttribute.Reaction,
        CharacterAttribute.Strength,
    };

    /// <summary>Willpower, Logic, Intuition, Charisma -- printed p. 38 / PDF p. 39, in this order.</summary>
    public static IReadOnlyList<CharacterAttribute> Mental { get; } = new[]
    {
        CharacterAttribute.Willpower,
        CharacterAttribute.Logic,
        CharacterAttribute.Intuition,
        CharacterAttribute.Charisma,
    };

    /// <summary>Edge, Magic, Resonance, Essence -- printed p. 38 / PDF p. 39, in this order.</summary>
    public static IReadOnlyList<CharacterAttribute> Special { get; } = new[]
    {
        CharacterAttribute.Edge,
        CharacterAttribute.Magic,
        CharacterAttribute.Resonance,
        CharacterAttribute.Essence,
    };

    /// <summary>
    /// All twelve attributes, Physical then Mental then Special, each group in its own
    /// printed order -- the complete closed vocabulary in one sequence.
    /// </summary>
    public static IReadOnlyList<CharacterAttribute> All { get; } =
        Physical.Concat(Mental).Concat(Special).ToArray();

    /// <summary>
    /// Which of the three printed groups <paramref name="attribute"/> belongs to.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attribute"/> is not one of the twelve named <see cref="CharacterAttribute"/>
    /// values -- an impossible value for well-typed callers (an out-of-range cast from
    /// <see langword="int"/>), so this is a programmer error, not an unresolved rule.
    /// </exception>
    public static AttributeCategory CategoryOf(CharacterAttribute attribute) => attribute switch
    {
        CharacterAttribute.Body or CharacterAttribute.Agility
            or CharacterAttribute.Reaction or CharacterAttribute.Strength => AttributeCategory.Physical,

        CharacterAttribute.Willpower or CharacterAttribute.Logic
            or CharacterAttribute.Intuition or CharacterAttribute.Charisma => AttributeCategory.Mental,

        CharacterAttribute.Edge or CharacterAttribute.Magic
            or CharacterAttribute.Resonance or CharacterAttribute.Essence => AttributeCategory.Special,

        _ => throw new ArgumentOutOfRangeException(
            nameof(attribute), attribute, "Unknown CharacterAttribute value."),
    };
}
