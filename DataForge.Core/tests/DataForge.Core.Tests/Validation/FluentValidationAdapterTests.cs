using DataForge.Core;
using DataForge.Core.Core.Validation;
using DataForge.Core.FluentValidation;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace DataForge.Core.Tests.Validation;

public class FluentValidationAdapterTests
{
    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private sealed class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Age).GreaterThan(0);
        }
    }

    [Fact]
    public async Task Adapter_MapsFluentErrors()
    {
        var adapter = new FluentValidationAdapter<Person>(new PersonValidator());
        var result = await adapter.ValidateAsync(new Person { Name = "", Age = -1 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
        result.Errors.Should().Contain(e => e.PropertyName == "Age");
    }

    [Fact]
    public async Task Pipeline_ContinueOnValidationError_UsesAdapter()
    {
        var adapter = new FluentValidationAdapter<Person>(new PersonValidator());
        var data = new[]
        {
            new Person { Name = "Ann", Age = 20 },
            new Person { Name = "", Age = 20 },
            new Person { Name = "Bob", Age = 30 }
        };

        var result = await DataForgePipeline.FromMemory(data)
            .ValidateWith(adapter)
            .ContinueOnValidationError()
            .ToListAsync();

        result.Select(x => x.Name).Should().BeEquivalentTo(new[] { "Ann", "Bob" });
    }
}
