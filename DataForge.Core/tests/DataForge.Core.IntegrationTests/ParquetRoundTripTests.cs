using DataForge.Core;
using DataForge.Core.Parquet;
using FluentAssertions;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DataForge.Core.IntegrationTests;

public class ParquetRoundTripTests : IDisposable
{
    private readonly string _testDirectory;

    public ParquetRoundTripTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DataForgeParquet_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public async Task Parquet_ReadWrite_RoundTrip()
    {
        var inputPath = Path.Combine(_testDirectory, "input.parquet");
        var outputPath = Path.Combine(_testDirectory, "output.parquet");

        var seed = new[]
        {
            new MetricRow { Id = 1, Name = "alpha", Value = 10.5m },
            new MetricRow { Id = 2, Name = "beta", Value = 99.0m }
        };

        await DataForgePipeline
            .FromMemory(seed)
            .ToParquetAsync(inputPath);

        await ParquetPipelineExtensions
            .FromParquet<MetricRow>(inputPath)
            .Where(x => x.Value >= 50)
            .ToParquetAsync(outputPath);

        var rows = await ParquetPipelineExtensions
            .FromParquet<MetricRow>(outputPath)
            .ToListAsync();

        rows.Should().HaveCount(1);
        rows[0].Name.Should().Be("beta");
        rows[0].Value.Should().Be(99.0m);
    }

    private class MetricRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Value { get; set; }
    }
}
