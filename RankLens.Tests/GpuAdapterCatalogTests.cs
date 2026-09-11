using RankLens.App;

namespace RankLens.Tests;

public sealed class GpuAdapterCatalogTests
{
    [Fact]
    public void EnumeratesDxgiOrderAndExplainsEveryCompatibilityResult()
    {
        var adapters = new GpuAdapterCatalog().Enumerate();

        Assert.NotEmpty(adapters);
        Assert.Equal(Enumerable.Range(0, adapters.Count), adapters.Select(adapter => adapter.DeviceId));
        Assert.All(adapters, adapter =>
        {
            Assert.False(string.IsNullOrWhiteSpace(adapter.Name));
            Assert.Contains("DirectML", adapter.CompatibilityReason, StringComparison.Ordinal);
        });
    }
}
