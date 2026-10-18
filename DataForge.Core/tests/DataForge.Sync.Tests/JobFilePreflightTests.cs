using DataForge.Jobs.Parsing;
using Xunit;

namespace DataForge.Sync.Tests;

public class JobFilePreflightTests
{
    [Fact]
    public async Task ChecksReadableAndMissingFilesWithoutCreatingSink()
    {
        var directory = Path.Combine(Path.GetTempPath(), "df-preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "input.csv"), "Id\n1\n");
            const string yaml = """
                jobs:
                  - source: { type: csv, path: '${INPUT}' }
                    sink: { type: json, path: output.json }
                  - source: { type: json, path: missing.json }
                    sink: { type: json, path: output2.json }
                """;
            var checks = await JobFilePreflight.CheckAsync(yaml, Path.Combine(directory, "job.yaml"),
                new Dictionary<string, string> { ["INPUT"] = "input.csv" });
            Assert.Equal(new[] { "passed", "failed" }, checks.Select(c => c.Status));
            Assert.False(File.Exists(Path.Combine(directory, "output.json")));
            Assert.False(File.Exists(Path.Combine(directory, "output2.json")));
            Assert.DoesNotContain(directory, checks[1].Message);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task PreCancelledCheck_DoesNotParseInput()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JobFilePreflight.CheckAsync("invalid: [", "job.yaml", cancellationToken: cancellation.Token));
    }
}
