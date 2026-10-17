using DataForge.Jobs;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.Execution;

internal static class JobJoin
{
    public static async IAsyncEnumerable<JobRow> ReadJoinedAsync(
        JobDefinition job,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sources = job.Sources;
        if (sources.Count == 0)
            throw new InvalidOperationException("Join requires sources.");

        var tables = new List<List<JobRow>>(sources.Count);
        foreach (var source in sources)
        {
            var rows = new List<JobRow>();
            await foreach (var row in ReadOne(job, source.ToSource(), cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(Prefix(row, source.Alias));
            }

            tables.Add(rows);
        }

        IEnumerable<JobRow> current = tables[0];
        for (var i = 1; i < sources.Count; i++)
        {
            var on = sources[i].On;
            if (string.IsNullOrWhiteSpace(on) || !on.Contains('='))
                throw new InvalidOperationException($"Join source {i} requires on: LeftCol = RightCol.");

            var parts = on.Split('=', 2, StringSplitOptions.TrimEntries);
            var leftKey = parts[0];
            var rightKey = parts[1];
            var rightRows = tables[i];
            current = InnerJoin(current, rightRows, leftKey, rightKey);
        }

        foreach (var row in current)
            yield return row;
    }

    private static IEnumerable<JobRow> InnerJoin(
        IEnumerable<JobRow> leftRows,
        List<JobRow> rightRows,
        string leftKey,
        string rightKey)
    {
        foreach (var left in leftRows)
        {
            var leftValue = left.Get(leftKey);
            foreach (var right in rightRows)
            {
                if (!string.Equals(leftValue, right.Get(rightKey), StringComparison.OrdinalIgnoreCase))
                    continue;

                var merged = left.Clone();
                foreach (var (key, value) in right.Values)
                    merged.Values[key] = value;
                yield return merged;
            }
        }
    }

    private static JobRow Prefix(JobRow row, string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return row;

        var prefixed = new JobRow();
        foreach (var (key, value) in row.Values)
            prefixed.Values[$"{alias}_{key}"] = value;
        return prefixed;
    }

    private static IAsyncEnumerable<JobRow> ReadOne(
        JobDefinition owner,
        SourceDefinition source,
        CancellationToken cancellationToken)
    {
        if (!JobProviderRegistry.TryGetSource(source.Type, out var provider))
            throw new NotSupportedException(
                $"Source type '{source.Type}' is not registered. Reference the matching DataForge.Jobs.* adapter and call its registration method.");

        var probe = new JobDefinition
        {
            Name = owner.Name,
            Source = source
        };
        return provider.Create(probe).AsAsyncEnumerable(cancellationToken);
    }
}
