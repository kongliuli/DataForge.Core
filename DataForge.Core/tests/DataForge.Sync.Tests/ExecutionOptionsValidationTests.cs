using DataForge.Jobs.Parsing;
using Xunit;

namespace DataForge.Sync.Tests;

public class ExecutionOptionsValidationTests
{
    private const string Job = "source: { type: csv, path: in.csv }\nsink: { type: json, path: out.json }\n";

    [Theory]
    [InlineData("parallelism", 0)]
    [InlineData("parallelism", -1)]
    [InlineData("progressEvery", -1)]
    [InlineData("batchSize", 0)]
    [InlineData("batchSize", -1)]
    public void InvalidExecutionOptions_RejectedAtJobAndBatchLevel(string key, int value)
    {
        Assert.Throws<InvalidOperationException>(() => JobYamlParser.ParseFile(Job + $"execution: {{ {key}: {value} }}"));
        var batch = $"execution: {{ {key}: {value} }}\njobs:\n  - source: {{ type: csv, path: in.csv }}\n    sink: {{ type: json, path: out.json }}";
        Assert.Throws<InvalidOperationException>(() => JobYamlParser.ParseFile(batch));
    }

    [Fact]
    public void ZeroProgressCadenceAndPositiveBatchSize_AreValid()
    {
        Assert.Single(JobYamlParser.ParseMany(Job + "execution: { parallelism: 1, progressEvery: 0, batchSize: 1 }"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidSinkBatchSize_IsRejected(int size)
    {
        var yaml = $"source: {{ type: csv, path: in.csv }}\nsink: {{ type: json, path: out.json, options: {{ batchSize: {size} }} }}";
        Assert.Throws<InvalidOperationException>(() => JobYamlParser.Parse(yaml));
    }
}
