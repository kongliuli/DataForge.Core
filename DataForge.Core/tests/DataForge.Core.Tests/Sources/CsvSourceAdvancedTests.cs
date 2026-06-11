using DataForge.Core.Core.Sources.Implementations;
using DataForge.Core.Core.Sources.Options;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Sources;

public class CsvSourceAdvancedTests
{
    public class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    public class TemporaryFile : IDisposable
    {
        public string Path { get; }
        public TemporaryFile(string content)
        {
            Path = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllText(Path, content);
        }
        public void Dispose()
        {
            if (System.IO.File.Exists(Path)) System.IO.File.Delete(Path);
        }
    }

    [Fact]
    public async Task QuotedFieldWithDelimiter_ReturnsCorrectValue()
    {
        var csvContent = "Name,Age" + System.Environment.NewLine + "\u0022Smith, John\u0022,30";
        using var tempFile = new TemporaryFile(csvContent);
        var source = new CsvSource<Person>(tempFile.Path);
        var result = new List<Person>();
        await foreach (var item in source.ReadAsync()) result.Add(item);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Smith, John");
        result[0].Age.Should().Be(30);
    }

    [Fact]
    public async Task MultiLineQuotedField_ReturnsSingleRecord()
    {
        var csvContent = "Name,Age" + System.Environment.NewLine + "\u0022Line1" + System.Environment.NewLine + "Line2\u0022,42";
        using var tempFile = new TemporaryFile(csvContent);
        var source = new CsvSource<Person>(tempFile.Path);
        var result = new List<Person>();
        await foreach (var item in source.ReadAsync()) result.Add(item);
        result.Should().HaveCount(1);
        result[0].Name.Should().Contain("Line1");
        result[0].Name.Should().Contain("Line2");
        result[0].Age.Should().Be(42);
    }

    [Fact]
    public async Task EscapedQuotes_ReturnsLiteralQuotes()
    {
        var csvContent = "Name,Age" + System.Environment.NewLine + "\u0022He said \u0022\u0022Hello\u0022\u0022\u0022,42";
        using var tempFile = new TemporaryFile(csvContent);
        var source = new CsvSource<Person>(tempFile.Path);
        var result = new List<Person>();
        await foreach (var item in source.ReadAsync()) result.Add(item);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("He said \u0022Hello\u0022");
        result[0].Age.Should().Be(42);
    }

    [Fact]
    public async Task EmptyCsv_ReturnsZeroRecords()
    {
        using var tempFile = new TemporaryFile("Name,Age");
        var source = new CsvSource<Person>(tempFile.Path);
        var result = new List<Person>();
        await foreach (var item in source.ReadAsync()) result.Add(item);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CommentLines_Skipped()
    {
        var csvContent = "# comment" + System.Environment.NewLine + "Name,Age" + System.Environment.NewLine + "Alice,30";
        using var tempFile = new TemporaryFile(csvContent);
        var source = new CsvSource<Person>(tempFile.Path, new CsvSourceOptions { CommentPrefix = "#" });
        var result = new List<Person>();
        await foreach (var item in source.ReadAsync()) result.Add(item);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Alice");
    }
}
