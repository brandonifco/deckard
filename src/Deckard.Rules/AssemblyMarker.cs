using System.Reflection;

namespace Deckard.Rules;

/// <summary>
/// Assembly marker for Deckard.Rules (Shadowrun Sixth World mechanics).
/// Exists so the architecture tests in tests/Deckard.Rules.Tests can reflect over this
/// assembly and assert its dependency direction. It carries no rules behaviour.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>The Deckard.Rules assembly.</summary>
    public static Assembly Assembly => typeof(AssemblyMarker).Assembly;
}
