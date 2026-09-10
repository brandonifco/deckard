using System.Reflection;

namespace Deckard.Data;

/// <summary>
/// Assembly marker for Deckard.Data (structured rule values loaded from project rules data).
/// Exists so the architecture tests in tests/Deckard.Data.Tests can reflect over this
/// assembly and assert its dependency direction. It carries no rules behaviour.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>The Deckard.Data assembly.</summary>
    public static Assembly Assembly => typeof(AssemblyMarker).Assembly;
}
