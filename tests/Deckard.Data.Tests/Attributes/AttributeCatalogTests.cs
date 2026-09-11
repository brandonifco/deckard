using System;
using System.Collections.Generic;
using System.Linq;
using Deckard.Data.Attributes;

namespace Deckard.Data.Tests.Attributes;

/// <summary>
/// Pins the attribute vocabulary against SR6 Core / Game Concepts / Character Traits /
/// Attributes / printed pp. 37-38 / PDF pp. 38-39: the printed list is a finite, closed
/// set of twelve names in three groups (Physical, Mental, Special), and this asserts the
/// whole set and grouping -- not a sample -- per Issue #64's required evidence.
/// </summary>
public sealed class AttributeCatalogTests
{
    // ------------------------------------------------------------------- printed groups

    [Fact]
    public void Physical_group_is_exactly_Body_Agility_Reaction_Strength_in_printed_order()
    {
        Assert.Equal(
            new[]
            {
                CharacterAttribute.Body,
                CharacterAttribute.Agility,
                CharacterAttribute.Reaction,
                CharacterAttribute.Strength,
            },
            AttributeCatalog.Physical);
    }

    [Fact]
    public void Mental_group_is_exactly_Willpower_Logic_Intuition_Charisma_in_printed_order()
    {
        Assert.Equal(
            new[]
            {
                CharacterAttribute.Willpower,
                CharacterAttribute.Logic,
                CharacterAttribute.Intuition,
                CharacterAttribute.Charisma,
            },
            AttributeCatalog.Mental);
    }

    [Fact]
    public void Special_group_is_exactly_Edge_Magic_Resonance_Essence_in_printed_order()
    {
        Assert.Equal(
            new[]
            {
                CharacterAttribute.Edge,
                CharacterAttribute.Magic,
                CharacterAttribute.Resonance,
                CharacterAttribute.Essence,
            },
            AttributeCatalog.Special);
    }

    // ------------------------------------------------------------------- the whole set

    [Fact]
    public void All_is_exactly_the_three_groups_concatenated_Physical_then_Mental_then_Special()
    {
        Assert.Equal(
            AttributeCatalog.Physical.Concat(AttributeCatalog.Mental).Concat(AttributeCatalog.Special),
            AttributeCatalog.All);
    }

    [Fact]
    public void All_contains_exactly_the_twelve_printed_attributes_no_extra_none_missing()
    {
        // The printed list has twelve names total (4 Physical + 4 Mental + 4 Special).
        // Comparing as sets (not order) against the enum's own full value set catches an
        // attribute declared on the enum but left out of a group, or vice versa.
        var expected = new HashSet<CharacterAttribute>(Enum.GetValues<CharacterAttribute>());
        var actual = new HashSet<CharacterAttribute>(AttributeCatalog.All);

        Assert.Equal(12, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void All_has_no_duplicate_attribute()
    {
        Assert.Equal(AttributeCatalog.All.Count, AttributeCatalog.All.Distinct().Count());
    }

    // ------------------------------------------------------------------------ CategoryOf

    [Theory]
    [InlineData(CharacterAttribute.Body, AttributeCategory.Physical)]
    [InlineData(CharacterAttribute.Agility, AttributeCategory.Physical)]
    [InlineData(CharacterAttribute.Reaction, AttributeCategory.Physical)]
    [InlineData(CharacterAttribute.Strength, AttributeCategory.Physical)]
    [InlineData(CharacterAttribute.Willpower, AttributeCategory.Mental)]
    [InlineData(CharacterAttribute.Logic, AttributeCategory.Mental)]
    [InlineData(CharacterAttribute.Intuition, AttributeCategory.Mental)]
    [InlineData(CharacterAttribute.Charisma, AttributeCategory.Mental)]
    [InlineData(CharacterAttribute.Edge, AttributeCategory.Special)]
    [InlineData(CharacterAttribute.Magic, AttributeCategory.Special)]
    [InlineData(CharacterAttribute.Resonance, AttributeCategory.Special)]
    [InlineData(CharacterAttribute.Essence, AttributeCategory.Special)]
    public void CategoryOf_reports_the_printed_group_for_every_attribute(
        CharacterAttribute attribute, AttributeCategory expectedCategory)
    {
        Assert.Equal(expectedCategory, AttributeCatalog.CategoryOf(attribute));
    }

    [Fact]
    public void CategoryOf_throws_on_an_undefined_attribute_value()
    {
        var undefined = (CharacterAttribute)(-1);

        Assert.Throws<ArgumentOutOfRangeException>(() => AttributeCatalog.CategoryOf(undefined));
    }
}
