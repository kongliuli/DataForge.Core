using DataForge.Core.Core.Pipeline;
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

        result.Should().HaveCount(3);
        result.Should().ContainInOrder(3, 4, 5);
    }

    [Fact]
    public async Task Select_WithSelector_TransformsItems()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data).Select(x => x * 2);

        var result = await pipeline.ToListAsync();

        result.Should().HaveCount(3);
        result.Should().ContainInOrder(2, 4, 6);
    }

    [Fact]
    public async Task Take_WithCount_LimitsResults()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data).Take(3);

        var result = await pipeline.ToListAsync();

        result.Should().HaveCount(3);
        result.Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task Skip_WithCount_SkipsItems()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data).Skip(2);

        var result = await pipeline.ToListAsync();

        result.Should().HaveCount(3);
        result.Should().ContainInOrder(3, 4, 5);
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
}