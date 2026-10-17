using ClosedXML.Excel;
using DataForge.Core.Excel;
using FluentAssertions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DataForge.Core.IntegrationTests;

public class ExcelRoundTripTests : IDisposable
{
    private readonly string _testDirectory;

    public ExcelRoundTripTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DataForgeExcel_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task Excel_ReadWrite_RoundTrip()
    {
        var inputPath = Path.Combine(_testDirectory, "input.xlsx");
        var outputPath = Path.Combine(_testDirectory, "output.xlsx");

        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Orders");
            sheet.Cell(1, 1).Value = "Id";
            sheet.Cell(1, 2).Value = "Name";
            sheet.Cell(1, 3).Value = "Amount";
            sheet.Cell(2, 1).Value = 1;
            sheet.Cell(2, 2).Value = "Alice";
            sheet.Cell(2, 3).Value = 100;
            sheet.Cell(3, 1).Value = 2;
            sheet.Cell(3, 2).Value = "Bob";
            sheet.Cell(3, 3).Value = 200;
            workbook.SaveAs(inputPath);
        }

        var result = await ExcelPipelineExtensions
            .FromExcel<OrderRow>(inputPath, new ExcelSourceOptions { SheetName = "Orders" })
            .Where(o => o.Amount >= 150)
            .ToExcel(outputPath);

        result.RecordsWritten.Should().Be(1);
        File.Exists(outputPath).Should().BeTrue();

        var rows = await ExcelPipelineExtensions
            .FromExcel<OrderRow>(outputPath)
            .ToListAsync();

        rows.Should().HaveCount(1);
        rows[0].Name.Should().Be("Bob");
        rows[0].Amount.Should().Be(200);
    }

    private class OrderRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Amount { get; set; }
    }
}
