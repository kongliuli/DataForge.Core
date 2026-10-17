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
    public async Task OnErrorContinue_SelectAsync_SkipsFailingItems()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .OnErrorContinue()
            .SelectAsync<int>(async x =>
            {
                await Task.Yield();
                return x == 2 ? throw new InvalidOperationException("fail") : x * 10;
            });

        var result = await pipeline.ToListAsync();
        result.Should().BeEquivalentTo(new[] { 10, 30 });
    }

    [Fact]
    public async Task OnErrorContinue_SelectMany_SkipsFailingItems()
    {
        var data = new[] { 1, 2, 3 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .OnErrorContinue()
            .SelectMany<int>(x => x == 2
                ? throw new InvalidOperationException("fail")
                : new[] { x, x * 10 });

        var result = await pipeline.ToListAsync();
        result.Should().BeEquivalentTo(new[] { 1, 10, 3, 30 });
    }

    [Fact]
    public async Task ValidateWith_AfterSkip_StillApplies()
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
            .ContinueOnValidationError()
            .Skip(0)
            .Take(10);

        var result = await pipeline.ToListAsync();
        result.Should().HaveCount(2);
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

    [Fact]
    public async Task ValidateWith_BeforeGroupBy_FiltersInvalidItems()
    {
        var validator = new TestValidator();
        var data = new[]
        {
            new TestItem { Name = "A", Age = 20 },
            new TestItem { Name = string.Empty, Age = 20 },
            new TestItem { Name = "B", Age = 20 },
        };

        var groups = await DataForgePipeline.FromMemory(data)
            .ValidateWith(validator)
            .ContinueOnValidationError()
            .GroupBy(x => x.Age)
            .ToListAsync();

        groups.Should().HaveCount(1);
        groups[0].Should().HaveCount(2);
    }

    [Fact]
    public async Task ValidateWith_BeforeBatch_FiltersInvalidItems()
    {
        var validator = new TestValidator();
        var data = new[]
        {
            new TestItem { Name = "A", Age = 1 },
            new TestItem { Name = string.Empty, Age = 2 },
            new TestItem { Name = "B", Age = 3 },
        };

        var batches = await DataForgePipeline.FromMemory(data)
            .ValidateWith(validator)
            .ContinueOnValidationError()
            .Batch(10)
            .ToListAsync();

        batches.Should().HaveCount(1);
        batches[0].Select(x => x.Name).Should().BeEquivalentTo(new[] { "A", "B" });
    }

    [Fact]
    public async Task OnErrorContinue_TransformWith_SkipsFailingItems()
    {
        var data = new[] { 1, 2, 3 };
        var result = await DataForgePipeline.FromMemory(data)
            .OnErrorContinue()
            .TransformWith(new FailingTimesTenTransform())
            .ToListAsync();

        result.Should().BeEquivalentTo(new[] { 10, 30 });
    }

    [Fact]
    public async Task ValidateWith_BeforeZip_FiltersLeftSide()
    {
        var validator = new TestValidator();
        var left = new[]
        {
            new TestItem { Name = "A", Age = 1 },
            new TestItem { Name = string.Empty, Age = 2 },
            new TestItem { Name = "C", Age = 3 },
        };
        var right = DataForgePipeline.FromMemory(new[] { 10, 20, 30 });

        var zipped = await DataForgePipeline.FromMemory(left)
            .ValidateWith(validator)
            .ContinueOnValidationError()
            .Zip(right)
            .ToListAsync();

        // Left drops invalid "Age=2"; Zip stops at shorter sequence → (A,10), (C,20)
        zipped.Should().HaveCount(2);
        zipped[0].First.Name.Should().Be("A");
        zipped[0].Second.Should().Be(10);
        zipped[1].First.Name.Should().Be("C");
        zipped[1].Second.Should().Be(20);
    }

    private sealed class FailingTimesTenTransform : DataForge.Core.Core.Transforms.IDataTransform<int, int>
    {
        public int Transform(int source) =>
            source == 2 ? throw new InvalidOperationException("fail") : source * 10;
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