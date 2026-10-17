using DataForge.Core.Core.Infrastructure;
using DataForge.Core.SqlServer;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Infrastructure;

public class SqlIdentifiersTests
{
    [Theory]
    [InlineData("Orders")]
    [InlineData("dbo.Orders")]
    [InlineData("_tmp")]
    [InlineData("t1")]
    public void Validate_AcceptsSimpleIdentifiers(string identifier)
    {
        SqlIdentifiers.Validate(identifier).Should().Be(identifier.Trim());
    }

    [Theory]
    [InlineData("Orders; DROP TABLE Users")]
    [InlineData("Orders--")]
    [InlineData("1Orders")]
    [InlineData("schema.table.extra")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsUnsafeIdentifiers(string identifier)
    {
        var act = () => SqlIdentifiers.Validate(identifier);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QuoteSqlServer_WrapsSegments()
    {
        SqlIdentifiers.QuoteSqlServer("dbo.Orders").Should().Be("[dbo].[Orders]");
    }

    [Fact]
    public void QuoteMySql_WrapsSegments()
    {
        SqlIdentifiers.QuoteMySql("dbo.Orders").Should().Be("`dbo`.`Orders`");
    }

    [Fact]
    public void QuoteSqlite_WrapsSegments()
    {
        SqlIdentifiers.QuoteSqlite("dbo.Orders").Should().Be("\"dbo\".\"Orders\"");
    }

    [Fact]
    public void SqlServerSource_RejectsUnsafeTableName()
    {
        var act = () => new SqlServerSource<object>("Server=.;Database=db;Trusted_Connection=True", "Orders;DROP");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task SqlServerSource_Metadata_DoesNotExposeConnectionString()
    {
        var source = new SqlServerSource<object>("Server=.;Database=SecretDb;User Id=sa;Password=p@ss", "Orders");
        var metadata = await source.GetMetadataAsync();

        metadata.AdditionalInfo.Should().NotContainKey("ConnectionString");
        metadata.AdditionalInfo.Should().ContainKey("Table");
        metadata.Location.Should().Be("Orders");
    }
}
