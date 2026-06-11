using DataForge.Core.Core.Targets;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Targets;

public class JsonTargetTests
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
    public async Task ExportAsync_BasicData_WritesJsonFile()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new JsonTarget<Person>(new JsonExportOptions());
            await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "Alice", Age = 30 }), path);
            var content = System.IO.File.ReadAllText(path);
            content.Should().Contain("Alice");
            content.Should().Contain("30");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ExportAsync_Indented_ContainsNewlines()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new JsonTarget<Person>(new JsonExportOptions { Indented = true });
            await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "Bob", Age = 25 }), path);
            var content = System.IO.File.ReadAllText(path);
            content.Should().Contain(System.Environment.NewLine);
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ExportAsync_NotIndented_IsSingleLine()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new JsonTarget<Person>(new JsonExportOptions { Indented = false });
            await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "Charlie", Age = 35 }), path);
            var content = System.IO.File.ReadAllText(path).Trim();
            content.Should().NotContain(System.Environment.NewLine);
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ExportAsync_WithRootProperty_WrapsInObject()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new JsonTarget<Person>(new JsonExportOptions { RootPropertyName = "employees" });
            await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "Alice", Age = 30 }), path);
            var content = System.IO.File.ReadAllText(path);
            content.Should().Contain("employees");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ExportAsync_EmptyData_ReturnsEmptyArray()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            var target = new JsonTarget<Person>(new JsonExportOptions());
            await target.ExportAsync(ToAsyncEnumerable<Person>(), path);
            var content = System.IO.File.ReadAllText(path).Trim();
            content.Should().Be("[]");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }
}