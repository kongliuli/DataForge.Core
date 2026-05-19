using DataForge.Core;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Pipeline;

public class ExtendedPipelineTests
{
    [Fact]
    public async Task SelectMany_FlattensNestedCollections()
    {
        var data = new[]
        {
            new Container { Items = new List<int> { 1, 2 } },
            new Container { Items = new List<int> { 3, 4, 5 } },
        };
        var pipeline = DataForgePipeline.FromMemory(data).SelectMany(x => x.Items);

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task AggregateAsync_SumsValues()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.AggregateAsync((acc, x) => acc + x, 0);

        result.Should().Be(15);
    }

    [Fact]
    public async Task AggregateAsync_WithSeed_WorksCorrectly()
    {
        var data = new[] { 10, 20, 30 };
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.AggregateAsync((acc, x) => acc + x, 100);

        result.Should().Be(160);
    }

    [Fact]
    public async Task Batch_CreatesBatchesOfCorrectSize()
    {
        var data = new[] { 1, 2, 3, 4, 5 };
        var pipeline = DataForgePipeline.FromMemory(data).Batch(2);

        var result = await pipeline.ToListAsync();

        result.Should().HaveCount(3);
        result[0].Should().BeEquivalentTo(new[] { 1, 2 });
        result[1].Should().BeEquivalentTo(new[] { 3, 4 });
        result[2].Should().BeEquivalentTo(new[] { 5 });
    }

    [Fact]
    public async Task Batch_WithExactMultiple_CreatesFullBatches()
    {
        var data = new[] { 1, 2, 3, 4 };
        var pipeline = DataForgePipeline.FromMemory(data).Batch(2);

        var result = await pipeline.ToListAsync();

        result.Should().HaveCount(2);
        result[0].Should().BeEquivalentTo(new[] { 1, 2 });
        result[1].Should().BeEquivalentTo(new[] { 3, 4 });
    }

    [Fact]
    public async Task Zip_CombinesTwoPipelines()
    {
        var first = DataForgePipeline.FromMemory(new[] { 1, 2, 3 });
        var second = DataForgePipeline.FromMemory(new[] { "a", "b", "c" });

        var result = await first.Zip(second).ToListAsync();

        result.Should().HaveCount(3);
        result[0].Should().Be((1, "a"));
        result[1].Should().Be((2, "b"));
        result[2].Should().Be((3, "c"));
    }

    [Fact]
    public async Task Zip_WithDifferentLengths_StopsAtShorter()
    {
        var first = DataForgePipeline.FromMemory(new[] { 1, 2, 3 });
        var second = DataForgePipeline.FromMemory(new[] { "a", "b" });

        var result = await first.Zip(second).ToListAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task TransformWith_UsesCustomTransform()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .TransformWith(new TestTransform());

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { 10, 20, 30 });
    }

    [Fact]
    public async Task OnErrorContinue_SkipsErrorsInSource()
    {
        var pipeline = DataForgePipeline.FromMemory(new[] { 1, 2, 3, 4, 5 })
            .OnErrorContinue()
            .Where(x =>
            {
                if (x == 3) throw new InvalidOperationException("test");
                return true;
            });

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { 1, 2, 4, 5 });
    }

    [Fact]
    public async Task OnErrorSkip_SkipsErrorsInSource()
    {
        var pipeline = DataForgePipeline.FromMemory(new[] { "a", "b", "c", "d", "e" })
            .OnErrorSkip()
            .Where(x =>
            {
                if (x == "c") throw new InvalidOperationException("test");
                return true;
            });

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { "a", "b", "d", "e" });
    }

    [Fact]
    public async Task OnError_WithCustomHandler_UsesHandlerDecision()
    {
        var pipeline = DataForgePipeline.FromMemory(new[] { "a", "b", "c" })
            .OnError((ex, item) => DataForge.Core.Core.Infrastructure.ErrorAction.Skip)
            .Where(x =>
            {
                if (x == "b") throw new InvalidOperationException("test");
                return true;
            });

        var result = await pipeline.ToListAsync();

        result.Should().BeEquivalentTo(new[] { "a", "c" });
    }

    [Fact]
    public async Task FromJsonString_ParsesJsonContent()
    {
        var json = "[{\"Name\":\"Alice\",\"Age\":30},{\"Name\":\"Bob\",\"Age\":25}]";
        var pipeline = DataForgePipeline.FromJsonString<Person>(json);

        var result = await pipeline.ToListAsync();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Alice");
        result[0].Age.Should().Be(30);
    }

    [Fact]
    public async Task FromJsonArray_ParsesJsonFile()
    {
        var json = "[{\"Name\":\"Test\",\"Age\":20}]";
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(tempFile, json);
            var pipeline = DataForgePipeline.FromJsonArray<Person>(tempFile);

            var result = await pipeline.ToListAsync();

            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Test");
        }
        finally
        {
            if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ToConsoleAsync_WritesToConsole()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data);

        var result = await pipeline.ToConsoleAsync();

        result.RecordsWritten.Should().Be(3);
    }

    [Fact]
    public async Task ToStreamAsync_WritesJsonToStream()
    {
        var data = new[] { "a", "b", "c" };
        var pipeline = DataForgePipeline.FromMemory(data);

        using var stream = new MemoryStream();
        var result = await pipeline.ToStreamAsync(stream, DataForge.Core.Core.Targets.ExportFormat.Json);

        result.RecordsWritten.Should().Be(3);
        stream.Length.Should().BeGreaterThan(0);
    }

    private class Container
    {
        public List<int> Items { get; set; } = [];
    }

    public class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private class TestTransform : DataForge.Core.Core.Transforms.IDataTransform<int, int>
    {
        public int Transform(int source) => source * 10;
    }
}