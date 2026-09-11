using Deckard.Core.Replay;

namespace Deckard.Core.Tests.Replay;

/// <summary>
/// See ADR 0005. The acceptance test for this Issue is deliberately not "two identical
/// identities are equal" alone -- see Issue #14 on vacuous equality tests -- but that each
/// of the four components independently breaks equality when it alone differs, holding the
/// other three fixed.
/// </summary>
public sealed class ReplayCompatibilityIdentityTests
{
    private static ReplayCompatibilityIdentity Baseline() => new(
        RandomAlgorithmId.Pcg32SetSeq64XshRr32,
        new RulesetVersion("sr6-core-ruleset", 1),
        new ReplaySchemaVersion(1),
        new SourceBaselineId("sr6-core"));

    [Fact]
    public void Identical_components_compare_equal()
    {
        ReplayCompatibilityIdentity a = Baseline();
        ReplayCompatibilityIdentity b = Baseline();

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Differing_only_in_random_algorithm_is_unequal()
    {
        ReplayCompatibilityIdentity a = Baseline();
        ReplayCompatibilityIdentity b = a with { RandomAlgorithm = new RandomAlgorithmId("some-other-generator") };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Differing_only_in_ruleset_is_unequal()
    {
        ReplayCompatibilityIdentity a = Baseline();
        ReplayCompatibilityIdentity b = a with { Ruleset = new RulesetVersion("sr6-core-ruleset", 2) };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Differing_only_in_replay_schema_is_unequal()
    {
        ReplayCompatibilityIdentity a = Baseline();
        ReplayCompatibilityIdentity b = a with { ReplaySchema = new ReplaySchemaVersion(2) };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Differing_only_in_source_baseline_is_unequal()
    {
        ReplayCompatibilityIdentity a = Baseline();
        ReplayCompatibilityIdentity b = a with { SourceBaseline = new SourceBaselineId("sr6-seattle-city-edition") };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void An_identity_differing_in_ruleset_does_not_equal_one_differing_in_schema()
    {
        // The acceptance criterion names this pair explicitly: two identities that each
        // differ from the baseline in a different single component must not collide with
        // each other either.
        ReplayCompatibilityIdentity rulesetChanged = Baseline() with
        {
            Ruleset = new RulesetVersion("sr6-core-ruleset", 2),
        };
        ReplayCompatibilityIdentity schemaChanged = Baseline() with
        {
            ReplaySchema = new ReplaySchemaVersion(2),
        };

        Assert.NotEqual(rulesetChanged, schemaChanged);
    }
}
