using Deckard.Core.Replay;

namespace Deckard.Core.Tests.Replay;

/// <summary>See ADR 0005.</summary>
public sealed class ReplaySchemaVersionTests
{
    [Fact]
    public void Same_version_compares_equal()
    {
        var a = new ReplaySchemaVersion(1);
        var b = new ReplaySchemaVersion(1);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Different_version_compares_unequal()
    {
        var a = new ReplaySchemaVersion(1);
        var b = new ReplaySchemaVersion(2);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Negative_version_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReplaySchemaVersion(-1));
    }
}
