using DataForge.Jobs.Execution;
using DataForge.Jobs.Models;
using Xunit;

namespace DataForge.Sync.Tests;

public class JobCancellationTests
{
    [Fact]
    public async Task CancelledRunner_PropagatesCancellationForBothOverloads()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var runner = new JobRunner();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync("missing.yaml", cancellationToken: cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(new JobDefinition(), "job.yaml", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task PreCancelledBatch_ThrowsBeforeFileLookupOrYamlParsing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JobBatch.RunFileAsync("missing.yaml", cancellationToken: cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JobBatch.RunYamlAsync("invalid: [", cancellationToken: cts.Token));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Batch_CancelledAtStartDoesNotBecomeFailureResult(int parallelism)
    {
        using var cts = new CancellationTokenSource();
        var yaml = $$"""
            execution:
              parallelism: {{parallelism}}
            jobs:
              - name: first
                source: { type: csv, path: ./missing.csv }
                sink: { type: json, path: ./unused.json }
              - name: second
                source: { type: csv, path: ./missing.csv }
                sink: { type: json, path: ./unused2.json }
            """;
        var progress = new CancelProgress(cts);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JobBatch.RunYamlAsync(yaml, cancellationToken: cts.Token, progress: progress));
        Assert.Equal(1, progress.Reports);
    }

    private sealed class CancelProgress(CancellationTokenSource cts) : IProgress<JobProgress>
    {
        public int Reports { get; private set; }
        public void Report(JobProgress value) { Reports++; cts.Cancel(); }
    }
}
