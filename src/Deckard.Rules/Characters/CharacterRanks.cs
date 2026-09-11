using System;
using System.Collections.Generic;
using Deckard.Data.Attributes;
using Deckard.Data.Skills;

namespace Deckard.Rules.Characters;

/// <summary>
/// A character's attribute and skill ranks -- the minimal state
/// <see cref="Deckard.Rules.Resolution.DicePoolAssembly"/> needs to build a dice pool from a
/// character rather than from loose integers. SR6 Core / Game Concepts / Tests / printed p.
/// 35 / PDF p. 36: to make a dice pool "you generally look at a skill ... and a linked
/// attribute ... Both of those have numerical values. Add those two numbers together."
///
/// Deliberately minimal, per Issue #69's non-goals: ranks only, nothing else a full
/// character sheet would eventually need. No modifiers of any kind (the same paragraph
/// defers those to the rest of the book -- half-building them here would be worse than not
/// having them), no specializations, no Expertise, no untrained/defaulting penalties, no
/// Edge interaction, no character creation, and no rank caps (printed p. 37 defers those to
/// p. 58, outside this Issue's source packet). A rank is simply whatever non-negative
/// integer the caller supplies for it -- this type does not judge whether it is a value SR6
/// character creation would actually allow.
///
/// Holds only the attributes and skills the caller actually supplies to the constructor --
/// not all twelve <see cref="CharacterAttribute"/> values or all nineteen <see cref="Skill"/>
/// values. <see cref="AttributeRank"/> and <see cref="SkillRank"/> throw for one that was
/// never supplied, rather than defaulting to 0: a supplied rank of exactly 0 (a valid rank;
/// no cap is encoded here) and "this character's rank for X was never recorded" are two
/// different facts, and collapsing them into the same return value would be exactly the
/// silent-default failure mode CLAUDE.md's fail-visibly invariant forbids. In particular, an
/// *untrained* skill -- one this holder was never given a rank for -- is not modeled as rank
/// 0; it simply cannot be assembled through this type, which matches the "no
/// untrained/defaulting penalties" non-goal rather than quietly half-implementing it.
///
/// The internal dictionaries are used only as keyed lookups (<c>TryGetValue</c>), never
/// enumerated into an observable sequence, so ADR 0006
/// (docs/decisions/0006-deterministic-ordering-conventions.md) has nothing to say about them.
/// </summary>
public sealed class CharacterRanks
{
    private readonly Dictionary<CharacterAttribute, int> _attributeRanks;
    private readonly Dictionary<Skill, int> _skillRanks;

    /// <summary>
    /// Builds a character's rank sheet from an attribute-rank map and a skill-rank map.
    /// Both maps are copied defensively, so mutating the caller's original dictionaries
    /// afterward has no effect on this instance.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="attributeRanks"/> or <paramref name="skillRanks"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A key in <paramref name="attributeRanks"/> is not one of the twelve
    /// <see cref="CharacterAttribute"/> values, a key in <paramref name="skillRanks"/> is
    /// not one of the nineteen <see cref="Skill"/> values, or any rank is negative.
    /// </exception>
    public CharacterRanks(
        IReadOnlyDictionary<CharacterAttribute, int> attributeRanks,
        IReadOnlyDictionary<Skill, int> skillRanks)
    {
        ArgumentNullException.ThrowIfNull(attributeRanks);
        ArgumentNullException.ThrowIfNull(skillRanks);

        _attributeRanks = new Dictionary<CharacterAttribute, int>(attributeRanks.Count);
        foreach ((CharacterAttribute attribute, int rank) in attributeRanks)
        {
            if (!Enum.IsDefined(attribute))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attributeRanks), attribute, "Unknown CharacterAttribute value.");
            }

            ArgumentOutOfRangeException.ThrowIfNegative(rank, nameof(attributeRanks));
            _attributeRanks[attribute] = rank;
        }

        _skillRanks = new Dictionary<Skill, int>(skillRanks.Count);
        foreach ((Skill skill, int rank) in skillRanks)
        {
            if (!Enum.IsDefined(skill))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(skillRanks), skill, "Unknown Skill value.");
            }

            ArgumentOutOfRangeException.ThrowIfNegative(rank, nameof(skillRanks));
            _skillRanks[skill] = rank;
        }
    }

    /// <summary>This character's rank in <paramref name="attribute"/>.</summary>
    /// <exception cref="ArgumentException">
    /// No rank was supplied for <paramref name="attribute"/> at construction.
    /// </exception>
    public int AttributeRank(CharacterAttribute attribute)
    {
        if (_attributeRanks.TryGetValue(attribute, out int rank))
        {
            return rank;
        }

        throw new ArgumentException(
            $"No rank recorded for attribute {attribute}.", nameof(attribute));
    }

    /// <summary>This character's rank in <paramref name="skill"/>.</summary>
    /// <exception cref="ArgumentException">
    /// No rank was supplied for <paramref name="skill"/> at construction.
    /// </exception>
    public int SkillRank(Skill skill)
    {
        if (_skillRanks.TryGetValue(skill, out int rank))
        {
            return rank;
        }

        throw new ArgumentException(
            $"No rank recorded for skill {skill}.", nameof(skill));
    }
}
