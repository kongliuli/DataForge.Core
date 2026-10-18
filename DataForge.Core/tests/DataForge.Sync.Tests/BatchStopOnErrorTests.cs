using DataForge.Jobs.Execution;
using Xunit;

namespace DataForge.Sync.Tests;

public class BatchStopOnErrorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ParallelBatch_StopsAdmissionAfterFailureWhenRequested(bool stop)
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.csv").Replace('\\', '/');
        var yaml = $"execution: {{ parallelism: 2, stopOnError: {stop.ToString().ToLowerInvariant()} }}\njobs:\n";
        for (var i = 0; i < 20; i++)
            yaml += $"  - name: job{i}\n    source: {{ type: csv, path: '{missing}' }}\n    sink: {{ type: json, path: unused.json }}\n";
        var results = await JobBatch.RunYamlAsync(yaml);
        Assert.All(results, result => Assert.False(result.Success));
        if (stop)
            Assert.InRange(results.Count, 1, 2);
        else
            Assert.Equal(20, results.Count);
    }
}
