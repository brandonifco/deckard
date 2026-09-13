using System.Collections.Generic;
using RulesKernel.Randomness;

namespace Deckard.Core.Dice;

/// <summary>
/// Six-sided dice over an injected <see cref="IRandomSource"/>.
///
/// <para>
/// This type used to own its rejection-sampling logic. It no longer does: that logic is
/// exactly uniform bounded drawing, which is not a Shadowrun concern and not even a
/// tabletop one, so it moved to <see cref="UniformInt"/> in the kernel where every engine
/// can share one verified copy. What remains here is the part that genuinely is tabletop
/// vocabulary -- the word "die", the 1-6 face convention, and rolling several in order.
/// </para>
///
/// <para>
/// The kernel deliberately refuses to name this concept: an engine over a statute draws
/// nothing, and a kernel that shipped a <c>D6</c> would have declared what kind of rules it
/// is for. So this is the boundary in miniature. When a shared tabletop pack exists, this
/// type is what moves into it; until then it lives here, thin enough that moving it is a
/// rename.
/// </para>
///
/// <para>
/// It carries no SR6 semantics -- no hits, glitches, thresholds, Edge, or pool sizing by
/// attribute or skill. Those are later layers.
/// </para>
///
/// <para>
/// A rolled face says nothing about how to reproduce it on its own: replaying a roll needs
/// the same <see cref="IRandomSource"/> sequence <em>and</em> a matching
/// <see cref="RulesKernel.Identity.ReplayCompatibilityIdentity"/> (ADR 0005) -- a seed
/// alone is not the contract.
/// </para>
/// </summary>
public static class D6
{
    private const uint Faces = 6;

    /// <summary>
    /// Rolls one six-sided die, returning a face from 1 to 6.
    ///
    /// <para>
    /// A call almost always consumes exactly one draw from <paramref name="source"/>, and
    /// more only when <see cref="UniformInt"/> rejects a raw value and redraws. The exact
    /// count is determined by the source's own sequence, which is what keeps it exactly
    /// reproducible under replay rather than merely usually reproducible.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static int Roll(IRandomSource source) => (int)UniformInt.Below(source, Faces) + 1;

    /// <summary>
    /// Rolls <paramref name="count"/> six-sided dice, returning each face in the order it
    /// was drawn. The result is ordered by construction -- the sequence dice were actually
    /// rolled in, not a sort applied afterward -- per ADR 0006, which governs every
    /// observable ordered result this engine produces.
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
