using DataForge.Core.Core.Validation;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Validation;

public class ExtendedValidationTests
{
    [Fact]
    public void Must_WithValidValue_Passes()
    {
        var validator = new MustValidator();
        var item = new TestItem { Name = "hello world" };

        var result = validator.Validate(item);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Must_WithInvalidValue_Fails()
    {
        var validator = new MustValidator();
        var item = new TestItem { Name = "hi" };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void When_ConditionFalse_SkipsValidation()
    {
        var validator = new WhenValidator();
        var item = new TestItem { Name = "", IsActive = false };

        var result = validator.Validate(item);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void When_ConditionTrue_RunsValidation()
    {
        var validator = new WhenValidator();
        var item = new TestItem { Name = "", IsActive = true };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void MinLength_WithShortString_Fails()
    {
        var validator = new MinLengthValidator();
        var item = new TestItem { Name = "ab" };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void MinLength_WithLongEnoughString_Passes()
    {
        var validator = new MinLengthValidator();
        var item = new TestItem { Name = "abcde" };

        var result = validator.Validate(item);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GreaterThanOrEqualTo_WithEqualValue_Passes()
    {
        var validator = new GreaterThanOrEqualValidator();
        var item = new TestItem { Age = 18 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GreaterThanOrEqualTo_WithLessValue_Fails()
    {
        var validator = new GreaterThanOrEqualValidator();
        var item = new TestItem { Age = 10 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void LessThan_WithLessValue_Passes()
    {
        var validator = new LessThanValidator();
        var item = new TestItem { Age = 50 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void LessThan_WithGreaterValue_Fails()
    {
        var validator = new LessThanValidator();
        var item = new TestItem { Age = 200 };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void WithSeverity_SetsErrorSeverity()
    {
        var validator = new SeverityValidator();
        var item = new TestItem { Name = "" };

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Severity.Should().Be(ValidationSeverity.Warning);
    }

    [Fact]
    public void ValidationError_DefaultSeverity_IsError()
    {
        var error = new ValidationError { PropertyName = "Test", Message = "test" };

        error.Severity.Should().Be(ValidationSeverity.Error);
    }

    private class MustValidator : DataValidator<TestItem>
    {
        public MustValidator()
        {
            RuleFor(x => x.Name).Must(x => x.Length >= 5);
        }
    }

    private class WhenValidator : DataValidator<TestItem>
    {
        public WhenValidator()
        {
            RuleFor(x => x.Name).NotEmpty().When(x => x.IsActive);
        }
    }

    private class MinLengthValidator : DataValidator<TestItem>
    {
        public MinLengthValidator()
        {
            RuleFor(x => x.Name).MinLength(3);
        }
    }

    private class GreaterThanOrEqualValidator : DataValidator<TestItem>
    {
        public GreaterThanOrEqualValidator()
        {
            RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
        }
    }

    private class LessThanValidator : DataValidator<TestItem>
    {
        public LessThanValidator()
        {
            RuleFor(x => x.Age).LessThan(100);
        }
    }

    private class SeverityValidator : DataValidator<TestItem>
    {
        public SeverityValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithSeverity(ValidationSeverity.Warning);
        }
    }

    public class TestItem
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public bool IsActive { get; set; }
    }
}