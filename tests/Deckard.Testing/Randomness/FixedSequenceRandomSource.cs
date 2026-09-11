using System.Collections.Generic;
using System.Linq;
using Deckard.Core.Randomness;

namespace Deckard.Testing.Randomness;

/// <summary>
/// A scripted <see cref="IRandomSource"/> that replays a fixed sequence of values in
/// order, for unit tests that need to control exactly what a mechanic draws. Lives in
/// <c>Deckard.Testing</c>, a non-packable test-support project under <c>tests/</c>
/// rather than in <c>Deckard.Core</c>: it ships to nobody, it is not a fourth layer in
/// the <c>Core &lt;- Data &lt;- Rules</c> graph, and <c>tools/repo-checks.py --only
/// layering</c> forbids any <c>src/</c> project from referencing it. See
/// docs/decisions/0001-architecture-boundaries.md.
/// </summary>
public sealed class FixedSequenceRandomSource : IRandomSource
{
    private readonly IReadOnlyList<uint> _values;
    private int _consumed;

    /// <summary>An empty sequence is allowed to construct; it fails on the first draw, like any other exhaustion.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    public FixedSequenceRandomSource(IEnumerable<uint> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values.ToArray();
    }

    /// <summary>
    /// How many values have been drawn so far. Exposed because the number of draws a
    /// mechanic makes is part of its observable contract under ADR 0002 -- a test needs
    /// to be able to assert on it, not just on the values themselves.
    /// </summary>
    public int Consumed => _consumed;

    /// <exception cref="InvalidOperationException">
    /// The supplied sequence is already exhausted. Never wraps, repeats the last value,
    /// or returns zero -- a caller drawing more values than a test scripted is a
    /// programmer error, and any of those fallbacks would silently hide it instead.
    /// </exception>
    public uint NextUInt32()
    {
        if (_consumed >= _values.Count)
        {
            throw new InvalidOperationException(
                $"FixedSequenceRandomSource was given {_values.Count} value(s) and was asked for one more.");
        }

        return _values[_consumed++];
    }
}
