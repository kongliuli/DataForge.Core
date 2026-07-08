using DataForge.Core;
using DataForge.Core.Core.Pipeline;
using DataForge.Core.Core.Validation;
using FluentAssertions;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DataForge.Core.Tests.Pipeline;

public class RowErrorTests
{
    [Fact]
    public async Task WithBadRowOutput_WritesValidationErrorsToFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"df-rowerr-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        var outPath = Path.Combine(dir, "out.json");
        var errPath = Path.Combine(dir, "errors.ndjson");

        try
        {
            var data = new[]
            {
                new NameRow { Name = "ok" },
                new NameRow { Name = "" }
            };

            var result = await DataForgePipeline
                .FromMemory(data)
                .ValidateWith(new NameRowValidator())
                .ContinueOnValidationError()
                .WithBadRowOutput(errPath)
                .ToJsonAsync(outPath);

            result.RowErrors.Should().HaveCount(1);
            result.RowErrors[0].PropertyName.Should().Be("Name");
            result.RowErrors[0].Kind.Should().Be(Core.Models.ErrorKind.Validation);
            File.Exists(errPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public async Task SelectParallelAsync_PreservesOrder()
    {
        var data = Enumerable.Range(1, 20).Select(i => i).ToList();
        var result = await DataForgePipeline
            .FromMemory(data)
            .SelectParallelAsync(async x =>
            {
                await Task.Delay(1);
                return x * 2;
            }, maxDegreeOfParallelism: 4)
            .ToListAsync();

        result.Should().Equal(Enumerable.Range(1, 20).Select(x => x * 2));
    }

    private class NameRow
    {
        public string Name { get; set; } = "";
    }

    private class NameRowValidator : DataValidator<NameRow>
    {
        public NameRowValidator() => RuleFor(x => x.Name).Required();
    }
}
