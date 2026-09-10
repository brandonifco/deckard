using System;
using System.Linq;
using Deckard.Core;

namespace Deckard.Core.Tests;

/// <summary>
/// Mechanical enforcement of the layering in docs/architecture.md.
/// Core is the bottom of the graph: it may depend on no other Deckard assembly at all.
///
/// These assertions are deliberately one-directional. The C# compiler omits assembly
/// references that a compilation does not actually use, so "Deckard.Core references X"
/// cannot be asserted until code in this assembly consumes X. What can always be
/// asserted -- and what actually matters -- is that nothing leaks upward through the
/// layers. The declared ProjectReference graph is checked separately and exactly by
/// tools/repo-checks.py, which reads the .csproj files rather than the compiled output.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] Allowed = Array.Empty<string>();

    [Fact]
    public void Core_references_no_other_Deckard_assembly()
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
