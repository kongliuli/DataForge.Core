using DataForge.Core;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Validation;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Pipeline;

public class ErrorHandlingTests
{
    [Fact]
    public async Task OnErrorContinue_SkipsFailingItems()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .OnErrorContinue()
            .Select<int>(x => x == 2 ? throw new InvalidOperationException("fail") : x * 10);

        var result = await pipeline.ToListAsync();
        result.Should().BeEquivalentTo(new[] { 10, 30 });
    }

    [Fact]
    public async Task OnErrorSkip_SkipsFailingItems()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .OnErrorSkip()
            .Select<int>(x => x == 2 ? throw new InvalidOperationException("fail") : x * 10);

        var result = await pipeline.ToListAsync();
        result.Should().BeEquivalentTo(new[] { 10, 30 });
    }

    [Fact]
    public async Task OnErrorStop_StopsOnFirstFailure()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .OnErrorStop()
            .Select<int>(x => x == 2 ? throw new InvalidOperationException("fail") : x * 10);

        Func<Task> act = () => pipeline.ToListAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OnErrorCustomHandler_ContinueAction_SkipsItem()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .OnError((ex, item) => ErrorAction.Continue)
            .Select<int>(x => x == 2 ? throw new InvalidOperationException("fail") : x * 10);

        var result = await pipeline.ToListAsync();
        result.Should().BeEquivalentTo(new[] { 10, 30 });
    }

    [Fact]
    public async Task ContinueOnValidationError_SkipsInvalidItems()
    {
        var validator = new TestValidator();
        var data = new[]
        {
            new TestItem { Name = "Valid", Age = 25 },
            new TestItem { Name = string.Empty, Age = 25 },
            new TestItem { Name = "Also Valid", Age = 30 },
        };

        var pipeline = DataForgePipeline.FromMemory(data)
            .ValidateWith(validator)
            .ContinueOnValidationError();

        var result = await pipeline.ToListAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task FailOnValidationError_ThrowsOnInvalid()
    {
        var validator = new TestValidator();
        var data = new[]
        {
            new TestItem { Name = "Valid", Age = 25 },
            new TestItem { Name = string.Empty, Age = 25 },
        };

        var pipeline = DataForgePipeline.FromMemory(data)
            .ValidateWith(validator)
            .FailOnValidationError();

        Func<Task> act = () => pipeline.ToListAsync();
        await act.Should().ThrowAsync<ValidationException>();
    }

    public class TestItem
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    public class TestValidator : DataValidator<TestItem>
    {
        public TestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}