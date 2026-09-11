using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deckard.Data.Attributes;
using Deckard.Data.Skills;
using Deckard.Rules.Characters;
using Deckard.Rules.Resolution;

namespace Deckard.Rules.Tests.Resolution;

/// <summary>
/// Pins <see cref="DicePoolAssembly"/> against SR6 Core / Game Concepts / Tests / printed p.
/// 35 / PDF p. 36: a dice pool is a skill's rank plus its linked attribute's rank, or the
/// sum of two attribute ranks (the same attribute counted twice being a special case of
/// that sum). See <see cref="CharacterRanks"/> for the rank holder these assemblies read
/// from, and <see cref="DicePoolAssemblyResolutionTests"/> for the end-to-end path into
/// <see cref="SimpleTest"/>/<see cref="OpposedTest"/>.
/// </summary>
public sealed class DicePoolAssemblyTests
{
    private static CharacterRanks Character(
        IReadOnlyDictionary<CharacterAttribute, int>? attributes = null,
        IReadOnlyDictionary<Skill, int>? skills = null) =>
        new(attributes ?? new Dictionary<CharacterAttribute, int>(),
            skills ?? new Dictionary<Skill, int>());

    // ------------------------------------------------------------------------------ FromSkill

    [Fact]
    public void FromSkill_throws_on_null_character()
    {
        Assert.Throws<ArgumentNullException>(() => DicePoolAssembly.FromSkill(null!, Skill.Firearms));
    }

    [Fact]
    public void FromSkill_throws_on_an_undefined_skill_value()
    {
        var character = Character();
        var undefined = (Skill)(-1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => DicePoolAssembly.FromSkill(character, undefined));
    }

    [Fact]
    public void FromSkill_throws_when_the_skill_rank_was_never_recorded()
    {
        var character = Character(
            attributes: new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Agility] = 4 });

        Assert.Throws<ArgumentException>(() => DicePoolAssembly.FromSkill(character, Skill.Firearms));
    }

    [Fact]
    public void FromSkill_throws_when_the_linked_attribute_rank_was_never_recorded()
    {
        var character = Character(
            skills: new Dictionary<Skill, int> { [Skill.Firearms] = 5 });

        Assert.Throws<ArgumentException>(() => DicePoolAssembly.FromSkill(character, Skill.Firearms));
    }

    [Theory]
    // Each pair is a skill and its printed linked attribute (SkillCatalogTests pins the
    // full mapping); these five were chosen to cover five different linked attributes, not
    // to retest SkillCatalog.LinkedAttributeOf itself.
    [InlineData(Skill.Firearms, CharacterAttribute.Agility, 4, 3, 7)]
    [InlineData(Skill.Cracking, CharacterAttribute.Logic, 2, 5, 7)]
    [InlineData(Skill.Con, CharacterAttribute.Charisma, 6, 1, 7)]
    [InlineData(Skill.Piloting, CharacterAttribute.Reaction, 0, 4, 4)]
    [InlineData(Skill.Tasking, CharacterAttribute.Resonance, 3, 3, 6)]
    public void FromSkill_is_skill_rank_plus_the_catalog_linked_attribute_rank(
        Skill skill, CharacterAttribute linkedAttribute, int skillRank, int attributeRank, int expectedPool)
    {
        var character = Character(
            attributes: new Dictionary<CharacterAttribute, int> { [linkedAttribute] = attributeRank },
            skills: new Dictionary<Skill, int> { [skill] = skillRank });

        Assert.Equal(expectedPool, DicePoolAssembly.FromSkill(character, skill));
    }

    [Fact]
    public void FromSkill_ignores_a_rank_recorded_under_a_different_attribute_than_the_linked_one()
    {
        // Firearms links to Agility (SkillCatalog). Recording a rank only under Logic --
        // not Firearms' actual linked attribute -- must not be picked up: FromSkill has no
        // parameter through which a caller could name Logic instead, so this proves the
        // catalog lookup, not caller intent, decides which attribute is read.
        var character = Character(
            attributes: new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Logic] = 6 },
            skills: new Dictionary<Skill, int> { [Skill.Firearms] = 4 });

        Assert.Throws<ArgumentException>(() => DicePoolAssembly.FromSkill(character, Skill.Firearms));
    }

    [Fact]
    public void FromSkill_takes_no_attribute_parameter_so_the_linkage_cannot_be_overridden()
    {
        // Acceptance criterion (Issue #69): "Pairing a skill with an attribute that is not
        // its linked attribute is not possible through the normal path." Enforced by the
        // method's own shape -- there is no attribute parameter to override the catalog
        // with. Pinned here via reflection so a future signature change that adds one is
        // caught by a failing test, not only by review.
        ParameterInfo[] parameters = typeof(DicePoolAssembly)
            .GetMethod(nameof(DicePoolAssembly.FromSkill))!
            .GetParameters();

        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(CharacterAttribute));
    }

    // -------------------------------------------------------------------------- FromAttributes

    [Fact]
    public void FromAttributes_throws_on_null_character()
    {
        Assert.Throws<ArgumentNullException>(
            () => DicePoolAssembly.FromAttributes(
                null!, CharacterAttribute.Body, CharacterAttribute.Agility));
    }

    [Fact]
    public void FromAttributes_throws_when_the_first_attribute_rank_was_never_recorded()
    {
        var character = Character(
            attributes: new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Agility] = 3 });

        Assert.Throws<ArgumentException>(
            () => DicePoolAssembly.FromAttributes(
                character, CharacterAttribute.Body, CharacterAttribute.Agility));
    }

    [Fact]
    public void FromAttributes_throws_when_the_second_attribute_rank_was_never_recorded()
    {
        var character = Character(
            attributes: new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Body] = 3 });

        Assert.Throws<ArgumentException>(
            () => DicePoolAssembly.FromAttributes(
                character, CharacterAttribute.Body, CharacterAttribute.Agility));
    }

    [Fact]
    public void FromAttributes_sums_two_different_attributes_the_first_printed_variant()
    {
        // "Some rolls will simply add two attributes" (printed p. 35).
        var character = Character(attributes: new Dictionary<CharacterAttribute, int>
        {
            [CharacterAttribute.Body] = 5,
            [CharacterAttribute.Agility] = 2,
        });

        int pool = DicePoolAssembly.FromAttributes(
            character, CharacterAttribute.Body, CharacterAttribute.Agility);

        Assert.Equal(7, pool);
    }

    [Fact]
    public void FromAttributes_doubles_the_same_attribute_the_second_printed_variant()
    {
        // "or will count the same attribute twice" (printed p. 35) -- passing the same
        // attribute for both parameters is that variant, not a special-cased branch.
        var character = Character(
            attributes: new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Willpower] = 4 });

        int pool = DicePoolAssembly.FromAttributes(
            character, CharacterAttribute.Willpower, CharacterAttribute.Willpower);

        Assert.Equal(8, pool);
    }

    [Fact]
    public void FromAttributes_order_of_the_two_parameters_does_not_change_the_sum()
    {
        var character = Character(attributes: new Dictionary<CharacterAttribute, int>
        {
            [CharacterAttribute.Reaction] = 3,
            [CharacterAttribute.Intuition] = 5,
        });

        int ordered = DicePoolAssembly.FromAttributes(
            character, CharacterAttribute.Reaction, CharacterAttribute.Intuition);
        int reversed = DicePoolAssembly.FromAttributes(
            character, CharacterAttribute.Intuition, CharacterAttribute.Reaction);

        Assert.Equal(8, ordered);
        Assert.Equal(ordered, reversed);
    }
}
