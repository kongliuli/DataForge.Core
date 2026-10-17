using DataForge.Core;
using DataForge.Core.DuckDB;
using FluentAssertions;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DataForge.Core.IntegrationTests;

public class DuckDbRoundTripTests : IDisposable
{
    private readonly string _testDirectory;

    public DuckDbRoundTripTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DataForgeDuckDb_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public async Task DuckDb_QueryAndLoad_RoundTrip()
    {
        var dbPath = Path.Combine(_testDirectory, "analytics.duckdb");

        await DataForgePipeline
            .FromMemory(new[]
            {
                new SaleRow { Region = "east", Amount = 100m },
                new SaleRow { Region = "west", Amount = 250m }
            })
            .ToDuckDbAsync(dbPath, "sales");

        var rows = await DuckDbPipelineExtensions
            .FromDuckDb<SaleRow>(dbPath, "SELECT Region, Amount FROM sales WHERE Amount >= 200 ORDER BY Region")
            .ToListAsync();

        rows.Should().HaveCount(1);
        rows[0].Region.Should().Be("west");
        rows[0].Amount.Should().Be(250m);
    }

    private class SaleRow
    {
        public string Region { get; set; } = "";
        public decimal Amount { get; set; }
    }
}
