using DataForge.Core.Core.Validation;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Validation;

public class ValidationTests
{
    [Fact]
    public void DataValidator_WithRequiredRule_FailsOnEmpty()
    {
        var validator = new TestValidator();
        var item = new TestItem { Name = "", Age = 25 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == ValidationErrorCode.Required);
    }

    [Fact]
    public void DataValidator_WithRequiredRule_PassesOnValue()
    {
        var validator = new TestValidator();
        var item = new TestItem { Name = "Test", Age = 25 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DataValidator_WithLengthRule_FailsOnTooLong()
    {
        var validator = new TestValidator();
        var item = new TestItem { Name = "This name is way too long for the rule", Age = 25 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == ValidationErrorCode.Length);
    }

    [Fact]
    public void DataValidator_WithGreaterThanRule_FailsOnZero()
    {
        var validator = new TestValidator();
        var item = new TestItem { Name = "Test", Age = 0 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == ValidationErrorCode.Range);
    }

    [Fact]
    public void DataValidator_WithInRangeRule_FailsOnOutOfRange()
    {
        var validator = new TestValidator();
        var item = new TestItem { Name = "Test", Age = 200 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == ValidationErrorCode.Range);
    }

    [Fact]
    public async Task ContinueOnValidationError_SkipsInvalidItems()
    {
        var data = new[]
        {
            new TestItem { Name = "Valid", Age = 25 },
            new TestItem { Name = "", Age = 25 },
            new TestItem { Name = "Also Valid", Age = 30 },
        };

        var validator = new TestValidator();
        var pipeline = DataForge.Core.DataForgePipeline.FromMemory(data)
            .ValidateWith(validator)
            .ContinueOnValidationError();

        var result = await pipeline.ToListAsync();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Valid");
        result[1].Name.Should().Be("Also Valid");
    }

    [Fact]
    public async Task FailOnValidationError_ThrowsOnInvalidItem()
    {
        var data = new[]
        {
            new TestItem { Name = "Valid", Age = 25 },
            new TestItem { Name = "", Age = 25 },
        };

        var validator = new TestValidator();
        var pipeline = DataForge.Core.DataForgePipeline.FromMemory(data)
            .ValidateWith(validator)
            .FailOnValidationError();

        var act = async () => await pipeline.ToListAsync();

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public void WithMessage_SetsCustomErrorMessage()
    {
        var validator = new CustomMessageValidator();
        var item = new TestItem { Name = "", Age = 25 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Be("Name is required!");
    }

    private class TestValidator : DataValidator<TestItem>
    {
        public TestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Name).Length(1, 20);
            RuleFor(x => x.Age).GreaterThan(0);
            RuleFor(x => x.Age).InRange(1, 150);
        }
    }

    private class CustomMessageValidator : DataValidator<TestItem>
    {
        public CustomMessageValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required!");
        }
    }

    public class TestItem
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }
}