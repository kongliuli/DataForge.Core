using DataForge.Core;
using DataForge.Core.Core.Validation;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Pipeline;

public class PipelineValidationTests
{
    [Fact]
    public async Task ValidateWith_Validates_Data()
    {
        var data = new[]
        {
            new TestPerson { Name = "John", Age = 25 },
            new TestPerson { Name = "", Age = 17 },
            new TestPerson { Name = "Jane", Age = 30 }
        };

        var results = await DataForgePipeline
            .FromMemory(data)
            .ValidateWith(new TestPersonValidator())
            .ContinueOnValidationError()
            .ToListAsync();

        results.Should().HaveCount(2);
        results.Should().Contain(x => x.Name == "John");
        results.Should().Contain(x => x.Name == "Jane");
    }

    [Fact]
    public async Task ContinueOnValidationError_Continues_On_Error()
    {
        var data = new[]
        {
            new TestPerson { Name = "John", Age = 25 },
            new TestPerson { Name = "", Age = 17 },
            new TestPerson { Name = "Jane", Age = 30 }
        };

        var results = await DataForgePipeline
            .FromMemory(data)
            .ValidateWith(new TestPersonValidator())
            .ContinueOnValidationError()
            .ToListAsync();

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task FailOnValidationError_Throws_Exception()
    {
        var data = new[]
        {
            new TestPerson { Name = "John", Age = 25 },
            new TestPerson { Name = "", Age = 17 }
        };

        var pipeline = DataForgePipeline
            .FromMemory(data)
            .ValidateWith(new TestPersonValidator())
            .FailOnValidationError();

        Func<Task> act = async () => await pipeline.ToListAsync();

        await act.Should().ThrowAsync<ValidationException>();
    }

    private class TestPersonValidator : DataValidator<TestPerson>
    {
        public TestPersonValidator()
        {
            RuleFor(x => x.Name)
                .Required()
                .Length(2, 50);

            RuleFor(x => x.Age)
                .InRange(18, 100);
        }
    }

    private class TestPerson
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }
}
