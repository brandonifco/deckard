using System.Reflection;

namespace Deckard.Core;

/// <summary>
/// Assembly marker for Deckard.Core (deterministic primitives shared by every other assembly).
/// Exists so the architecture tests in tests/Deckard.Core.Tests can reflect over this
/// assembly and assert its dependency direction. It carries no rules behaviour.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>The Deckard.Core assembly.</summary>
    public static Assembly Assembly => typeof(AssemblyMarker).Assembly;
}
