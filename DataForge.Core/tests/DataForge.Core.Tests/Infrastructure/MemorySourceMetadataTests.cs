using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources.Implementations;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Infrastructure;

public class MemorySourceMetadataTests
{
    private static IEnumerable<int> YieldNumbers()
    {
        for (var i = 1; i <= 5; i++) yield return i;
    }

    [Fact]
    public async Task DeferredEnumerable_MetadataDoesNotConsumeData()
    {
        var source = new MemorySource<int>(YieldNumbers());
        var metadata = await source.GetMetadataAsync();
        var items = new List<int>();
        await foreach (var item in source.ReadAsync()) items.Add(item);
        items.Should().HaveCount(5);
        items.Should().ContainInOrder(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task ListSource_ReturnsPositiveSize()
    {
        var source = new MemorySource<int>(new List<int> { 1, 2, 3, 4, 5 });
        var metadata = await source.GetMetadataAsync();
        metadata.Size.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ArraySource_ReturnsPositiveSize()
    {
        var source = new MemorySource<int>(new int[] { 1, 2, 3 });
        var metadata = await source.GetMetadataAsync();
        metadata.Size.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NonCollectionEnumerable_ReturnsNegativeOne()
    {
        var source = new MemorySource<int>(System.Linq.Enumerable.Range(1, 100).Where(_ => true));
        var metadata = await source.GetMetadataAsync();
        metadata.Size.Should().Be(-1L);
    }
}
