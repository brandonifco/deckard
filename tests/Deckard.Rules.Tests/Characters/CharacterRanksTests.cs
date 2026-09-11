using System.Collections.Generic;
using Deckard.Data.Attributes;
using Deckard.Data.Skills;
using Deckard.Rules.Characters;

namespace Deckard.Rules.Tests.Characters;

/// <summary>
/// Pins <see cref="CharacterRanks"/>: the minimal holder for a character's attribute and
/// skill ranks that Issue #69 introduces so dice pool assembly
/// (<see cref="Deckard.Rules.Resolution.DicePoolAssembly"/>) can work from a character
/// instead of loose integers. Not itself a sourced mechanic -- there is no printed formula
/// to pin here -- so these tests cover the type's own contract: what it stores, what it
/// rejects, and the "recorded zero" vs. "never recorded" distinction its doc comments call
/// out as the reason it throws instead of defaulting.
/// </summary>
public sealed class CharacterRanksTests
{
    private static readonly IReadOnlyDictionary<CharacterAttribute, int> EmptyAttributes =
        new Dictionary<CharacterAttribute, int>();

    private static readonly IReadOnlyDictionary<Skill, int> EmptySkills =
        new Dictionary<Skill, int>();

    // ------------------------------------------------------------------- argument validation

    [Fact]
    public void Constructor_throws_on_null_attribute_ranks()
    {
        Assert.Throws<ArgumentNullException>(() => new CharacterRanks(null!, EmptySkills));
    }

    [Fact]
    public void Constructor_throws_on_null_skill_ranks()
    {
        Assert.Throws<ArgumentNullException>(() => new CharacterRanks(EmptyAttributes, null!));
    }

    [Fact]
    public void Constructor_throws_on_negative_attribute_rank()
    {
        var attributes = new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Body] = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CharacterRanks(attributes, EmptySkills));
    }

    [Fact]
    public void Constructor_throws_on_negative_skill_rank()
    {
        var skills = new Dictionary<Skill, int> { [Skill.Firearms] = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CharacterRanks(EmptyAttributes, skills));
    }

    [Fact]
    public void Constructor_throws_on_an_undefined_attribute_value()
    {
        var undefined = (CharacterAttribute)(-1);
        var attributes = new Dictionary<CharacterAttribute, int> { [undefined] = 3 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CharacterRanks(attributes, EmptySkills));
    }

    [Fact]
    public void Constructor_throws_on_an_undefined_skill_value()
    {
        var undefined = (Skill)(-1);
        var skills = new Dictionary<Skill, int> { [undefined] = 3 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CharacterRanks(EmptyAttributes, skills));
    }

    // ------------------------------------------------------------------------- AttributeRank

    [Fact]
    public void AttributeRank_returns_the_recorded_rank()
    {
        var attributes = new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Agility] = 4 };
        var character = new CharacterRanks(attributes, EmptySkills);

        Assert.Equal(4, character.AttributeRank(CharacterAttribute.Agility));
    }

    [Fact]
    public void AttributeRank_throws_for_an_attribute_that_was_never_supplied()
    {
        var character = new CharacterRanks(EmptyAttributes, EmptySkills);

        Assert.Throws<ArgumentException>(() => character.AttributeRank(CharacterAttribute.Body));
    }

    [Fact]
    public void AttributeRank_of_zero_is_returned_not_confused_with_unrecorded()
    {
        // The doc comment's central claim: a rank explicitly recorded as 0 is a real,
        // distinct fact from "this attribute was never supplied" (the previous test). If
        // the implementation collapsed the two -- e.g. by using the indexer's default(int)
        // for a missing key instead of TryGetValue -- this test could not tell the
        // difference from that bug; the previous test is what makes it meaningful.
        var attributes = new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Essence] = 0 };
        var character = new CharacterRanks(attributes, EmptySkills);

        Assert.Equal(0, character.AttributeRank(CharacterAttribute.Essence));
    }

    // ----------------------------------------------------------------------------- SkillRank

    [Fact]
    public void SkillRank_returns_the_recorded_rank()
    {
        var skills = new Dictionary<Skill, int> { [Skill.Stealth] = 5 };
        var character = new CharacterRanks(EmptyAttributes, skills);

        Assert.Equal(5, character.SkillRank(Skill.Stealth));
    }

    [Fact]
    public void SkillRank_throws_for_a_skill_that_was_never_supplied()
    {
        var character = new CharacterRanks(EmptyAttributes, EmptySkills);

        Assert.Throws<ArgumentException>(() => character.SkillRank(Skill.Stealth));
    }

    [Fact]
    public void SkillRank_of_zero_is_returned_not_confused_with_unrecorded()
    {
        var skills = new Dictionary<Skill, int> { [Skill.Con] = 0 };
        var character = new CharacterRanks(EmptyAttributes, skills);

        Assert.Equal(0, character.SkillRank(Skill.Con));
    }

    // ------------------------------------------------------------------- defensive copying

    [Fact]
    public void Mutating_the_caller_s_dictionary_after_construction_does_not_change_the_character()
    {
        var attributes = new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Logic] = 2 };
        var character = new CharacterRanks(attributes, EmptySkills);

        attributes[CharacterAttribute.Logic] = 99;

        Assert.Equal(2, character.AttributeRank(CharacterAttribute.Logic));
    }

    // --------------------------------------------------------------- several ranks at once

    [Fact]
    public void Holds_several_attribute_and_skill_ranks_independently()
    {
        var attributes = new Dictionary<CharacterAttribute, int>
        {
            [CharacterAttribute.Agility] = 4,
            [CharacterAttribute.Logic] = 3,
            [CharacterAttribute.Charisma] = 1,
        };
        var skills = new Dictionary<Skill, int>
        {
            [Skill.Firearms] = 5,
            [Skill.Cracking] = 2,
        };
        var character = new CharacterRanks(attributes, skills);

        Assert.Equal(4, character.AttributeRank(CharacterAttribute.Agility));
        Assert.Equal(3, character.AttributeRank(CharacterAttribute.Logic));
        Assert.Equal(1, character.AttributeRank(CharacterAttribute.Charisma));
        Assert.Equal(5, character.SkillRank(Skill.Firearms));
        Assert.Equal(2, character.SkillRank(Skill.Cracking));
    }
}
