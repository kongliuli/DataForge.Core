using DataForge.Core.Core.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Infrastructure;

public class InfrastructureTests
{
    [Fact]
    public void DefaultTypeConverter_ConvertsStringToInt()
    {
        var converter = new DefaultTypeConverter();

        var result = converter.Convert<int>("42");

        result.Should().Be(42);
    }

    [Fact]
    public void DefaultTypeConverter_ConvertsStringToDecimal()
    {
        var converter = new DefaultTypeConverter();

        var result = converter.Convert<decimal>("3.14");

        result.Should().Be(3.14m);
    }

    [Fact]
    public void DefaultTypeConverter_ConvertsStringToBool()
    {
        var converter = new DefaultTypeConverter();

        converter.Convert<bool>("true").Should().BeTrue();
        converter.Convert<bool>("false").Should().BeFalse();
    }

    [Fact]
    public void DefaultTypeConverter_ConvertsIntToString()
    {
        var converter = new DefaultTypeConverter();

        var result = converter.Convert<string>(42);

        result.Should().Be("42");
    }

    [Fact]
    public void DefaultTypeConverter_CanConvert_SameType()
    {
        var converter = new DefaultTypeConverter();

        converter.CanConvert(typeof(int), typeof(int)).Should().BeTrue();
    }

    [Fact]
    public void DefaultTypeConverter_CanConvert_StringToNumeric()
    {
        var converter = new DefaultTypeConverter();

        converter.CanConvert(typeof(string), typeof(int)).Should().BeTrue();
        converter.CanConvert(typeof(string), typeof(decimal)).Should().BeTrue();
    }

    [Fact]
    public void DataForgeException_HasErrorCode()
    {
        var ex = new DataForgeException("test error", "TEST_001");

        ex.ErrorCode.Should().Be("TEST_001");
        ex.Message.Should().Be("test error");
    }

    [Fact]
    public void DataSourceException_InheritsFromDataForgeException()
    {
        var ex = new DataSourceException("source error", "MySource");

        ex.Should().BeAssignableTo<DataForgeException>();
        ex.SourceName.Should().Be("MySource");
    }

    [Fact]
    public void DataTargetException_InheritsFromDataForgeException()
    {
        var ex = new DataTargetException("target error", "MyTarget");

        ex.Should().BeAssignableTo<DataForgeException>();
        ex.TargetName.Should().Be("MyTarget");
    }

    [Fact]
    public void TransformException_HasTypeInformation()
    {
        var ex = new TransformException("transform error", typeof(string), typeof(int));

        ex.InputType.Should().Be(typeof(string));
        ex.OutputType.Should().Be(typeof(int));
    }
}