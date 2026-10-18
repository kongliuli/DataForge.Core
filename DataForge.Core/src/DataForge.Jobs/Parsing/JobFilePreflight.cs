using DataForge.Jobs.Execution;

namespace DataForge.Jobs.Parsing;

/// <summary>Non-writing checks of file source readability. Does not scan records or test sinks.</summary>
public static class JobFilePreflight
{
    public static async Task<IReadOnlyList<FileSourceCheck>> CheckAsync(
        string yaml, string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var jobs = JobYamlParser.ParseMany(yaml);
        var directory = Path.GetDirectoryName(Path.GetFullPath(jobFilePath))!;
        var checks = new List<FileSourceCheck>();
        for (var i = 0; i < jobs.Count; i++)
        {
            var job = VariableResolver.Apply(jobs[i], variables ?? new Dictionary<string, string>());
            var sources = job.Sources.Count > 0 ? job.Sources.Select(s => s.ToSource()) : [job.Source];
            var sourceIndex = 0;
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sourceIndex++;
                if (source.Type.ToLowerInvariant() is not ("csv" or "json" or "excel" or "parquet"))
                {
                    checks.Add(new(i + 1, sourceIndex, "not-checked", "This source requires a separate connection check."));
                    continue;
                }
                try
                {
                    var path = VariableResolver.ResolvePath(source.Path, directory);
                    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                    var buffer = new byte[1];
                    await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    checks.Add(new(i + 1, sourceIndex, "passed", "File can be opened for reading; format and records are not validated."));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    // Never expose a resolved path or credentials through OS exception messages.
                    checks.Add(new(i + 1, sourceIndex, "failed", "Source file cannot be opened for reading."));
                }
            }
        }
        return checks;
    }
}

public sealed record FileSourceCheck(int JobIndex, int SourceIndex, string Status, string Message);
