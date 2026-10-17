using DataForge.Core;
using DataForge.Core.Core.Transforms;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Transforms;

public class CommonTransformsTests
{
    [Fact]
    public async Task MapTransform_MapsValues()
    {
        var result = await DataForgePipeline.FromMemory(new[] { 1, 2, 3 })
            .TransformWith(new MapTransform<int, int>(x => x * 10))
            .ToListAsync();

        result.Should().Equal(10, 20, 30);
    }

    [Fact]
    public async Task AsyncMapTransform_ViaTransformWithAsync()
    {
        var result = await DataForgePipeline.FromMemory(new[] { 1, 2, 3 })
            .TransformWithAsync(new AsyncMapTransform<int, int>(async x =>
            {
                await Task.Yield();
                return x * 10;
            }))
            .ToListAsync();

        result.Should().Equal(10, 20, 30);
    }

    [Fact]
    public async Task LookupTransform_ReturnsValueOrDefault()
    {
        var lookup = new Dictionary<string, int>
        {
            ["a"] = 1,
            ["b"] = 2
        };

        var result = await DataForgePipeline.FromMemory(new[] { "a", "missing", "b" })
            .TransformWith(new LookupTransform<string, int>(lookup))
            .ToListAsync();

        result.Should().Equal(1, 0, 2);
    }

    [Fact]
    public async Task ComposeTransform_ChainsTransforms()
    {
        var composed = new ComposeTransform<string, string, string>(
            new TrimTransform(),
            new ToCaseTransform(toUpper: true));

        var result = await DataForgePipeline.FromMemory(new[] { "  hi ", "Yo" })
            .TransformWith(composed)
            .ToListAsync();

        result.Should().Equal("HI", "YO");
    }

    [Fact]
    public async Task ConditionalTransform_ChoosesBranch()
    {
        var transform = new ConditionalTransform<int, string>(
            x => x % 2 == 0,
            _ => "even",
            _ => "odd");

        var result = await DataForgePipeline.FromMemory(new[] { 1, 2, 3 })
            .TransformWith(transform)
            .ToListAsync();

        result.Should().Equal("odd", "even", "odd");
    }

    [Fact]
    public async Task CastTransform_SucceedsForCompatibleType()
    {
        object[] data = { "a", "b" };
        var result = await DataForgePipeline.FromMemory(data)
            .TransformWith(new CastTransform<object, string>())
            .ToListAsync();

        result.Should().Equal("a", "b");
    }

    [Fact]
    public async Task CastTransform_ThrowsOnIncompatibleType()
    {
        object[] data = { 1 };
        var pipeline = DataForgePipeline.FromMemory(data)
            .TransformWith(new CastTransform<object, string>());

        var act = async () => await pipeline.ToListAsync();
        await act.Should().ThrowAsync<InvalidCastException>();
    }

    [Fact]
    public async Task DefaultIfEmptyTransform_ReplacesNull()
    {
        var result = await DataForgePipeline.FromMemory(new[] { "a", null, "b" })
            .TransformWith(new DefaultIfEmptyTransform<string?>("x"))
            .ToListAsync();

        result.Should().Equal("a", "x", "b");
    }

    [Fact]
    public async Task SkipNullsTransform_ThrowsOnNull_UnlessOnErrorContinue()
    {
        var failing = DataForgePipeline.FromMemory(new[] { "a", null, "b" })
            .TransformWith(new SkipNullsTransform<string>());

        var act = async () => await failing.ToListAsync();
        await act.Should().ThrowAsync<ArgumentNullException>();

        var skipped = await DataForgePipeline.FromMemory(new[] { "a", null, "b" })
            .OnErrorContinue()
            .TransformWith(new SkipNullsTransform<string>())
            .ToListAsync();

        skipped.Should().Equal("a", "b");
    }

    [Fact]
    public async Task FormatTransforms_ProduceExpectedStrings()
    {
        var date = await DataForgePipeline.FromMemory(new[] { new DateTime(2026, 3, 22) })
            .TransformWith(new DateFormatTransform("yyyy-MM-dd"))
            .ToListAsync();
        date.Should().Equal("2026-03-22");

        var number = await DataForgePipeline.FromMemory(new[] { 12.3m })
            .TransformWith(new NumberFormatTransform("0.00"))
            .ToListAsync();
        number.Should().Equal("12.30");

        var boolean = await DataForgePipeline.FromMemory(new[] { true, false })
            .TransformWith(new BoolFormatTransform("Y", "N"))
            .ToListAsync();
        boolean.Should().Equal("Y", "N");
    }

    [Fact]
    public async Task WhereAsync_OnErrorContinue_SkipsFailingPredicate()
    {
        var result = await DataForgePipeline.FromMemory(new[] { 1, 2, 3, 4 })
            .OnErrorContinue()
            .WhereAsync(async x =>
            {
                await Task.Yield();
                if (x == 3) throw new InvalidOperationException("boom");
                return x % 2 == 0;
            })
            .ToListAsync();

        result.Should().Equal(2, 4);
    }

    [Fact]
    public async Task TransformWithAsync_OnErrorContinue_SkipsFailures()
    {
        var result = await DataForgePipeline.FromMemory(new[] { 1, 2, 3 })
            .OnErrorContinue()
            .TransformWithAsync(new AsyncMapTransform<int, int>(async x =>
            {
                await Task.Yield();
                if (x == 2) throw new InvalidOperationException("boom");
                return x * 10;
            }))
            .ToListAsync();

        result.Should().Equal(10, 30);
    }
}
