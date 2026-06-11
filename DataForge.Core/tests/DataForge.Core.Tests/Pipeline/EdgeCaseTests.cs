using DataForge.Core;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Pipeline;

public class EdgeCaseTests
{
    [Fact]
    public async Task EmptyStream_ReturnsEmptyList()
    {
        var data = Array.Empty<int>();
        var pipeline = DataForgePipeline.FromMemory(data)
            .Where(x => x > 0)
            .Select(x => x * 2);

        var result = await pipeline.ToListAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PreCancelledToken_ThrowsOperationCanceled()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => pipeline.ToListAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TakeZero_ReturnsEmpty()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data).Take(0);

        var result = await pipeline.ToListAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SkipNegative_ReturnsAll()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data).Skip(-1);

        var result = await pipeline.ToListAsync();
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task SelectThenWhere_ChainsCorrectly()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .Select(x => x.ToString())
            .Where(s => s.Length == 1);

        var result = await pipeline.ToListAsync();
        result.Should().BeEquivalentTo(new[] { "1", "2", "3", "4", "5" });
    }

    [Fact]
    public async Task DistinctByCustomKey_Deduplicates()
    {
        var data = new[]
        {
            new Person { Name = "Alice", City = "NYC" },
            new Person { Name = "Bob", City = "LA" },
            new Person { Name = "Charlie", City = "NYC" },
        };
        var pipeline = DataForgePipeline.FromMemory(data)
            .DistinctBy(p => p.City);

        var result = await pipeline.ToListAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Batch_ExactMultiple_CreatesFullBatches()
    {
        var data = new[] { 1, 2, 3, 4 };
        var pipeline = DataForgePipeline.FromMemory(data).Batch(2);

        var result = await pipeline.ToListAsync();
        result.Should().HaveCount(2);
        result[0].Should().BeEquivalentTo(new[] { 1, 2 });
        result[1].Should().BeEquivalentTo(new[] { 3, 4 });
    }

    [Fact]
    public async Task Batch_RemainderOne_CreatesPartialBatch()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data).Batch(2);

        var result = await pipeline.ToListAsync();
        result.Should().HaveCount(3);
        result[2].Should().BeEquivalentTo(new[] { 5 });
    }

    public class Person
    {
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }
}