using System.Collections.Generic;
using Deckard.Core.Randomness;

namespace Deckard.Core.Dice;

/// <summary>
/// Uniform six-sided die generation over an injected <see cref="IRandomSource"/>, by
/// rejection sampling rather than modulo. This is the "dice roller" and "dice pool"
/// layers from docs/architecture.md's randomness-layering diagram: turning a raw
/// <c>uint32</c> stream into faces, and preserving the order of several draws. It carries
/// no SR6 semantics -- no hits, glitches, thresholds, Edge, or pool sizing by attribute
/// or skill. Those are later layers.
///
/// <see cref="uint.MaxValue"/> plus one (2^32) is not itself a multiple of six --
/// 2^32 = 4,294,967,296 = 6 x 715,827,882 + 4 -- so a plain <c>value % 6</c> mapping is
/// not exactly uniform: four of the six faces would receive 715,827,883 outcomes and two
/// would receive 715,827,882, a difference of about 2.33e-10 absolute (see ADR 0002,
/// docs/decisions/0002-deterministic-randomness.md). That is far too small to distort any
/// dice pool this engine will ever roll -- ADR 0002 said otherwise when this Issue was
/// filed and was corrected. Rejection sampling is used anyway, on the stronger ground that
/// actually holds: it is exactly uniform rather than uniform up to a provably tiny
/// discrepancy, it costs almost nothing, and a mapping that is provably exact needs no
/// argument about whether its error is small enough to tolerate.
///
/// A rolled face, and even a whole rolled sequence, says nothing about how to reproduce it
/// on its own: replaying a roll needs the same <see cref="IRandomSource"/> sequence *and*
/// a matching <see cref="Deckard.Core.Replay.ReplayCompatibilityIdentity"/> (ADR 0005,
/// docs/decisions/0005-replay-compatibility-identity.md) -- a seed alone is not the
/// contract.
/// </summary>
public static class D6
{
    private const uint Faces = 6;

    /// <summary>
    /// The number of raw <c>uint32</c> values, out of 2^32, that map onto each face when
    /// draws below this limit are accepted: the largest multiple of <see cref="Faces"/>
    /// that fits in a <c>uint32</c>.
    ///
    /// 2^32 does not itself fit in a <c>uint</c>, so this is written as
    /// <c>uint.MaxValue - (uint.MaxValue % Faces)</c> (4,294,967,295 - 3 =
    /// 4,294,967,292) rather than against 2^32 directly. Both land on the same value:
    /// 2^32 is not itself a multiple of 6, so the largest multiple of 6 not exceeding
    /// <c>uint.MaxValue</c> (2^32 - 1) is the same as the largest multiple of 6 below
    /// 2^32. Raw draws at or above this limit -- the four values 4,294,967,292 through
    /// <see cref="uint.MaxValue"/> -- are rejected and redrawn; this is the whole risk
    /// this type exists to get right, because an off-by-one here (using <c>&gt;</c>
    /// instead of <c>&gt;=</c>, or subtracting <see cref="Faces"/> itself instead of the
    /// remainder) reintroduces exactly the bias rejection sampling exists to remove, while
    /// looking correct in every ordinary test run.
    /// </summary>
    private const uint AcceptanceLimit = uint.MaxValue - (uint.MaxValue % Faces);

    /// <summary>
    /// Rolls one six-sided die: draws from <paramref name="source"/> until it produces a
    /// value below <see cref="AcceptanceLimit"/>, then maps that value onto a face 1-6.
    ///
    /// A single call almost always consumes exactly one draw. It consumes more only when
    /// a rejected raw value is drawn -- a 4-in-4,294,967,296 chance per draw -- in which
    /// case it draws again and again until an accepted value appears. The exact number of
    /// draws a call makes is therefore not fixed at one; it is determined entirely by
    /// <paramref name="source"/>'s own sequence, which is what keeps it exactly
    /// reproducible under replay (ADR 0002) rather than merely usually reproducible.
    ///
    /// This loop terminates only if <paramref name="source"/> eventually yields an
    /// accepted value. Every real generator does: even two consecutive rejections have
    /// probability (4 / 2^32)^2, about 9e-19, and Deckard has no source that could rig
    /// every draw into the rejected tail forever. A source that never yields an accepted
    /// value is a broken source, not an unresolved dice mechanic -- so this deliberately
    /// imposes no retry cap. An arbitrary cap would not make that case fail more visibly;
    /// it would misreport a broken <see cref="IRandomSource"/> as a dice-roll failure,
    /// which is worse. A test that scripts only rejected values already fails visibly
    /// without one: <c>FixedSequenceRandomSource</c> (tests/Deckard.Testing) throws
    /// <see cref="InvalidOperationException"/> on exhaustion rather than looping, so the
    /// scripted source itself stops the test before this method ever could hang.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static int Roll(IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        uint raw;
        do
        {
            raw = source.NextUInt32();
        }
        while (raw >= AcceptanceLimit);

        return (int)(raw % Faces) + 1;
    }

    /// <summary>
    /// Rolls <paramref name="count"/> six-sided dice in order, returning each face in the
    /// order it was drawn. The result is an <see cref="IReadOnlyList{T}"/> ordered by
    /// construction -- the sequence dice were actually rolled in, not a sort applied
    /// afterward -- per ADR 0006 (docs/decisions/0006-deterministic-ordering-conventions.md),
    /// which governs every observable ordered result this engine produces, including this
    /// one, the first to exist.
    ///
    /// Total draw count is deterministic given <paramref name="source"/>'s sequence, but it
    /// is not simply <paramref name="count"/>: each die independently draws as many raw
    /// values as <see cref="Roll(IRandomSource)"/> needs, including any rejected-and-redrawn
    /// values, before the next die's draws begin.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static IReadOnlyList<int> Roll(IRandomSource source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var results = new int[count];
        for (int i = 0; i < count; i++)
        {
            results[i] = Roll(source);
        }

        return results;
    }
}
