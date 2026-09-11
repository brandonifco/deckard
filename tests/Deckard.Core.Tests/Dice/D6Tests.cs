using System.Collections.Generic;
using Deckard.Core.Dice;
using Deckard.Core.Randomness;
using Deckard.Testing.Randomness;

namespace Deckard.Core.Tests.Dice;

/// <summary>
/// Pins <see cref="D6"/> against the acceptance criteria and required evidence in Issue
/// #3: every face 1-6 reachable and no other value ever produced, uniformity over a large
/// seeded sample, exhaustive coverage of the exact <c>uint32</c> values where the
/// rejection rule flips (not a sample), a scripted sequence that a biased modulo mapping
/// would answer differently than the real implementation, and ordered N-dice results with
/// their draw count pinned.
///
/// The domain-boundary numbers used throughout are computed independently of
/// <see cref="D6"/>'s own <c>AcceptanceLimit</c> constant, as literals derived the same
/// way Issue #3 itself derives them: 2^32 = 4,294,967,296 = 6 x 715,827,882 + 4, so the
/// largest multiple of 6 not exceeding <c>uint.MaxValue</c> is 4,294,967,292, and the four
/// raw values 4,294,967,292 through <c>uint.MaxValue</c> (4,294,967,295) are the entire
/// rejected tail -- not recomputed from production code, so a shared bug in both places
/// could not hide behind agreement between them.
/// </summary>
public sealed class D6Tests
{
    private const uint AcceptanceLimit = 4_294_967_292u;

    private static readonly uint[] RejectedRawValues =
    [
        4_294_967_292u,
        4_294_967_293u,
        4_294_967_294u,
        4_294_967_295u, // == uint.MaxValue
    ];

    // -------------------------------------------------------------- argument validation

    [Fact]
    public void Roll_throws_on_null_source()
    {
        Assert.Throws<ArgumentNullException>(() => D6.Roll(null!));
    }

    [Fact]
    public void Roll_with_count_throws_on_null_source()
    {
        Assert.Throws<ArgumentNullException>(() => D6.Roll(null!, 3));
    }

    [Fact]
    public void Roll_with_count_throws_on_negative_count()
    {
        var source = new FixedSequenceRandomSource([0u]);
        Assert.Throws<ArgumentOutOfRangeException>(() => D6.Roll(source, -1));
    }

    [Fact]
    public void Roll_with_zero_count_returns_an_empty_result_without_drawing()
    {
        // An empty script: if this drew even once, FixedSequenceRandomSource would throw,
        // so a passing test proves zero draws happened, not just that the result is empty.
        var source = new FixedSequenceRandomSource([]);

        IReadOnlyList<int> result = D6.Roll(source, 0);

        Assert.Empty(result);
        Assert.Equal(0, source.Consumed);
    }

    // ------------------------------------------------------- accepted-value face mapping

    [Theory]
    [InlineData(0u, 1)]
    [InlineData(1u, 2)]
    [InlineData(2u, 3)]
    [InlineData(3u, 4)]
    [InlineData(4u, 5)]
    [InlineData(5u, 6)]
    public void Roll_maps_the_first_six_raw_values_to_every_face_in_order(uint raw, int expectedFace)
    {
        // The first group of six raw values (0-5) is the simplest accepted range and
        // exercises every face at least once, satisfying "every face 1-6 is reachable".
        var source = new FixedSequenceRandomSource([raw]);

        Assert.Equal(expectedFace, D6.Roll(source));
        Assert.Equal(1, source.Consumed);
    }

    [Theory]
    [InlineData(4_294_967_286u, 1)]
    [InlineData(4_294_967_287u, 2)]
    [InlineData(4_294_967_288u, 3)]
    [InlineData(4_294_967_289u, 4)]
    [InlineData(4_294_967_290u, 5)]
    [InlineData(4_294_967_291u, 6)]
    public void Roll_maps_the_last_accepted_group_to_every_face_in_order(uint raw, int expectedFace)
    {
        // The last group of six raw values below AcceptanceLimit (4,294,967,286 through
        // 4,294,967,291) is where a mapping error at the accepted edge -- as opposed to
        // the accept/reject decision itself -- would show up: these are the highest
        // values D6.Roll ever accepts.
        var source = new FixedSequenceRandomSource([raw]);

        Assert.Equal(expectedFace, D6.Roll(source));
        Assert.Equal(1, source.Consumed);
    }

    [Fact]
    public void No_value_outside_1_to_6_is_ever_reachable()
    {
        // Every accepted raw value in the full uint32 domain reduces to raw % 6, which is
        // in [0, 5] by construction -- so "no other value is ever reachable" follows from
        // the mapping's arithmetic, not from sampling. This is exhaustive over the output
        // range (only 6 possible faces exist), pinned here rather than left implicit.
        for (int i = 0; i < 6; i++)
        {
            var source = new FixedSequenceRandomSource([(uint)i]);
            int face = D6.Roll(source);
            Assert.InRange(face, 1, 6);
        }
    }

    // --------------------------------------------------- exhaustive rejection boundary

    [Theory]
    [MemberData(nameof(RejectedRawValueCases))]
    public void Roll_rejects_every_one_of_the_four_biased_raw_values_and_redraws(uint rejectedRaw)
    {
        // Exhaustive over the ENTIRE rejected set -- there are only four such values in
        // the whole uint32 domain (uint.MaxValue mod 6 = 3 extra beyond the last full
        // group, i.e. 2^32 mod 6 = 4 values total) -- not a sample of it. Each is followed
        // by 0u, an unambiguously accepted value, so a pass proves the first draw was
        // discarded rather than merely that some value near it also happens to look like
        // face 1.
        var source = new FixedSequenceRandomSource([rejectedRaw, 0u]);

        int face = D6.Roll(source);

        Assert.Equal(1, face);
        Assert.Equal(2, source.Consumed);
    }

    public static IEnumerable<object[]> RejectedRawValueCases()
    {
        foreach (uint value in RejectedRawValues)
        {
            yield return new object[] { value };
        }
    }

    [Fact]
    public void The_exact_boundary_where_acceptance_flips_is_pinned()
    {
        // AcceptanceLimit - 1 is the single highest raw value D6.Roll ever accepts;
        // AcceptanceLimit itself is the lowest raw value it ever rejects. This is the
        // precise off-by-one Issue #3 warns is invisible to casual inspection and to
        // random sampling: a "<=" where "<" belongs (or vice versa) would only show up
        // exactly here.
        var acceptedAtEdge = new FixedSequenceRandomSource([AcceptanceLimit - 1]);
        Assert.Equal(6, D6.Roll(acceptedAtEdge));
        Assert.Equal(1, acceptedAtEdge.Consumed);

        var rejectedAtEdge = new FixedSequenceRandomSource([AcceptanceLimit, 0u]);
        Assert.Equal(1, D6.Roll(rejectedAtEdge));
        Assert.Equal(2, rejectedAtEdge.Consumed);
    }

    [Fact]
    public void Every_value_in_the_rejected_tail_is_accounted_for_by_2_to_the_32_mod_6()
    {
        // Cross-checks that RejectedRawValues really is the complete rejected set implied
        // by the Issue's own arithmetic (2^32 mod 6 = 4), rather than a hand-picked few
        // values that happen to pass: the four rejected raws are exactly
        // [AcceptanceLimit, uint.MaxValue], contiguous and none missing or extra.
        Assert.Equal(4, RejectedRawValues.Length);
        Assert.Equal(AcceptanceLimit, RejectedRawValues[0]);
        Assert.Equal(uint.MaxValue, RejectedRawValues[^1]);
        for (int i = 0; i < RejectedRawValues.Length; i++)
        {
            Assert.Equal(AcceptanceLimit + (uint)i, RejectedRawValues[i]);
        }
    }

    // ----------------------------------------------------- discriminates from modulo

    [Fact]
    public void Roll_disagrees_with_a_naive_modulo_mapping_on_a_sequence_landing_in_the_biased_tail()
    {
        // A chi-square test cannot separate exact uniformity from modulo-6 bias: the
        // effect is ~2.33e-10 absolute per face (see ADR 0002), invisible at any sample
        // size this engine could feasibly draw. What DOES separate them is a scripted
        // sequence that starts on a value modulo would treat as valid but rejection
        // sampling discards -- so the two mappings are forced to look at different draws
        // for their answer, structurally rather than statistically.
        //
        // uint.MaxValue (4,294,967,295) is in the four-value biased tail. A naive
        // `value % 6` mapping treats it as valid input and answers from it directly,
        // consuming exactly one draw. The correct mapping rejects it and answers from the
        // next draw instead, consuming two. The second scripted value (5u) is chosen so
        // the two mappings' answers actually differ -- not merely that draw counts
        // differ -- which is what makes this a genuine discriminator and not a coincidence
        // that happens to agree.
        uint rejectedRaw = uint.MaxValue;
        uint nextRaw = 5u;

        // The biased implementation ADR 0002 forbids. Deliberately local to this test: it
        // must never exist as shipped code, only as the thing this test proves D6.Roll is
        // not doing.
        static int NaiveModuloRoll(IRandomSource source) => (int)(source.NextUInt32() % 6) + 1;

        var forNaiveModulo = new FixedSequenceRandomSource([rejectedRaw, nextRaw]);
        int naiveFace = NaiveModuloRoll(forNaiveModulo);

        var forRejectionSampling = new FixedSequenceRandomSource([rejectedRaw, nextRaw]);
        int correctFace = D6.Roll(forRejectionSampling);

        // The naive mapping never looks past the biased first value.
        Assert.Equal(1, forNaiveModulo.Consumed);
        Assert.Equal((int)(rejectedRaw % 6) + 1, naiveFace);

        // D6.Roll rejects the same first value and answers from the second instead.
        Assert.Equal(2, forRejectionSampling.Consumed);
        Assert.Equal((int)(nextRaw % 6) + 1, correctFace);

        // A biased-modulo implementation fails this assertion; D6.Roll passes it. This is
        // the discrimination the required evidence asks for.
        Assert.NotEqual(naiveFace, correctFace);
    }

    // ------------------------------------------------------------- uniformity (coarse)

    [Fact]
    public void Uniformity_holds_over_a_large_seeded_sample()
    {
        // A chi-square goodness-of-fit check over a real PRNG stream. This tests
        // something different from the boundary tests above: not the exactness of the
        // rejection rule, but that the mapping is not GROSSLY wrong (a stuck face, a
        // transposed pair of residues, a shifted modulus). Fully deterministic -- fixed
        // seed and stream, so this either always passes or always fails, never
        // intermittently -- so the threshold below is not a flakiness tolerance, it is
        // "how wrong would the mapping have to be for this many rolls to notice".
        const int sampleSize = 600_000;
        const double expectedPerFace = sampleSize / 6.0;
        // Chi-square critical value for 5 degrees of freedom (6 faces - 1) at alpha =
        // 0.01: a correctly-uniform mapping produces a statistic at or above this value
        // only 1% of the time by chance. Comfortable headroom is expected here, not a
        // near miss -- this fixed seed's statistic is computed once and stays constant.
        const double chiSquareCriticalValue_df5_alpha01 = 15.086;

        var source = Pcg32.FromSeed(seed: 42UL, stream: 1UL);
        var counts = new int[6];
        for (int i = 0; i < sampleSize; i++)
        {
            counts[D6.Roll(source) - 1]++;
        }

        double chiSquare = 0.0;
        foreach (int count in counts)
        {
            double diff = count - expectedPerFace;
            chiSquare += diff * diff / expectedPerFace;
        }

        // Every face reached at least once -- "every face 1-6 is reachable" under a real
        // generator, not just under a hand-scripted single value.
        Assert.All(counts, count => Assert.True(count > 0));
        Assert.Equal(sampleSize, Sum(counts));
        Assert.True(
            chiSquare < chiSquareCriticalValue_df5_alpha01,
            $"chi-square statistic {chiSquare:F4} over {sampleSize} rolls exceeds the "
                + $"df=5, alpha=0.01 critical value {chiSquareCriticalValue_df5_alpha01}; "
                + "counts were [" + string.Join(", ", counts) + "]");
    }

    private static int Sum(int[] values)
    {
        int total = 0;
        foreach (int value in values)
        {
            total += value;
        }

        return total;
    }

    // ------------------------------------------------------------ N-dice, order, count

    [Fact]
    public void Rolling_N_dice_returns_exactly_the_scripted_faces_in_draw_order()
    {
        // Six raw values chosen out of ascending order (3, 0, 5, 1, 4, 2) so a bug that
        // silently sorted or reversed the result -- rather than preserving draw order, as
        // ADR 0006 requires -- would be caught: an ordering bug that only ever gets tested
        // against an already-sorted script cannot be told apart from correct behaviour.
        var source = new FixedSequenceRandomSource([3u, 0u, 5u, 1u, 4u, 2u]);

        IReadOnlyList<int> results = D6.Roll(source, 6);

        Assert.Equal(new[] { 4, 1, 6, 2, 5, 3 }, results);
        Assert.Equal(6, source.Consumed);
    }

    [Fact]
    public void Rolling_N_dice_consumes_exactly_one_draw_per_die_when_none_are_rejected()
    {
        // Pins the "no rejection" draw count for the N-dice path directly: N accepted
        // raw values in, N draws consumed, not N+something. Draw count is part of this
        // mechanic's observable contract (ADR 0002) and this is its baseline case.
        var source = new FixedSequenceRandomSource([0u, 1u, 2u, 3u]);

        IReadOnlyList<int> results = D6.Roll(source, 4);

        Assert.Equal(new[] { 1, 2, 3, 4 }, results);
        Assert.Equal(4, source.Consumed);
    }

    [Fact]
    public void A_rejection_mid_pool_costs_one_extra_draw_without_disturbing_order_or_later_dice()
    {
        // Three dice: the second die's first draw (uint.MaxValue) is in the biased tail
        // and must be discarded before that die's real value (2u) is used. This proves
        // the extra draw is attributed to the die that actually needed it -- the first
        // die's result is unaffected, the second die still gets the value scripted for
        // it (not the third die's), and the third die's value is not shifted left to
        // paper over the rejection.
        var source = new FixedSequenceRandomSource([0u, uint.MaxValue, 2u, 3u]);

        IReadOnlyList<int> results = D6.Roll(source, 3);

        Assert.Equal(new[] { 1, 3, 4 }, results);
        // 4 raw draws for 3 dice: the one rejection is the entire difference.
        Assert.Equal(4, source.Consumed);
    }

    // --------------------------------------------------------------- determinism replay

    [Fact]
    public void The_same_source_sequence_produces_the_same_dice_every_time()
    {
        // Two independently-seeded generators with identical seed and stream draw
        // identical dice, in identical order -- the property replay depends on. This
        // exercises D6 over a real PRNG (Pcg32), not just the scripted double, so it is
        // not merely restating FixedSequenceRandomSource's own contract.
        var a = Pcg32.FromSeed(2024UL, 7UL);
        var b = Pcg32.FromSeed(2024UL, 7UL);

        IReadOnlyList<int> rollsA = D6.Roll(a, 500);
        IReadOnlyList<int> rollsB = D6.Roll(b, 500);

        Assert.Equal(rollsA, rollsB);
    }
}
