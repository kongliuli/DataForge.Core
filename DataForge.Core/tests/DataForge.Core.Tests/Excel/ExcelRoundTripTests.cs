using DataForge.Core;
using DataForge.Core.Excel;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Excel;

public class ExcelRoundTripTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task ToExcel_Then_FromExcel_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dataforge-excel-{Guid.NewGuid():N}.xlsx");
        try
        {
            var written = await DataForgePipeline.FromMemory(new[]
                {
                    new Row { Id = 1, Name = "Alpha" },
                    new Row { Id = 2, Name = "Beta" }
                })
                .ToExcel(path, new ExcelExportOptions { SheetName = "Items", IncludeHeader = true });

            written.RecordsWritten.Should().Be(2);
            File.Exists(path).Should().BeTrue();

            var rows = await new DataForgePipeline()
                .FromExcel<Row>(path, new ExcelSourceOptions { SheetName = "Items", HasHeaderRow = true })
                .ToListAsync();

            rows.Should().HaveCount(2);
            rows.Select(r => r.Name).Should().BeEquivalentTo(new[] { "Alpha", "Beta" });
            rows.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1, 2 });
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
