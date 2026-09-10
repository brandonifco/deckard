using System;
using System.Linq;
using Deckard.Rules;

namespace Deckard.Rules.Tests;

/// <summary>
/// Mechanical enforcement of the layering in docs/architecture.md.
/// Rules is the top of the engine graph: it may consume Core and Data, and nothing inside the engine may depend on it.
///
/// These assertions are deliberately one-directional. The C# compiler omits assembly
/// references that a compilation does not actually use, so "Deckard.Rules references X"
/// cannot be asserted until code in this assembly consumes X. What can always be
/// asserted -- and what actually matters -- is that nothing leaks upward through the
/// layers. The declared ProjectReference graph is checked separately and exactly by
/// tools/repo-checks.py, which reads the .csproj files rather than the compiled output.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] Allowed = new[] { "Deckard.Core", "Deckard.Data" };

    [Fact]
    public void Rules_never_references_anything_above_Data()
    {
        string[] actual = AssemblyMarker.Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n.StartsWith("Deckard.", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        string[] forbidden = actual.Except(Allowed, StringComparer.Ordinal).ToArray();

        Assert.Empty(forbidden);
    }
}
