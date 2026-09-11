using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Deckard.Core;

namespace Deckard.Core.Tests;

/// <summary>
/// Mechanical enforcement of the layering in docs/architecture.md.
/// Core is the bottom of the graph: it may depend on no other Deckard assembly at all, and
/// (see ADR 0005 and Issue #38) it must not touch the filesystem either -- ADR 0001 already
/// forbids this, and <see cref="Deckard.Core.Replay.SourceBaselineId"/> exists precisely so
/// a value can cross that boundary instead of Core reading
/// <c>.github/source-manifest.json</c> itself.
///
/// The assembly-reference assertion is deliberately one-directional. The C# compiler omits
/// assembly references that a compilation does not actually use, so "Deckard.Core
/// references X" cannot be asserted until code in this assembly consumes X. What can
/// always be asserted -- and what actually matters -- is that nothing leaks upward through
/// the layers. The declared ProjectReference graph is checked separately and exactly by
/// tools/repo-checks.py, which reads the .csproj files rather than the compiled output.
///
/// The filesystem assertion scans Core's own source text rather than its compiled IL, the
/// same way tools/repo-checks.py --only determinism scans for banned ambient-entropy APIs:
/// a fully-qualified type reference to a filesystem API will not necessarily appear as a
/// distinct assembly reference under modern .NET, where much of the BCL is forwarded from
/// System.Private.CoreLib, but the call site text always appears in the source that emits
/// it.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] Allowed = Array.Empty<string>();

    // Call-site patterns for filesystem APIs. Namespace-qualified ("System.IO") is
    // included for a fully-qualified reference; the rest are the member-access patterns
    // that appear regardless of a `using` directive, which matters because this project's
    // ImplicitUsings already brings System.IO into scope for every file.
    private static readonly string[] FilesystemPatterns =
    {
        "System.IO",
        "File.",
        "File(",
        "Directory.",
        "Directory(",
        "FileStream",
        "StreamReader",
        "StreamWriter",
    };

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

    [Fact]
    public void Core_source_touches_no_filesystem()
    {
        string coreSourceDirectory = Path.Combine(RepositoryRoot(), "src", "Deckard.Core");
        string[] sourceFiles = Directory.GetFiles(coreSourceDirectory, "*.cs", SearchOption.AllDirectories)
            // obj/ and bin/ hold generated and compiled output, not source -- notably
            // <Project>.GlobalUsings.g.cs, which itself contains the literal text
            // "global using System.IO;" because ImplicitUsings brings that namespace into
            // scope project-wide. That is a build artifact describing scope, not a
            // filesystem access, and repo-checks.py's own determinism scan excludes the
            // same directories for the same reason.
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .ToArray();

        // Guards the guard: if this ever finds zero files, the path resolution is wrong
        // and every check below would pass vacuously.
        Assert.NotEmpty(sourceFiles);

        string[] offenders = sourceFiles
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .SelectMany(file => FilesystemPatterns
                .Where(pattern => file.Text.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetFileName(file.Path)}: contains '{pattern}'"))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "")
    {
        // tests/Deckard.Core.Tests/ArchitectureTests.cs -> repository root is two levels up.
        string testDirectory = Path.GetDirectoryName(thisFile)!;
        return Path.GetFullPath(Path.Combine(testDirectory, "..", ".."));
    }
}
