using DataForge.Jobs.Parsing;
using Xunit;

namespace DataForge.Sync.Tests;

public class YamlStructureTests
{
    private const string Single = "name: sample\nsource:\n  type: csv\n  path: ./in.csv\nsink:\n  type: json\n  path: ./out.json";

    [Fact]
    public void SingleParserRejectsBatchWhileBatchParserPreservesAllJobs()
    {
        var item = "  - " + Single.Replace("\n", "\n    ");
        var yaml = "jobs:\n" + item + "\n" + item;
        var error = Assert.Throws<InvalidOperationException>(() => JobYamlParser.Parse(yaml));
        Assert.Contains("JobBatch", error.Message);
        Assert.Equal(2, JobYamlParser.ParseMany(yaml).Count);
    }

    [Fact]
    public void QuotedJobsKey_IsRecognizedAsBatch()
    {
        var yaml = "'jobs':\n  - " + Single.Replace("\n", "\n    ");
        Assert.Equal("sample", Assert.Single(JobYamlParser.ParseMany(yaml)).Name);
    }

    [Fact]
    public void JobsInComment_DoesNotChangeSingleJobShape()
    {
        var file = JobYamlParser.ParseFile("# jobs: example\n" + Single);
        Assert.NotNull(file.SourceJob);
        Assert.Single(file.Jobs);
    }

    [Theory]
    [InlineData("jobs: []")]
    [InlineData("jobs: null")]
    [InlineData("jobs: {}")]
    public void EmptyOrNonSequenceBatch_IsRejected(string yaml)
    {
        var error = Assert.Throws<InvalidOperationException>(() => JobYamlParser.ParseFile(yaml));
        Assert.Contains("non-empty sequence", error.Message);
    }

    [Fact]
    public void MultipleDocuments_AreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => JobYamlParser.ParseFile(Single + "\n---\n" + Single));
    }
}
