using Deckard.Core.Randomness;
using Deckard.Rules.Resolution;

namespace Deckard.Rules.Tests.Resolution;

/// <summary>
/// Enforces ADR 0013 (docs/decisions/0013-randomness-consumption-atomicity.md): no
/// operation may draw from its <see cref="IRandomSource"/> until every check capable of
/// producing an unresolved or invalid result has completed. For every resolution entry
/// point under <c>Deckard.Rules</c> that accepts an <see cref="IRandomSource"/> and can
/// fail to resolve, this captures a real <see cref="Pcg32"/>'s <see cref="Pcg32State"/>
/// before the call and asserts it is identical afterward.
///
/// State-equality, not output-equality (ADR 0013's own reasoning for why): a real
/// <see cref="Pcg32"/> is used here rather than
/// <see cref="Deckard.Testing.Randomness.FixedSequenceRandomSource"/>, because only
/// <see cref="Pcg32.GetState"/> can prove the generator's internal state did not advance
/// by even a partial step. A scripted source's <c>Consumed</c> counter can only prove how
/// many values were drawn, not whether the generator moved.
///
/// <see cref="ExtendedTest.Resolve"/> and <see cref="TeamworkTest.Resolve"/> need no test
/// here: neither accepts an <see cref="IRandomSource"/> parameter at all, so neither can
/// advance one -- true by construction, not by behavior this file could observe. The same
/// is true of <c>Smackdown</c>, <c>NotDeadYet</c>, and
/// <c>SocialSituationEdgeGain</c> (<c>src/Deckard.Rules/Edge/</c>): none accepts a source
/// today. <see cref="DicePoolRoll.Roll"/>, <see cref="SimpleTest.Resolve"/>, and
/// <see cref="OpposedTest.Resolve"/> are the only three public members under
/// <c>Deckard.Rules</c> that currently accept one (re-derived directly from source:
/// <c>grep -rn "IRandomSource" src/Deckard.Rules</c>), and all three are total once their
/// arguments validate (ADR 0011): every unresolved-or-invalid path any of them can
/// produce is a thrown <see cref="ArgumentException"/>, not an
/// <see cref="UnresolvedTestResult"/> branch. <see cref="DicePoolRoll.Roll"/> is covered
/// directly below, not only incidentally through <see cref="SimpleTest"/>'s and
/// <see cref="OpposedTest"/>'s own negative-pool cases: it is a public entry point a
/// caller can invoke without going through either.
/// </summary>
public sealed class RandomnessConsumptionAtomicityTests
{
    // A fixed seed/stream is enough here: these tests only ever compare a captured state
    // against itself, never against a pinned expected value, so which sequence is used
    // does not matter -- only that nothing in it gets consumed.
    private static Pcg32 NewSource() => Pcg32.FromSeed(seed: 42UL, stream: 54UL);

    // -------------------------------------------------------------------- DicePoolRoll

    [Fact]
    public void DicePoolRoll_Roll_leaves_the_source_untouched_when_source_is_null()
    {
        // No source is passed for Roll to advance, so there is nothing to capture
        // "before" that belongs to this call -- but a source constructed alongside the
        // (failing) call proves the failure itself performs no ambient draw from
        // anywhere else either.
        var sentinel = NewSource();
        Pcg32State before = sentinel.GetState();

        Assert.Throws<ArgumentNullException>(() => DicePoolRoll.Roll(null!, dicePool: 3));

        Assert.Equal(before, sentinel.GetState());
    }

    [Fact]
    public void DicePoolRoll_Roll_leaves_the_source_untouched_on_negative_dice_pool()
    {
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        Assert.Throws<ArgumentOutOfRangeException>(() => DicePoolRoll.Roll(source, dicePool: -1));

        Assert.Equal(before, source.GetState());
    }

    [Fact]
    public void DicePoolRoll_Roll_does_advance_the_source_once_every_check_passes()
    {
        // The mirror case: once validation clears, consumption is expected, not a
        // violation. This exists so the tests above cannot be satisfied by a
        // DicePoolRoll.Roll that simply never draws at all. DicePoolRoll.Roll is exercised
        // here directly, not only incidentally through SimpleTest's and OpposedTest's own
        // cases above/below: it is a public entry point a caller can invoke on its own.
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        DicePoolRoll.Roll(source, dicePool: 3);

        Assert.NotEqual(before, source.GetState());
    }

    // ------------------------------------------------------------------------ SimpleTest

    [Fact]
    public void SimpleTest_Resolve_leaves_the_source_untouched_when_source_is_null()
    {
        // No source is passed for Resolve to advance, so there is nothing to capture
        // "before" that belongs to this call -- but a source constructed alongside the
        // (failing) call proves the failure itself performs no ambient draw from
        // anywhere else either.
        var sentinel = NewSource();
        Pcg32State before = sentinel.GetState();

        Assert.Throws<ArgumentNullException>(() => SimpleTest.Resolve(null!, dicePool: 3, threshold: 2));

        Assert.Equal(before, sentinel.GetState());
    }

    [Fact]
    public void SimpleTest_Resolve_leaves_the_source_untouched_on_negative_dice_pool()
    {
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SimpleTest.Resolve(source, dicePool: -1, threshold: 2));

        Assert.Equal(before, source.GetState());
    }

    [Fact]
    public void SimpleTest_Resolve_leaves_the_source_untouched_on_negative_threshold()
    {
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SimpleTest.Resolve(source, dicePool: 3, threshold: -1));

        Assert.Equal(before, source.GetState());
    }

    [Fact]
    public void SimpleTest_Resolve_leaves_the_source_untouched_when_both_pool_and_threshold_are_negative()
    {
        // Threshold is validated ahead of the dice pool inside SimpleTest.Resolve
        // (dicePool's own validation lives one layer down, inside DicePoolRoll.Roll).
        // Whichever check fires first, the source must be untouched either way.
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SimpleTest.Resolve(source, dicePool: -1, threshold: -1));

        Assert.Equal(before, source.GetState());
    }

    [Fact]
    public void SimpleTest_Resolve_does_advance_the_source_once_every_check_passes()
    {
        // The mirror case: once validation clears, consumption is expected, not a
        // violation. This exists so the tests above cannot be satisfied by a
        // SimpleTest.Resolve that simply never draws at all.
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        SimpleTest.Resolve(source, dicePool: 3, threshold: 2);

        Assert.NotEqual(before, source.GetState());
    }

    // ----------------------------------------------------------------------- OpposedTest

    [Fact]
    public void OpposedTest_Resolve_leaves_the_source_untouched_when_source_is_null()
    {
        var sentinel = NewSource();
        Pcg32State before = sentinel.GetState();

        Assert.Throws<ArgumentNullException>(
            () => OpposedTest.Resolve(null!, actorPool: 3, defenderPool: 3));

        Assert.Equal(before, sentinel.GetState());
    }

    [Fact]
    public void OpposedTest_Resolve_leaves_the_source_untouched_on_negative_actor_pool()
    {
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OpposedTest.Resolve(source, actorPool: -1, defenderPool: 3));

        Assert.Equal(before, source.GetState());
    }

    [Fact]
    public void OpposedTest_Resolve_leaves_the_source_untouched_on_negative_defender_pool()
    {
        // actorPool is valid here, so this specifically proves the actor's side is never
        // rolled before defenderPool's check runs -- both checks must complete before
        // either side draws, not just before its own side draws.
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OpposedTest.Resolve(source, actorPool: 3, defenderPool: -1));

        Assert.Equal(before, source.GetState());
    }

    [Fact]
    public void OpposedTest_Resolve_leaves_the_source_untouched_when_both_pools_are_negative()
    {
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OpposedTest.Resolve(source, actorPool: -1, defenderPool: -1));

        Assert.Equal(before, source.GetState());
    }

    [Fact]
    public void OpposedTest_Resolve_does_advance_the_source_once_every_check_passes()
    {
        Pcg32 source = NewSource();
        Pcg32State before = source.GetState();

        OpposedTest.Resolve(source, actorPool: 3, defenderPool: 3);

        Assert.NotEqual(before, source.GetState());
    }
}
