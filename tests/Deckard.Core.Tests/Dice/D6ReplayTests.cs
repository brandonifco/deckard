using System.Collections.Generic;
using Deckard.Core.Dice;
using Deckard.Core.Randomness;

namespace Deckard.Core.Tests.Dice;

/// <summary>
/// Proves the composition Issue #55 says is not yet proven: that capturing a
/// <see cref="Pcg32"/>'s state partway through rolling dice and restoring it reproduces
/// the same subsequent dice -- not just the same raw <c>uint32</c> sequence
/// (<see cref="Deckard.Core.Tests.Randomness.Pcg32Tests"/>) and not just "same source,
/// same dice" from a fresh seed (<see cref="Deckard.Core.Tests.Dice.D6Tests"/>) in
/// isolation. <see cref="D6"/> is a pure function of its source, so this composition is
/// expected to hold, but "follows from two tested properties" is not "proven by test",
/// and Phase 1's exit criterion is the latter.
///
/// A scripted <see cref="Deckard.Testing.Randomness.FixedSequenceRandomSource"/> cannot
/// stand in for the generator here -- it has no internal state to capture, only a
/// position in a fixed list -- so every test in this file drives a real
/// <see cref="Pcg32"/>.
/// </summary>
public sealed class D6ReplayTests
{
    /// <summary>
    /// Wraps an <see cref="IRandomSource"/> and counts calls to
    /// <see cref="NextUInt32"/>. <c>FixedSequenceRandomSource</c> exposes this as
    /// <c>Consumed</c> for scripted sequences (see <see cref="D6Tests"/>); a real
    /// <see cref="Pcg32"/> has no such counter of its own, and the straddle case below is
    /// only proven if the rejecting die actually consumed two draws, not one.
    /// </summary>
    private sealed class CountingRandomSource(IRandomSource inner) : IRandomSource
    {
        public int Consumed { get; private set; }

        public uint NextUInt32()
        {
            Consumed++;
            return inner.NextUInt32();
        }
    }

    [Fact]
    public void Capturing_mid_dice_sequence_then_restoring_reproduces_the_subsequent_dice()
    {
        var original = Pcg32.FromSeed(seed: 777UL, stream: 21UL);

        // Roll a prefix nobody inspects, just to land the capture somewhere mid-sequence
        // rather than at the generator's very first draw.
        D6.Roll(original, 25);
        var captured = original.GetState();

        // What actually happens next, on the very instance that had been rolling all
        // along -- this is the ground truth the restored copy is judged against.
        IReadOnlyList<int> continuation = D6.Roll(original, 40);

        // A fresh instance built from the capture, not `original` itself: this is what
        // "restore resumes, it does not restart" means for dice specifically. If
        // FromState secretly re-seeded, or GetState captured the wrong point, this would
        // diverge from `continuation` almost immediately -- PCG32 has no short-range
        // correlation between nearby states, so a wrong capture does not coincidentally
        // agree for a while and then drift.
        var restored = Pcg32.FromState(captured);
        IReadOnlyList<int> replayed = D6.Roll(restored, 40);

        Assert.Equal(continuation, replayed);
    }

    // ------------------------------------------------------- the rejection-straddle case

    // D6's rejected raw values are the top four of the uint32 domain: AcceptanceLimit
    // (4,294,967,292) through uint.MaxValue. This literal is independent of D6's own
    // AcceptanceLimit constant, same as D6Tests keeps its own copy, so a shared bug in
    // both places could not hide behind agreement between them.
    private const uint AcceptanceLimit = 4_294_967_292u;

    /// <summary>
    /// A hand-constructed (state, increment) pair, picked so the very next raw draw lands
    /// in the rejected tail and the draw after that is accepted -- a capture taken
    /// immediately before a die that rejects and redraws, which is the exact shape Issue
    /// #55 asks to be covered explicitly rather than incidentally.
    ///
    /// A real Pcg32 stream essentially never produces this on its own (4 draws in 2^32
    /// land in the rejected tail), so it has to be reached deliberately through
    /// <see cref="Pcg32.FromState"/> rather than found by scanning a seeded sequence.
    /// It is derived, not guessed, by inverting Pcg32's output function
    /// (<see cref="Pcg32.NextUInt32"/>) for the case rot = 0:
    ///
    /// Any state below 2^59 forces rot = state &gt;&gt; 59 = 0, making the rotate step a
    /// no-op, so the output is exactly xorshifted = ((state &gt;&gt; 18) ^ state) &gt;&gt; 27.
    /// Shifting a XOR by a fixed amount commutes bitwise -- (a ^ b) &gt;&gt; k equals
    /// (a &gt;&gt; k) ^ (b &gt;&gt; k) -- so writing A for state &gt;&gt; 27 (state's bits 27
    /// through 58), xorshifted reduces to A ^ (A &gt;&gt; 18). Solving that for
    /// A == 0xFFFFFFFF (uint.MaxValue, one of the four rejected raw values) from the top
    /// bit down -- bits 31..14 of A equal the target directly, because A &gt;&gt; 18 is
    /// zero there; bits 13..0 equal the target XORed with the already-solved bit 18
    /// higher -- gives A = 0xFFFFC000, so state = A &lt;&lt; 27 = 0x7FFFE0000000000.
    /// The increment only has to be odd; 1 is the simplest choice.
    ///
    /// This derivation is not trusted on the strength of the comment alone:
    /// <see cref="The_straddle_fixture_actually_straddles"/> checks it against the real
    /// <see cref="Pcg32"/> implementation before either test below relies on it.
    /// </summary>
    private const ulong StraddleState = 0x7FFFE0000000000UL;

    private const ulong StraddleIncrement = 1UL;

    [Fact]
    public void The_straddle_fixture_actually_straddles()
    {
        // A probe instance, used and discarded here so this check cannot itself consume
        // the draws either test below depends on.
        var probe = Pcg32.FromState(new Pcg32State(StraddleState, StraddleIncrement));

        uint firstRawDraw = probe.NextUInt32();
        uint secondRawDraw = probe.NextUInt32();

        Assert.True(
            firstRawDraw >= AcceptanceLimit,
            $"expected the first draw to be rejected (>= {AcceptanceLimit}), got {firstRawDraw}");
        Assert.True(
            secondRawDraw < AcceptanceLimit,
            $"expected the second draw (the redraw) to be accepted (< {AcceptanceLimit}), got {secondRawDraw}");
    }

    [Fact]
    public void Restoring_a_state_captured_immediately_before_a_rejecting_die_reproduces_the_straddling_die_and_what_follows()
    {
        // The case Issue #55 calls out by name. Capturing_mid_dice_sequence_... above
        // exercises a real seeded sequence, but a real Pcg32 stream essentially never
        // straddles a rejection on its own, so this deliberately starts from the
        // constructed fixture above instead of a seed.
        var captured = new Pcg32State(StraddleState, StraddleIncrement);

        var original = Pcg32.FromState(captured);
        var countingOriginal = new CountingRandomSource(original);

        int straddlingFace = D6.Roll(countingOriginal);

        // Two raw draws for one die -- the rejected draw and its redraw -- confirms this
        // Roll call actually is the straddling one, not an incidental single-draw hit
        // that happened to land right after the fixture.
        Assert.Equal(2, countingOriginal.Consumed);

        // Pinned against the accepted second raw draw the fixture check above confirms
        // (0x1B406A50, which maps to face 5) -- a known expected value, not merely
        // agreement between two calls to the same code.
        Assert.Equal(5, straddlingFace);

        // A few ordinary dice after the straddling one, still drawn from `original`.
        IReadOnlyList<int> continuation = D6.Roll(original, 10);

        // A fresh instance from the SAME captured state, independent of `original` and of
        // the counting wrapper around it.
        var restored = Pcg32.FromState(captured);
        IReadOnlyList<int> replayed = D6.Roll(restored, 11);

        int[] expected = [straddlingFace, .. continuation];
        Assert.Equal(expected, replayed);
    }
}
