using Deckard.Core.Randomness;

namespace Deckard.Rules.Resolution;

/// <summary>
/// SR6 Core / Game Concepts / Tests / Simple Tests / printed p. 35 / PDF p. 36: roll a
/// dice pool, count hits, and compare against a gamemaster-set threshold. Hits equal to or
/// greater than the threshold succeed; net hits are the hits above the threshold.
///
/// A glitch never cancels a success: printed p. 44 / PDF p. 45 -- "If you rolled enough
/// hits, a test with a glitch may also be a success." <see cref="SimpleTestResult.Succeeded"/>
/// and <see cref="DicePoolRoll.Glitch"/> are independent facts about the same roll, both
/// exposed on <see cref="SimpleTestResult"/> rather than one silently overriding the other.
/// </summary>
public static class SimpleTest
{
    /// <summary>
    /// Rolls <paramref name="dicePool"/> dice and resolves them against
    /// <paramref name="threshold"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dicePool"/> or <paramref name="threshold"/> is negative. The book's
    /// printed Threshold Guidelines table (same pages) runs 1-7, but nothing in the source
    /// packet restricts a threshold's valid domain to that table's suggested values, so a
    /// threshold of exactly 0 is accepted -- it simply always succeeds, since any
    /// non-negative hit count meets or beats it. A negative threshold has no reading in
    /// the source at all and is rejected as invalid input, not as an unresolved rule.
    /// </exception>
    public static SimpleTestResult Resolve(IRandomSource source, int dicePool, int threshold)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);

        DicePoolRoll roll = DicePoolRoll.Roll(source, dicePool);
        bool succeeded = roll.Hits >= threshold;
        // "the hits above the minimum amount needed to succeed are called net hits"
        // (printed p. 35 / PDF p. 36) is defined in terms of success; on a failed test
        // there is nothing "above" the threshold, so net hits is reported as 0 rather
        // than a negative shortfall.
        int netHits = succeeded ? roll.Hits - threshold : 0;

        return new SimpleTestResult(roll, threshold, succeeded, netHits);
    }
}

/// <summary>The structured, explainable outcome of a resolved <see cref="SimpleTest"/>.</summary>
public sealed class SimpleTestResult
{
    /// <summary>The dice pool rolled for this test, with its hits, ones, and glitch severity.</summary>
    public DicePoolRoll Roll { get; }

    /// <summary>The threshold this test's hits were compared against.</summary>
    public int Threshold { get; }

    /// <summary><see langword="true"/> when <c>Roll.Hits &gt;= Threshold</c>.</summary>
    public bool Succeeded { get; }

    /// <summary>Hits above <see cref="Threshold"/> on success; 0 on failure.</summary>
    public int NetHits { get; }

    internal SimpleTestResult(DicePoolRoll roll, int threshold, bool succeeded, int netHits)
    {
        Roll = roll;
        Threshold = threshold;
        Succeeded = succeeded;
        NetHits = netHits;
    }
}
