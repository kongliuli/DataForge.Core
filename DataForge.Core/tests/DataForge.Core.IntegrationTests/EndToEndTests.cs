using DataForge.Core;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Validation;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DataForge.Core.IntegrationTests;

public class EndToEndTests : IDisposable
{
    private readonly string _testDirectory;

    public EndToEndTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DataForgeTest_{Guid.NewGuid()}");
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
    public async Task CsvToJson_EndToEnd()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "input.csv");
        var jsonPath = Path.Combine(_testDirectory, "output.json");

        await File.WriteAllTextAsync(csvPath, "Id,Name,Age\n1,John,30\n2,Jane,25");

        // Act
        var result = await DataForgePipeline
            .FromCsv<Person>(csvPath)
            .Where(p => p.Age > 25)
            .ToJsonAsync(jsonPath);

        // Assert
        result.RecordsWritten.Should().Be(1);
        File.Exists(jsonPath).Should().BeTrue();

        var json = await File.ReadAllTextAsync(jsonPath);
        json.Should().Contain("John");
        json.Should().NotContain("Jane");
    }

    [Fact]
    public async Task CsvToCsv_WithTransformation()
    {
        // Arrange
        var inputPath = Path.Combine(_testDirectory, "input.csv");
        var outputPath = Path.Combine(_testDirectory, "output.csv");

        await File.WriteAllTextAsync(inputPath, "Id,Name,Amount\n1,ProductA,100\n2,ProductB,200\n3,ProductC,150");

        // Act
        var results = await DataForgePipeline
            .FromCsv<Product>(inputPath)
            .Select(p => new { p.Name, DiscountedPrice = p.Amount * 0.9m })
            .Where(p => p.DiscountedPrice > 100)
            .OrderByDescending(p => p.DiscountedPrice)
            .ToCsvAsync(outputPath);

        // Assert
        results.RecordsWritten.Should().Be(2);
        var output = await File.ReadAllTextAsync(outputPath);
        output.Should().Contain("ProductB");
        output.Should().Contain("ProductA");
        output.Should().NotContain("ProductC");
    }

    [Fact]
    public async Task JsonToJson_WithValidation()
    {
        // Arrange
        var inputPath = Path.Combine(_testDirectory, "input.json");
        var outputPath = Path.Combine(_testDirectory, "output.json");

        var json = @"[
            {""Id"": 1, ""Name"": ""John"", ""Email"": ""john@example.com""},
            {""Id"": 2, ""Name"": """", ""Email"": ""invalid""},
            {""Id"": 3, ""Name"": ""Jane"", ""Email"": ""jane@example.com""}
        ]";
        await File.WriteAllTextAsync(inputPath, json);

        // Act
        var results = await DataForgePipeline
            .FromJsonString<PersonWithEmail>(json)
            .ValidateWith(new PersonValidator())
            .ContinueOnValidationError()
            .ToJsonAsync(outputPath);

        // Assert
        results.RecordsWritten.Should().Be(2);
    }

    [Fact]
    public async Task MemorySource_WithComplexPipeline()
    {
        // Arrange
        var data = Enumerable.Range(1, 1000)
            .Select(i => new { Id = i, Value = i * 10 })
            .ToList();

        // Act
        var results = await DataForgePipeline
            .FromMemory(data)
            .Where(x => x.Value > 5000)
            .Skip(100)
            .Take(50)
            .Batch(10)
            .SelectMany(batch => batch)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(50);
        results.First().Value.Should().BeGreaterThan(5000);
    }

    [Fact]
    public async Task GroupBy_WithAggregation()
    {
        // Arrange
        var data = new[]
        {
            new { Category = "A", Value = 10 },
            new { Category = "B", Value = 20 },
            new { Category = "A", Value = 30 },
            new { Category = "B", Value = 40 }
        };

        // Act
        var results = await DataForgePipeline
            .FromMemory(data)
            .GroupBy(x => x.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Value), Count = g.Count() })
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(r => r.Category == "A" && r.Total == 40);
        results.Should().Contain(r => r.Category == "B" && r.Total == 60);
    }

    [Fact]
    public async Task DistinctBy_WorksCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new { Id = 1, Name = "John", Email = "john@example.com" },
            new { Id = 2, Name = "Jane", Email = "john@example.com" },
            new { Id = 3, Name = "Bob", Email = "bob@example.com" }
        };

        // Act
        var results = await DataForgePipeline
            .FromMemory(data)
            .DistinctBy(x => x.Email)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(x => x.Name == "John");
        results.Should().Contain(x => x.Name == "Bob");
    }

    [Fact]
    public async Task PerformanceCounter_TracksProgress()
    {
        // Arrange
        var counter = new PerformanceCounter(100);
        var data = Enumerable.Range(1, 100)
            .Select(i => new { Id = i })
            .ToList();

        // Act
        var results = await DataForgePipeline
            .FromMemory(data)
            .WithCounter(counter)
            .ToListAsync();

        // Assert
        counter.ProcessedItems.Should().Be(100);
        counter.ItemsPerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SkipAndTake_WorksCorrectly()
    {
        // Arrange
        var data = Enumerable.Range(1, 100)
            .Select(i => new { Id = i })
            .ToList();

        // Act
        var results = await DataForgePipeline
            .FromMemory(data)
            .Skip(10)
            .Take(20)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(20);
        results.First().Id.Should().Be(11);
        results.Last().Id.Should().Be(30);
    }

    private class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Amount { get; set; }
    }

    private class PersonWithEmail
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }

    private class PersonValidator : DataValidator<PersonWithEmail>
    {
        public PersonValidator()
        {
            RuleFor(x => x.Name)
                .Required()
                .Length(2, 50);

            RuleFor(x => x.Email)
                .Must(e => e.Contains("@") && e.Contains("."))
                .WithMessage("Invalid email format");
        }
    }
}
