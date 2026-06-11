using DataForge.Core.Core.Targets;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Targets;

public class ConsoleTargetTests
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

    private static string CaptureOutput(Func<Task> action)
    {
        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            action().GetAwaiter().GetResult();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void ExportAsync_WithFormatter_UsesFormatter()
    {
        var output = CaptureOutput(async () =>
        {
            var target = new ConsoleTarget<Person>(p => "NAME:" + p.Name);
            await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "Bob", Age = 25 }), string.Empty);
        });
        output.Trim().Should().Be("NAME:Bob");
    }

    [Fact]
    public void ExportAsync_EmptyData_WritesNothing()
    {
        var output = CaptureOutput(async () =>
        {
            var target = new ConsoleTarget<Person>();
            await target.ExportAsync(ToAsyncEnumerable<Person>(), string.Empty);
        });
        output.Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAsync_ReturnsCorrectRecordCount()
    {
        var target = new ConsoleTarget<Person>();
        var result = await target.ExportAsync(ToAsyncEnumerable(new Person { Name = "A", Age = 1 }, new Person { Name = "B", Age = 2 }), string.Empty);
        result.RecordsWritten.Should().Be(2);
    }
}