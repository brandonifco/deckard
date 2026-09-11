namespace Deckard.Core.Replay;

/// <summary>
/// Names the pseudorandom algorithm a sequence of draws was produced by -- not its seed,
/// not its captured state, its algorithm. Two <see cref="Randomness.IRandomSource"/>
/// implementations that happen to produce identical output today are still a
/// replay-compatibility break the moment either one is replaced, because nothing about a
/// sequence of <c>uint</c> values proves which generator produced it. See ADR 0002 for why
/// PCG32 was chosen, and ADR 0005 for why this exists as a comparable value rather than a
/// fact only a reviewer remembers.
/// </summary>
public readonly record struct RandomAlgorithmId
{
    /// <summary>
    /// The algorithm's canonical name, exactly as the PCG reference implementation names
    /// its variant.
    /// </summary>
    public string Name { get; }

    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or whitespace.
    /// </exception>
    public RandomAlgorithmId(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// The one algorithm Deckard implements today: PCG32, variant
    /// <c>pcg_setseq_64_xsh_rr_32</c>, backing <see cref="Randomness.Pcg32"/>. See ADR
    /// 0002. Introducing a second value -- replacing this generator, or adding another --
    /// is the replay-compatibility event ADR 0002 already names; ADR 0005 makes that
    /// checkable as a value comparison instead of leaving it as something only a reviewer
    /// remembers.
    /// </summary>
    public static readonly RandomAlgorithmId Pcg32SetSeq64XshRr32 = new("pcg_setseq_64_xsh_rr_32");
}
