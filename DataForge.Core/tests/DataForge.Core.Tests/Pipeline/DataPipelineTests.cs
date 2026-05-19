using DataForge.Core;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Pipeline;

public class DataPipelineTests
{
    [Fact]
    public async Task ToListAsync_WithData_ReturnsAllItems()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.ToListAsync();

        result.Should().HaveCount(5);
        result.Should().ContainInOrder(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task Where_WithPredicate_FiltersItems()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data).Where(x => x > 2);

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { 3, 4, 5 });
    }

    [Fact]
    public async Task Where_WithFalsePredicate_ReturnsEmpty()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data).Where(_ => false);

        var result = await pipeline.ToListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Select_WithSelector_TransformsItems()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data).Select(x => x * 2);

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { 2, 4, 6 });
    }

    [Fact]
    public async Task Select_ChangesType()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data).Select(x => x.ToString());

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { "1", "2", "3" });
    }

    [Fact]
    public async Task OrderBy_SortsAscending()
    {
        var data = new[] { 3, 1, 4, 1, 5 };
        var pipeline = DataForgePipeline.FromMemory(data).OrderBy(x => x);

        var result = await pipeline.ToListAsync();

        result.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task OrderByDescending_SortsDescending()
    {
        var data = new[] { 3, 1, 4, 1, 5 };
        var pipeline = DataForgePipeline.FromMemory(data).OrderByDescending(x => x);

        var result = await pipeline.ToListAsync();

        result.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Take_WithCount_LimitsResults()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data).Take(3);

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Skip_WithCount_SkipsItems()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data).Skip(2);

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { 3, 4, 5 });
    }

    [Fact]
    public async Task Distinct_RemovesDuplicates()
    {
        var data = new[] { 1, 2, 2, 3, 3, 3 };
        var pipeline = DataForgePipeline.FromMemory(data).Distinct();

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task DistinctBy_RemovesByKey()
    {
        var data = new[] { "apple", "avocado", "banana", "blueberry", "cherry" };
        var pipeline = DataForgePipeline.FromMemory(data).DistinctBy(x => x[0]);

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { "apple", "banana", "cherry" });
    }

    [Fact]
    public async Task ChainedOperations_WorkCorrectly()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .Where(x => x > 2)
            .Select(x => x * 10)
            .Take(2);

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { 30, 40 });
    }

    [Fact]
    public async Task CountAsync_WithData_ReturnsCorrectCount()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.CountAsync();

        result.Should().Be(5);
    }

    [Fact]
    public async Task AnyAsync_WithData_ReturnsTrue()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.AnyAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AnyAsync_WithEmptyData_ReturnsFalse()
    {
        var data = Array.Empty<int>();
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.AnyAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithData_ReturnsFirst()
    {
        var data = new[] { 10, 20, 30 };
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.FirstOrDefaultAsync();

        result.Should().Be(10);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithEmptyData_ReturnsDefault()
    {
        var data = Array.Empty<int>();
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.FirstOrDefaultAsync();

        result.Should().Be(0);
    }

    [Fact]
    public async Task ToArrayAsync_ReturnsArray()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.ToArrayAsync();

        result.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task GroupBy_GroupsCorrectly()
    {
        var data = new[]
        {
            new TestItem { Category = "A", Value = 1 },
            new TestItem { Category = "B", Value = 2 },
            new TestItem { Category = "A", Value = 3 },
        };
        var pipeline = DataForgePipeline.FromMemory(data)
            .GroupBy(x => x.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() });

        var result = await pipeline.ToListAsync();

        result.Should().HaveCount(2);
        result.Should().Contain(x => x.Category == "A" && x.Count == 2);
        result.Should().Contain(x => x.Category == "B" && x.Count == 1);
    }

    private class TestItem
    {
        public string Category { get; set; } = "";
        public int Value { get; set; }
    }
}