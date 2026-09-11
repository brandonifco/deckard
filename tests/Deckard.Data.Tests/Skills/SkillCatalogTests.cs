using System;
using System.Collections.Generic;
using System.Linq;
using Deckard.Data.Attributes;
using Deckard.Data.Skills;
using Xunit;

namespace Deckard.Data.Tests.Skills;

/// <summary>
/// Pins the skill vocabulary against SR6 Core / Skills / printed pp. 92-97 / PDF pp.
/// 93-98: the printed "Skill List" is a finite, closed set of nineteen skills, and this
/// asserts the whole set -- not a sample -- with every skill's linked attribute and
/// untrained flag checked individually, per Issue #66's required evidence.
///
/// Completeness was confirmed against the packet's own cross-check: every skill's entry
/// prints its attribute under a "Linked Attribute:" label -- either one line ("Linked
/// Attribute: X") for a skill with no printed secondary, or two ("Primary Linked
/// Attribute: X" / "Secondary Linked Attribute: Y") for one that has one. Counting raw
/// occurrences of the substring "Linked Attribute" across the packet gives 25: seven
/// skills print both a primary and a secondary line (Astral, Athletics, Biotech,
/// Electronics, Engineering, Influence, Perception -- 7 x 2 = 14 lines, Electronics'
/// own pair separated in the extracted text by an unrelated sidebar table and a page
/// break, confirmed as Electronics' own by its resuming prose rather than assumed from
/// position) and eleven print a single line (CloseCombat, Con, Cracking, Enchanting,
/// ExoticWeapons, Firearms, Outdoors, Piloting, Sorcery, Stealth, Tasking -- 11 x 1 = 11
/// lines). 14 + 11 = 25, covering eighteen of the nineteen skills. Conjuring is the
/// nineteenth: its attribute (Magic) is printed under the label "Primary Attribute"
/// rather than "Primary Linked Attribute" -- one word short, and the only skill entry
/// worded that way -- so it does not contribute to the count of 25, which is otherwise
/// exactly the packet's own stated cross-check total rather than an inflated count from
/// a heading or sidebar.
/// </summary>
public sealed class SkillCatalogTests
{
    // ------------------------------------------------------------------- the whole set

    [Fact]
    public void All_is_exactly_the_nineteen_printed_skills_in_printed_order()
    {
        Assert.Equal(
            new[]
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
            },
            SkillCatalog.All);
    }

    [Fact]
    public void All_contains_exactly_the_nineteen_printed_skills_no_extra_none_missing()
    {
        // The printed Skill List box has nineteen entries. Comparing as sets (not order)
        // against the enum's own full value set catches a skill declared on the enum but
        // left out of All, or vice versa.
        var expected = new HashSet<Skill>(Enum.GetValues<Skill>());
        var actual = new HashSet<Skill>(SkillCatalog.All);

        Assert.Equal(19, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void All_has_no_duplicate_skill()
    {
        Assert.Equal(SkillCatalog.All.Count, SkillCatalog.All.Distinct().Count());
    }

    // ---------------------------------------------------------------- LinkedAttributeOf

    [Theory]
    [InlineData(Skill.Astral, CharacterAttribute.Intuition)]
    [InlineData(Skill.Athletics, CharacterAttribute.Agility)]
    [InlineData(Skill.Biotech, CharacterAttribute.Logic)]
    [InlineData(Skill.CloseCombat, CharacterAttribute.Agility)]
    [InlineData(Skill.Con, CharacterAttribute.Charisma)]
    [InlineData(Skill.Conjuring, CharacterAttribute.Magic)]
    [InlineData(Skill.Cracking, CharacterAttribute.Logic)]
    [InlineData(Skill.Electronics, CharacterAttribute.Logic)]
    [InlineData(Skill.Enchanting, CharacterAttribute.Magic)]
    [InlineData(Skill.Engineering, CharacterAttribute.Logic)]
    [InlineData(Skill.ExoticWeapons, CharacterAttribute.Agility)]
    [InlineData(Skill.Firearms, CharacterAttribute.Agility)]
    [InlineData(Skill.Influence, CharacterAttribute.Charisma)]
    [InlineData(Skill.Outdoors, CharacterAttribute.Intuition)]
    [InlineData(Skill.Perception, CharacterAttribute.Intuition)]
    [InlineData(Skill.Piloting, CharacterAttribute.Reaction)]
    [InlineData(Skill.Sorcery, CharacterAttribute.Magic)]
    [InlineData(Skill.Stealth, CharacterAttribute.Agility)]
    [InlineData(Skill.Tasking, CharacterAttribute.Resonance)]
    public void LinkedAttributeOf_reports_the_printed_primary_attribute_for_every_skill(
        Skill skill, CharacterAttribute expectedAttribute)
    {
        Assert.Equal(expectedAttribute, SkillCatalog.LinkedAttributeOf(skill));
    }

    [Fact]
    public void LinkedAttributeOf_covers_every_skill_in_All()
    {
        // Exhaustive lookup, not a sample: every member of the closed set resolves
        // without throwing.
        foreach (var skill in SkillCatalog.All)
        {
            _ = SkillCatalog.LinkedAttributeOf(skill);
        }
    }

    [Fact]
    public void LinkedAttributeOf_throws_on_an_undefined_skill_value()
    {
        var undefined = (Skill)(-1);

        Assert.Throws<ArgumentOutOfRangeException>(() => SkillCatalog.LinkedAttributeOf(undefined));
    }

    // -------------------------------------------------------------- CanBeUsedUntrained

    [Theory]
    [InlineData(Skill.Astral, false)]
    [InlineData(Skill.Athletics, true)]
    [InlineData(Skill.Biotech, false)]
    [InlineData(Skill.CloseCombat, true)]
    [InlineData(Skill.Con, true)]
    [InlineData(Skill.Conjuring, false)]
    [InlineData(Skill.Cracking, false)]
    [InlineData(Skill.Electronics, true)]
    [InlineData(Skill.Enchanting, false)]
    [InlineData(Skill.Engineering, true)]
    [InlineData(Skill.ExoticWeapons, false)]
    [InlineData(Skill.Firearms, true)]
    [InlineData(Skill.Influence, true)]
    [InlineData(Skill.Outdoors, true)]
    [InlineData(Skill.Perception, true)]
    [InlineData(Skill.Piloting, true)]
    [InlineData(Skill.Sorcery, false)]
    [InlineData(Skill.Stealth, true)]
    [InlineData(Skill.Tasking, false)]
    public void CanBeUsedUntrained_reports_the_printed_flag_for_every_skill(
        Skill skill, bool expectedUntrained)
    {
        Assert.Equal(expectedUntrained, SkillCatalog.CanBeUsedUntrained(skill));
    }

    [Fact]
    public void CanBeUsedUntrained_covers_every_skill_in_All()
    {
        foreach (var skill in SkillCatalog.All)
        {
            _ = SkillCatalog.CanBeUsedUntrained(skill);
        }
    }

    [Fact]
    public void CanBeUsedUntrained_throws_on_an_undefined_skill_value()
    {
        var undefined = (Skill)(-1);

        Assert.Throws<ArgumentOutOfRangeException>(() => SkillCatalog.CanBeUsedUntrained(undefined));
    }

    // ------------------------------------------------------- cross-checks between tables

    [Fact]
    public void Exactly_eleven_skills_can_be_used_untrained_and_eight_cannot()
    {
        // The "Skill List" box's Untrained? column (printed p. 92) reads, in printed
        // order: No, Yes, No, Yes, Yes, No, No, Yes, No, Yes, No, Yes, Yes, Yes, Yes,
        // Yes, No, Yes, No -- eleven Yes, eight No. Cross-checks the per-skill Theory
        // above against the chapter's own summary table rather than only against itself.
        var untrainedCount = SkillCatalog.All.Count(SkillCatalog.CanBeUsedUntrained);

        Assert.Equal(11, untrainedCount);
        Assert.Equal(8, SkillCatalog.All.Count - untrainedCount);
    }
}
