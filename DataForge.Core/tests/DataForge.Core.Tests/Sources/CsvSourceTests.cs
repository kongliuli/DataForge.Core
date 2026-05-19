using DataForge.Core.Core.Sources.Implementations;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Sources;

public class CsvSourceTests
{
    [Fact]
    public async Task ReadAsync_WithSimpleCsv_ReturnsData()
    {
        using var tempFile = new TemporaryFile("Name,Age\nAlice,30\nBob,25");
        var source = new CsvSource<Person>(tempFile.Path);

        var result = new List<Person>();
        await foreach (var item in source.ReadAsync())
        {
            result.Add(item);
        }

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Alice");
        result[0].Age.Should().Be(30);
        result[1].Name.Should().Be("Bob");
        result[1].Age.Should().Be(25);
    }

    [Fact]
    public async Task ExistsAsync_WithExistingFile_ReturnsTrue()
    {
        using var tempFile = new TemporaryFile("Name,Age\nTest,20");
        var source = new CsvSource<Person>(tempFile.Path);

        var result = await source.ExistsAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingFile_ReturnsFalse()
    {
        var source = new CsvSource<Person>("/nonexistent/file.csv");

        var result = await source.ExistsAsync();

        result.Should().BeFalse();
    }

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
            if (System.IO.File.Exists(Path))
            {
                System.IO.File.Delete(Path);
            }
        }
    }
}