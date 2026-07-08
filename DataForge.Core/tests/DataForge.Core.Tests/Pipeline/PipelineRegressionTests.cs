using DataForge.Core;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Pipeline;
using FluentAssertions;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DataForge.Core.Tests.Pipeline;

public class PipelineRegressionTests
{
    [Fact]
    public async Task WithCounter_AfterWhere_CountsFilteredItems()
    {
        var counter = new PerformanceCounter();
        var data = Enumerable.Range(1, 10).Select(i => new { Id = i }).ToList();

        await DataForgePipeline
            .FromMemory(data)
            .WithCounter(counter)
            .Where(x => x.Id > 5)
            .ToListAsync();

        counter.ProcessedItems.Should().Be(5);
    }

    [Fact]
    public async Task ThenBy_SortsByMultipleKeys()
    {
        var data = new[]
        {
            new { Group = "A", Order = 2 },
            new { Group = "A", Order = 1 },
            new { Group = "B", Order = 1 }
        };

        var result = await DataForgePipeline
            .FromMemory(data)
            .OrderBy(x => x.Group)
            .ThenBy(x => x.Order)
            .ToListAsync();

        result[0].Order.Should().Be(1);
        result[1].Order.Should().Be(2);
        result[2].Group.Should().Be("B");
    }

    [Fact]
    public void FromExcel_WithoutExtensionPackage_ThrowsGuidance()
    {
        var act = () => DataForgePipeline.FromExcel<object>("file.xlsx");
        act.Should().Throw<DataForgeException>()
            .Which.ErrorCode.Should().Be("EXCEL_EXTENSION_REQUIRED");
    }

    [Fact]
    public async Task ValidateWith_BeforeSelect_StillFiltersInvalidRows()
    {
        var data = new[]
        {
            new PersonRow { Name = "ok", Age = 10 },
            new PersonRow { Name = "", Age = 20 }
        };

        var result = await DataForgePipeline
            .FromMemory(data)
            .ValidateWith(new PersonRowValidator())
            .ContinueOnValidationError()
            .Select(x => new { x.Name, x.Age })
            .ToListAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("ok");
    }

    private class PersonRow
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private class PersonRowValidator : Core.Validation.DataValidator<PersonRow>
    {
        public PersonRowValidator()
        {
            RuleFor(x => x.Name).Required();
        }
    }
}
