using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Pipeline;

internal static class SortEngine
{
    public static async IAsyncEnumerable<T> SortAsync<T>(
        IAsyncEnumerable<T> source,
        IReadOnlyList<SortKeySpec> sortKeys,
        ExternalSortOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (sortKeys.Count == 0)
        {
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }

            yield break;
        }

        var maxInMemory = options?.MaxInMemoryRows ?? 100_000;
        var runBufferSize = options?.RunBufferRows ?? 10_000;
        var tempDir = options?.TempDirectory ?? Path.GetTempPath();
        var sessionId = Guid.NewGuid().ToString("N");
        var runPaths = new List<string>();
        var inMemory = new List<T>(runBufferSize);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            inMemory.Add(item);
            if (inMemory.Count >= runBufferSize)
            {
                runPaths.Add(await WriteRunAsync(inMemory, sortKeys, tempDir, sessionId, runPaths.Count, cancellationToken).ConfigureAwait(false));
                inMemory = new List<T>(runBufferSize);
            }
        }

        if (inMemory.Count > 0)
        {
            if (runPaths.Count == 0 && inMemory.Count <= maxInMemory)
            {
                foreach (var sorted in SortInMemory(inMemory, sortKeys))
                {
                    yield return sorted;
                }

                yield break;
            }

            runPaths.Add(await WriteRunAsync(inMemory, sortKeys, tempDir, sessionId, runPaths.Count, cancellationToken).ConfigureAwait(false));
        }

        if (runPaths.Count == 0)
        {
            yield break;
        }

        if (runPaths.Count == 1)
        {
            await foreach (var item in ReadRunAsync<T>(runPaths[0], cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }

            try { File.Delete(runPaths[0]); } catch { }
            yield break;
        }

        try
        {
            await foreach (var item in MergeRunsAsync<T>(runPaths, sortKeys, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            foreach (var path in runPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // ponytail: best-effort temp cleanup
                }
            }
        }
    }

    private static IEnumerable<T> SortInMemory<T>(List<T> items, IReadOnlyList<SortKeySpec> sortKeys)
    {
        IOrderedEnumerable<T>? ordered = null;
        for (var i = 0; i < sortKeys.Count; i++)
        {
            var spec = sortKeys[i];
            Func<T, object?> key = x => spec.KeySelector(x!);
            if (i == 0)
            {
                ordered = spec.Descending
                    ? items.OrderByDescending(key, Comparer<object?>.Create(spec.Comparison))
                    : items.OrderBy(key, Comparer<object?>.Create(spec.Comparison));
            }
            else
            {
                ordered = spec.Descending
                    ? ordered!.ThenByDescending(key, Comparer<object?>.Create(spec.Comparison))
                    : ordered!.ThenBy(key, Comparer<object?>.Create(spec.Comparison));
            }
        }

        if (ordered != null)
        {
            return ordered;
        }

        return items;
    }

    private static async Task<string> WriteRunAsync<T>(
        List<T> items,
        IReadOnlyList<SortKeySpec> sortKeys,
        string tempDir,
        string sessionId,
        int runIndex,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(tempDir, $"dataforge-sort-{sessionId}-{runIndex:D4}.ndjson");
        var sorted = SortInMemory(items, sortKeys);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        foreach (var item in sorted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = JsonSerializer.Serialize(item);
            var bytes = System.Text.Encoding.UTF8.GetBytes(line + "\n");
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        return path;
    }

    private static async IAsyncEnumerable<T> ReadRunAsync<T>(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(path);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null)
            {
                yield break;
            }

            yield return JsonSerializer.Deserialize<T>(line)!;
        }
    }

    private static async IAsyncEnumerable<T> MergeRunsAsync<T>(
        IReadOnlyList<string> runPaths,
        IReadOnlyList<SortKeySpec> sortKeys,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var readers = new StreamReader[runPaths.Count];
        var heads = new (T Item, int RunIndex)[runPaths.Count];
        var hasHead = new bool[runPaths.Count];
        var comparer = CreateComparer<T>(sortKeys);

        for (var i = 0; i < runPaths.Count; i++)
        {
            readers[i] = new StreamReader(runPaths[i]);
            var line = await readers[i].ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line != null)
            {
                heads[i] = (JsonSerializer.Deserialize<T>(line)!, i);
                hasHead[i] = true;
            }
        }

        try
        {
            while (true)
            {
                var bestIndex = -1;
                for (var i = 0; i < heads.Length; i++)
                {
                    if (!hasHead[i])
                    {
                        continue;
                    }

                    if (bestIndex < 0 || comparer.Compare(heads[i], heads[bestIndex]) < 0)
                    {
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                {
                    yield break;
                }

                yield return heads[bestIndex].Item;
                var nextLine = await readers[bestIndex].ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (nextLine == null)
                {
                    hasHead[bestIndex] = false;
                }
                else
                {
                    heads[bestIndex] = (JsonSerializer.Deserialize<T>(nextLine)!, bestIndex);
                }
            }
        }
        finally
        {
            foreach (var reader in readers)
            {
                reader?.Dispose();
            }
        }
    }

    private static Comparer<(T Item, int RunIndex)> CreateComparer<T>(IReadOnlyList<SortKeySpec> sortKeys)
    {
        return Comparer<(T Item, int RunIndex)>.Create((a, b) =>
        {
            foreach (var spec in sortKeys)
            {
                var cmp = spec.Comparison(spec.KeySelector(a.Item!), spec.KeySelector(b.Item!));
                if (cmp != 0)
                {
                    return spec.Descending ? -cmp : cmp;
                }
            }

            return a.RunIndex.CompareTo(b.RunIndex);
        });
    }
}
