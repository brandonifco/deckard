using System;
using Deckard.Data.Attributes;
using Deckard.Data.Skills;
using Deckard.Rules.Characters;

namespace Deckard.Rules.Resolution;

/// <summary>
/// Builds a dice pool size from a character's ranks -- the join between vocabulary
/// (<see cref="Skill"/>, <see cref="CharacterAttribute"/>, <see cref="CharacterRanks"/>) and
/// mechanics (<see cref="SimpleTest"/>, <see cref="OpposedTest"/>) that neither side by
/// itself supplied. SR6 Core / Game Concepts / Tests / printed p. 35 / PDF p. 36: "The
/// number of dice you roll is called a dice pool. To make it, you generally look at a skill
/// ... and a linked attribute .... Add those two numbers together—that's how many dice you
/// roll. Some rolls will simply add two attributes, or will count the same attribute
/// twice."
///
/// Every method here returns a plain <see cref="int"/> -- the same shape
/// <see cref="SimpleTest.Resolve"/>, <see cref="OpposedTest.Resolve"/> and
/// <see cref="DicePoolRoll.Roll"/> already accept as their <c>dicePool</c> parameter, so an
/// assembled pool feeds them unchanged, per Issue #69's acceptance criteria.
///
/// <see cref="FromAttributes"/> models both variants the book names, in one shape: pass two
/// different attributes for "add two attributes", or the same attribute twice for "count
/// the same attribute twice" -- the arithmetic is identical either way, so both are the same
/// call shape. The book names these variants without saying which rolls use which (Issue
/// #69's known ambiguity), so this type does not decide that; it is the caller's job to pick
/// the shape the roll they are building calls for.
///
/// No modifiers, specializations, Expertise, untrained/defaulting penalties, Edge
/// interaction, or rank caps -- all out of this Issue's scope; see <see cref="CharacterRanks"/>'s
/// own remarks for why each is left out rather than half-built.
/// </summary>
public static class DicePoolAssembly
{
    /// <summary>
    /// <paramref name="character"/>'s rank in <paramref name="skill"/> plus their rank in
    /// that skill's printed linked attribute
    /// (<see cref="SkillCatalog.LinkedAttributeOf(Skill)"/>). The linked attribute is read
    /// from the catalog, never taken as a parameter here, so a caller cannot pair a skill
    /// with an attribute that is not its linked one through this method.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="character"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="skill"/> is not one of the nineteen named <see cref="Skill"/> values.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="character"/> has no recorded rank for <paramref name="skill"/> or for
    /// its linked attribute.
    /// </exception>
    public static int FromSkill(CharacterRanks character, Skill skill)
    {
        ArgumentNullException.ThrowIfNull(character);

        CharacterAttribute linkedAttribute = SkillCatalog.LinkedAttributeOf(skill);
        return character.SkillRank(skill) + character.AttributeRank(linkedAttribute);
    }

    /// <summary>
    /// <paramref name="character"/>'s rank in <paramref name="first"/> plus their rank in
    /// <paramref name="second"/>. Pass two different attributes for the printed "add two
    /// attributes" variant, or the same attribute for both parameters for the printed "count
    /// the same attribute twice" variant.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="character"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="character"/> has no recorded rank for <paramref name="first"/> or for
    /// <paramref name="second"/>.
    /// </exception>
    public static int FromAttributes(
        CharacterRanks character, CharacterAttribute first, CharacterAttribute second)
    {
        ArgumentNullException.ThrowIfNull(character);

        return character.AttributeRank(first) + character.AttributeRank(second);
    }
}
