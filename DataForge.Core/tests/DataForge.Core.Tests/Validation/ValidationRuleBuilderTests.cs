using DataForge.Core.Core.Validation;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Validation;

public class ValidationRuleBuilderTests
{
    [Fact]
    public void Required_Validates_NonEmpty_Value()
    {
        var validator = new TestValidator();
        var validItem = new TestItem { Name = "John", Age = 25, Email = "john@test.com" };
        var invalidItem = new TestItem { Name = "", Age = 25, Email = "john@test.com" };

        var validResult = validator.Validate(validItem);
        var invalidResult = validator.Validate(invalidItem);

        validResult.IsValid.Should().BeTrue();
        invalidResult.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Length_Validates_String_Length()
    {
        var validator = new TestValidator();
        var shortItem = new TestItem { Name = "J" };
        var longItem = new TestItem { Name = "This is a really long name that should exceed the length limit" };

        var shortResult = validator.Validate(shortItem);
        var longResult = validator.Validate(longItem);

        shortResult.IsValid.Should().BeFalse();
        longResult.IsValid.Should().BeFalse();
    }

    [Fact]
    public void InRange_Validates_Numeric_Range()
    {
        var validator = new TestValidator();
        var lowItem = new TestItem { Age = 17, Name = "John", Email = "john@test.com" };
        var validItem = new TestItem { Age = 25, Name = "John", Email = "john@test.com" };
        var highItem = new TestItem { Age = 101, Name = "John", Email = "john@test.com" };

        var lowResult = validator.Validate(lowItem);
        var validResult = validator.Validate(validItem);
        var highResult = validator.Validate(highItem);

        lowResult.IsValid.Should().BeFalse();
        validResult.IsValid.Should().BeTrue();
        highResult.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Must_Validates_Custom_Condition()
    {
        var validator = new TestValidator();
        var validEmail = new TestItem { Email = "test@example.com", Name = "John", Age = 25 };
        var invalidEmail = new TestItem { Email = "invalid-email", Name = "John", Age = 25 };

        var validResult = validator.Validate(validEmail);
        var invalidResult = validator.Validate(invalidEmail);

        validResult.IsValid.Should().BeTrue();
        invalidResult.IsValid.Should().BeFalse();
    }

    private class TestValidator : DataValidator<TestItem>
    {
        public TestValidator()
        {
            RuleFor(x => x.Name)
                .Required()
                .Length(2, 50);

            RuleFor(x => x.Age)
                .InRange(18, 100);

            RuleFor(x => x.Email)
                .Must(e => e.Contains("@"));
        }
    }

    private class TestItem
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string Email { get; set; } = "";
    }
}
