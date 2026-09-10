using System;
using System.Linq;
using Deckard.Data;

namespace Deckard.Data.Tests;

/// <summary>
/// Mechanical enforcement of the layering in docs/architecture.md.
/// Data may use Core primitives. It must never depend on Rules, because rule values have to stay independent of the algorithms that consume them.
///
/// These assertions are deliberately one-directional. The C# compiler omits assembly
/// references that a compilation does not actually use, so "Deckard.Data references X"
/// cannot be asserted until code in this assembly consumes X. What can always be
/// asserted -- and what actually matters -- is that nothing leaks upward through the
/// layers. The declared ProjectReference graph is checked separately and exactly by
/// tools/repo-checks.py, which reads the .csproj files rather than the compiled output.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] Allowed = new[] { "Deckard.Core" };

    [Fact]
    public void Data_never_references_anything_above_Core()
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
