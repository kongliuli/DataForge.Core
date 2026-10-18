using DataForge.Jobs.Parsing;
using Xunit;

namespace DataForge.Sync.Tests;

public class JobConfigurationValidatorTests
{
    [Fact]
    public void ValidConfiguration_DoesNotRequireExistingSource()
    {
        var report = JobConfigurationValidator.Validate("""
            source: { type: csv, path: does-not-exist.csv }
            sink: { type: json, path: unused.json }
            """);
        Assert.True(report.Valid);
        Assert.Equal(1, report.JobCount);
        Assert.Null(report.Error);
        Assert.Equal("configuration-only", report.Scope);
        Assert.Contains("connectivity", report.ChecksNotPerformed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("jobs: []")]
    [InlineData("secret: [private-value")]
    public void InvalidConfiguration_ReturnsSafeReport(string? yaml)
    {
        var report = JobConfigurationValidator.Validate(yaml);
        Assert.False(report.Valid);
        Assert.Equal(0, report.JobCount);
        Assert.NotNull(report.Error);
        Assert.DoesNotContain("private-value", report.Error);
    }
}
