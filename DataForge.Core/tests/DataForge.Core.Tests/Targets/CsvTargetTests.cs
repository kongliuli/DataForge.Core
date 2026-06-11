using DataForge.Core.Core.Targets;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Targets;

public class CsvTargetTests
{
    public class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items) yield return item;
    }

    [Fact]
    public async Task ExportAsync_BasicData_WritesCsvFile()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new CsvTarget<Person>(new CsvExportOptions());
            await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "Alice", Age = 30 }), path);
            var content = System.IO.File.ReadAllText(path);
            content.Should().Contain("Name,Age");
            content.Should().Contain("Alice");
            content.Should().Contain("30");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ExportAsync_DelimiterInValue_QuotesValue()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new CsvTarget<Person>(new CsvExportOptions());
            await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "Smith, John", Age = 30 }), path);
            var content = System.IO.File.ReadAllText(path);
            content.Should().Contain("\u0022Smith, John\u0022");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ExportAsync_EmptyData_OutputsOnlyHeader()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new CsvTarget<Person>(new CsvExportOptions());
            await target.ExportAsync(ToAsyncEnumerable<Person>(), path);
            var content = System.IO.File.ReadAllText(path).Trim();
            content.Should().Be("Name,Age");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ExportAsync_NoHeader_OmitsHeaderRow()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new CsvTarget<Person>(new CsvExportOptions { IncludeHeader = false });
            await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "Bob", Age = 25 }), path);
            var content = System.IO.File.ReadAllText(path).Trim();
            content.Should().NotContain("Name,Age");
            content.Should().Contain("Bob");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ExportAsync_CustomDelimiter_UsesDelimiter()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new CsvTarget<Person>(new CsvExportOptions { Delimiter = '\t' });
            await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "Charlie", Age = 35 }), path);
            var content = System.IO.File.ReadAllText(path);
            content.Should().Contain("Name\tAge");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ExportAsync_LargeBatch_WritesAllRecords()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var data = Enumerable.Range(1, 2500).Select(i => new Person { Name = "User" + i, Age = i }).ToArray();
            var target = new CsvTarget<Person>(new CsvExportOptions { BatchSize = 1000 });
            var result = await target.ExportAsync(ToAsyncEnumerable(data), path);
            result.RecordsWritten.Should().Be(2500);
            var lines = System.IO.File.ReadAllLines(path);
            lines.Should().HaveCount(2501);
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }
}