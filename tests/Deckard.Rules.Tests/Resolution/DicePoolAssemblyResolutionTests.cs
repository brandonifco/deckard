using System.Collections.Generic;
using System.Linq;
using Deckard.Data.Attributes;
using Deckard.Data.Skills;
using Deckard.Rules.Characters;
using Deckard.Rules.Resolution;
using Deckard.Testing.Randomness;

namespace Deckard.Rules.Tests.Resolution;

/// <summary>
/// Issue #69's required evidence: "at least one test that goes from a character's ranks
/// through pool assembly into a resolved test with scripted dice, so the pieces are shown
/// to fit rather than assumed to." <see cref="DicePoolAssembly"/> and <see cref="CharacterRanks"/>
/// are pinned on their own in <see cref="DicePoolAssemblyTests"/> and in
/// <c>CharacterRanksTests</c> (tests/Deckard.Rules.Tests/Characters); this file only
/// demonstrates the full path they exist to enable: <see cref="CharacterRanks"/> ranks in, an assembled
/// <see cref="int"/> pool out of <see cref="DicePoolAssembly"/>, fed unchanged into
/// <see cref="SimpleTest.Resolve"/> and <see cref="OpposedTest.Resolve"/> (Issue #69's
/// acceptance criterion that the result "feeds SimpleTest.Resolve and OpposedTest.Resolve
/// unchanged").
/// </summary>
public sealed class DicePoolAssemblyResolutionTests
{
    private static FixedSequenceRandomSource ForFaces(params int[] faces) =>
        new(faces.Select(f => (uint)(f - 1)));

    [Fact]
    public void A_character_s_skill_and_attribute_ranks_assemble_into_a_resolved_SimpleTest()
    {
        // "Outdoors + Intuition (3) test" is the book's own worked Simple test example
        // (printed p. 35 / PDF p. 36). Outdoors' printed linked attribute is Intuition
        // (SkillCatalog.LinkedAttributeOf), so this character's Outdoors 3 + Intuition 2
        // assembles the 5-die pool the example's shape calls for.
        var character = new CharacterRanks(
            attributeRanks: new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Intuition] = 2 },
            skillRanks: new Dictionary<Skill, int> { [Skill.Outdoors] = 3 });

        int pool = DicePoolAssembly.FromSkill(character, Skill.Outdoors);
        Assert.Equal(5, pool);

        // Threshold 3, matching the worked example's own threshold. Scripted so exactly 3
        // of the 5 dice are hits (5s/6s) and the rest are not -- enough to succeed with a
        // known, non-trivial net-hits count, not merely to hit any threshold at all.
        var source = ForFaces(6, 5, 6, 2, 3);
        SimpleTestResult result = SimpleTest.Resolve(source, pool, threshold: 3);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Roll.Hits);
        Assert.Equal(0, result.NetHits);
        Assert.Equal(5, source.Consumed);
    }

    [Fact]
    public void Two_characters_skill_ranks_assemble_into_a_resolved_OpposedTest()
    {
        // "Stealth + Agility vs. Perception + Intuition" is the book's own worked Opposed
        // test example (printed p. 36 / PDF p. 37). Stealth links to Agility and Perception
        // links to Intuition (SkillCatalog), matching that example's shape exactly.
        var actor = new CharacterRanks(
            attributeRanks: new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Agility] = 3 },
            skillRanks: new Dictionary<Skill, int> { [Skill.Stealth] = 2 });
        var defender = new CharacterRanks(
            attributeRanks: new Dictionary<CharacterAttribute, int> { [CharacterAttribute.Intuition] = 1 },
            skillRanks: new Dictionary<Skill, int> { [Skill.Perception] = 2 });

        int actorPool = DicePoolAssembly.FromSkill(actor, Skill.Stealth);
        int defenderPool = DicePoolAssembly.FromSkill(defender, Skill.Perception);
        Assert.Equal(5, actorPool);
        Assert.Equal(3, defenderPool);

        // Actor: 3 hits out of 5. Defender: 1 hit out of 3. Actor wins by 2 net hits.
        var source = ForFaces(6, 5, 6, 1, 2, /* actor */ 5, 3, 4 /* defender */);
        OpposedTestResult result = OpposedTest.Resolve(source, actorPool, defenderPool);

        Assert.Equal(OpposedTestWinner.Actor, result.Winner);
        Assert.Equal(3, result.Actor.Hits);
        Assert.Equal(1, result.Defender.Hits);
        Assert.Equal(2, result.NetHits);
        Assert.Equal(8, source.Consumed);
    }

    [Fact]
    public void A_two_attribute_pool_assembles_and_resolves_the_same_way_as_a_skill_pool()
    {
        // Demonstrates the printed "add two attributes" variant (printed p. 35) through
        // the same resolution path as the skill-based tests above, using different
        // attributes -- FromAttributes_doubles_the_same_attribute_the_second_printed_variant
        // in DicePoolAssemblyTests already pins the "same attribute twice" variant's own
        // arithmetic, so it is not repeated here.
        var character = new CharacterRanks(
            attributeRanks: new Dictionary<CharacterAttribute, int>
            {
                [CharacterAttribute.Reaction] = 2,
                [CharacterAttribute.Intuition] = 2,
            },
            skillRanks: new Dictionary<Skill, int>());

        int pool = DicePoolAssembly.FromAttributes(
            character, CharacterAttribute.Reaction, CharacterAttribute.Intuition);
        Assert.Equal(4, pool);

        var source = ForFaces(6, 6, 1, 2);
        SimpleTestResult result = SimpleTest.Resolve(source, pool, threshold: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Roll.Hits);
        Assert.Equal(0, result.NetHits);
    }
}
