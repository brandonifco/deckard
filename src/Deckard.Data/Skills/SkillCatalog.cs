using System;
using System.Collections.Generic;
using Deckard.Data.Attributes;

namespace Deckard.Data.Skills;

/// <summary>
/// Each of the nineteen <see cref="Skill"/> values' printed name, linked attribute and
/// untrained flag, from SR6 Core / Skills / printed pp. 92-97 / PDF pp. 93-98. Every list
/// here is ordered by construction (an array literal), never by enumerating a keyed
/// collection, per ADR 0006 (docs/decisions/0006-deterministic-ordering-conventions.md).
///
/// <para>
/// <b>What "linked attribute" means here.</b> The chapter's own framing (SR6 Core /
/// Skills / printed pp. 92-97 / PDF pp. 93-98 -- not pinned to a single page within that
/// range; this packet's plain-text, two-column extraction does not place this particular
/// sentence on a page with confidence): "Skills also link to an attribute to form a dice
/// pool, and the primary linked attribute is listed with the skill. If there are other
/// attributes linked to the skill, they are listed as secondary attributes."
/// <see cref="LinkedAttributeOf"/> returns that primary attribute -- the one
/// <c>Skill + Attribute</c> dice-pool assembly (out of scope for this Issue; see #66's
/// non-goals) will use by default. Seven of the nineteen skills also print a secondary
/// attribute, and in every one of those seven cases the book ties it to a specific
/// circumstance, not a standing alternative: Astral's
/// Willpower is "for astral combat" only; Athletics' Strength is for tests offering extra
/// resistance; Biotech's Intuition is "rarely used ... applied when characters try to do
/// something that is not exactly by the books"; Electronics' Intuition is "for kludge
/// work"; Engineering's Intuition is for juryrigging and its Agility for lockpicking;
/// Influence's Logic is "when making a clear argument," mainly Negotiation; Perception's
/// Logic is for pattern recognition. Modeling which situation selects which secondary is
/// per-skill action-rule and dice-pool-assembly work, both explicitly out of this
/// Issue's scope -- so this catalog does not represent secondary attributes at all rather
/// than guessing which of several conditional secondaries a caller meant. Each is instead
/// cited in a comment on its <see cref="Skill"/> case below, so the citation trail exists
/// for whichever future Issue models conditional dice-pool assembly.
/// </para>
///
/// <para>
/// <b>Conjuring's label.</b> Every other skill's entry reads "Linked Attribute:" or
/// "Primary Linked Attribute:". Conjuring alone reads "Primary Attribute: Magic" --
/// one word short. Conjuring has no secondary attribute printed, and its position and
/// phrasing otherwise match every single-attribute skill (e.g. Cracking's "Linked
/// Attribute: Logic", Enchanting's "Linked Attribute: Magic"), so this reads as the
/// source's own typesetting inconsistency rather than a distinct mechanic -- Conjuring's
/// linked attribute is Magic. Flagged here rather than silently normalized away.
/// </para>
/// </summary>
public static class SkillCatalog
{
    /// <summary>
    /// All nineteen skills, in the order the "Skill List" box on printed p. 92 prints
    /// them -- the complete closed vocabulary in one sequence.
    /// </summary>
    public static IReadOnlyList<Skill> All { get; } = new[]
    {
        Skill.Astral,
        Skill.Athletics,
        Skill.Biotech,
        Skill.CloseCombat,
        Skill.Con,
        Skill.Conjuring,
        Skill.Cracking,
        Skill.Electronics,
        Skill.Enchanting,
        Skill.Engineering,
        Skill.ExoticWeapons,
        Skill.Firearms,
        Skill.Influence,
        Skill.Outdoors,
        Skill.Perception,
        Skill.Piloting,
        Skill.Sorcery,
        Skill.Stealth,
        Skill.Tasking,
    };

    /// <summary>
    /// The primary linked attribute SR6 Core prints for <paramref name="skill"/> -- see
    /// the "What 'linked attribute' means here" remarks on <see cref="SkillCatalog"/> for
    /// why only the primary is represented.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="skill"/> is not one of the nineteen named <see cref="Skill"/>
    /// values -- an impossible value for well-typed callers, so this is a programmer
    /// error, not an unresolved rule.
    /// </exception>
    public static CharacterAttribute LinkedAttributeOf(Skill skill) => skill switch
    {
        // Secondary: Willpower, for astral combat.
        Skill.Astral => CharacterAttribute.Intuition,

        // Secondary: Strength, for tests where extra resistance might be offered.
        Skill.Athletics => CharacterAttribute.Agility,

        // The "Biotech" heading itself is printed p. 94; every vocabulary line
        // (Specialization, Untrained, and this Primary/Secondary pair) is printed
        // p. 95, on the far side of that page's own footer in the extracted text --
        // the same page-break-splits-a-skill's-block pattern as Electronics below,
        // just without an intervening sidebar table to make the gap obvious.
        // Secondary: Intuition, rarely used, for off-the-books attempts.
        Skill.Biotech => CharacterAttribute.Logic,

        Skill.CloseCombat => CharacterAttribute.Agility,
        Skill.Con => CharacterAttribute.Charisma,

        // Printed "Primary Attribute: Magic" -- see the Conjuring remarks above.
        Skill.Conjuring => CharacterAttribute.Magic,

        Skill.Cracking => CharacterAttribute.Logic,

        // Untrained/Specialization printed p. 95; this Primary/Secondary pair is
        // printed p. 96 -- the source's own Build/Repair Thresholds sidebar table
        // (Engineering's, not Electronics') and the page break both separate this
        // skill's own two lines in the extracted text. Confirmed as Electronics by
        // the resuming prose ("This is the other side of Matrix work, the legal
        // side ... Electronics is the skill for you"), not assumed from position.
        // Secondary: Intuition, for kludge work.
        Skill.Electronics => CharacterAttribute.Logic,

        Skill.Enchanting => CharacterAttribute.Magic,

        // Secondary: Intuition, for juryrigging; Agility, for lockpicking.
        Skill.Engineering => CharacterAttribute.Logic,

        Skill.ExoticWeapons => CharacterAttribute.Agility,
        Skill.Firearms => CharacterAttribute.Agility,

        // Secondary: Logic, when making a clear argument -- mainly Negotiation,
        // possibly some Leadership situations.
        Skill.Influence => CharacterAttribute.Charisma,

        Skill.Outdoors => CharacterAttribute.Intuition,

        // Secondary: Logic, for pattern recognition.
        Skill.Perception => CharacterAttribute.Intuition,

        Skill.Piloting => CharacterAttribute.Reaction,
        Skill.Sorcery => CharacterAttribute.Magic,
        Skill.Stealth => CharacterAttribute.Agility,
        Skill.Tasking => CharacterAttribute.Resonance,

        _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, "Unknown Skill value."),
    };

    /// <summary>
    /// Whether SR6 Core allows <paramref name="skill"/> to be rolled untrained -- printed
    /// as each skill's own "Untrained: Yes/No" line, cross-checked against the "Skill
    /// List" box's "Untrained?" column on printed p. 92 (both agree for all nineteen).
    /// Remember that using a skill untrained means rolling (linked attribute - 1); that
    /// arithmetic is dice-pool assembly and out of this Issue's scope.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="skill"/> is not one of the nineteen named <see cref="Skill"/>
    /// values -- an impossible value for well-typed callers, so this is a programmer
    /// error, not an unresolved rule.
    /// </exception>
    public static bool CanBeUsedUntrained(Skill skill) => skill switch
    {
        Skill.Astral => false,
        Skill.Athletics => true,
        Skill.Biotech => false,
        Skill.CloseCombat => true,
        Skill.Con => true,
        Skill.Conjuring => false,
        Skill.Cracking => false,
        Skill.Electronics => true,
        Skill.Enchanting => false,
        Skill.Engineering => true,
        Skill.ExoticWeapons => false,
        Skill.Firearms => true,
        Skill.Influence => true,
        Skill.Outdoors => true,
        Skill.Perception => true,
        Skill.Piloting => true,
        Skill.Sorcery => false,
        Skill.Stealth => true,
        Skill.Tasking => false,

        _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, "Unknown Skill value."),
    };
}
