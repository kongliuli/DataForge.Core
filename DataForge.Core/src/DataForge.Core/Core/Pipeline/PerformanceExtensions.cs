using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using DataForge.Core.Core.Transforms;
using DataForge.Core.Core.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Pipeline;

public static class PerformanceExtensions
{
    /// <summary>
    /// 添加进度报告
    /// </summary>
    public static IDataPipeline<T> WithProgress<T>(
        this IDataPipeline<T> pipeline,
        Action<ProgressReport<T>> progressHandler,
        int reportInterval = 1000)
    {
        return new ProgressReportingPipeline<T>(pipeline, progressHandler, reportInterval);
    }

    /// <summary>
    /// 添加性能计数器
    /// </summary>
    public static IDataPipeline<T> WithCounter<T>(
        this IDataPipeline<T> pipeline,
        PerformanceCounter counter)
    {
        return new CounterReportingPipeline<T>(pipeline, counter);
    }

    /// <summary>
    /// 并行处理（保留顺序）
    /// </summary>
    public static IDataPipeline<T> WithParallelization<T>(
        this IDataPipeline<T> pipeline,
        int maxDegreeOfParallelism = 4)
    {
        return new ParallelPipeline<T>(pipeline, maxDegreeOfParallelism, preserveOrder: true);
    }

    /// <summary>
    /// 并行处理（不保留顺序，更快）
    /// </summary>
    public static IDataPipeline<T> WithParallelizationUnordered<T>(
        this IDataPipeline<T> pipeline,
        int maxDegreeOfParallelism = 4)
    {
        return new ParallelPipeline<T>(pipeline, maxDegreeOfParallelism, preserveOrder: false);
    }
}

internal class ProgressReportingPipeline<T> : IDataPipeline<T>
{
    private readonly IDataPipeline<T> _inner;
    private readonly Action<ProgressReport<T>> _progressHandler;
    private readonly int _reportInterval;
    private long _count;

    public ProgressReportingPipeline(IDataPipeline<T> inner, Action<ProgressReport<T>> progressHandler, int reportInterval)
    {
        _inner = inner;
        _progressHandler = progressHandler;
        _reportInterval = reportInterval;
    }

    public async IAsyncEnumerable<T> AsAsyncEnumerable([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in _inner.AsAsyncEnumerable(cancellationToken).ConfigureAwait(false))
        {
            _count++;
            if (_count % _reportInterval == 0)
            {
                _progressHandler(new ProgressReport<T>
                {
                    ProcessedCount = _count,
                    CurrentItem = item
                });
            }
            yield return item;
        }
    }

    // 实现其他接口方法...
    public IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector) => _inner.Select(selector);
    public IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector) => _inner.SelectAsync(selector);
    public IDataPipeline<TResult> SelectMany<TResult>(Func<T, IEnumerable<TResult>> selector) => _inner.SelectMany(selector);
    public IDataPipeline<T> Where(Func<T, bool> predicate) => _inner.Where(predicate);
    public IDataPipeline<T> WhereAsync(Func<T, Task<bool>> predicate) => _inner.WhereAsync(predicate);
    public IDataPipeline<T> OrderBy<TKey>(Func<T, TKey> keySelector) => _inner.OrderBy(keySelector);
    public IDataPipeline<T> OrderByDescending<TKey>(Func<T, TKey> keySelector) => _inner.OrderByDescending(keySelector);
    public IDataPipeline<T> ThenBy<TKey>(Func<T, TKey> keySelector) => _inner.ThenBy(keySelector);
    public IDataPipeline<T> ThenByDescending<TKey>(Func<T, TKey> keySelector) => _inner.ThenByDescending(keySelector);
    public IDataPipeline<T> Distinct() => _inner.Distinct();
    public IDataPipeline<T> DistinctBy<TKey>(Func<T, TKey> keySelector) => _inner.DistinctBy(keySelector);
    public IDataPipeline<T> Skip(int count) => _inner.Skip(count);
    public IDataPipeline<T> Take(int count) => _inner.Take(count);
    public IDataPipeline<List<T>> Batch(int batchSize) => _inner.Batch(batchSize);
    public IGroupedDataPipeline<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull => _inner.GroupBy(keySelector);
    public IDataPipeline<(T First, TSecond Second)> Zip<TSecond>(IDataPipeline<TSecond> second) => _inner.Zip(second);
    public IDataPipeline<TResult> TransformWith<TResult>(IDataTransform<T, TResult> transform) => _inner.TransformWith(transform);
    public IDataPipeline<T> ValidateWith(IValidator<T> validator) => _inner.ValidateWith(validator);
    public IDataPipeline<T> ContinueOnValidationError() => _inner.ContinueOnValidationError();
    public IDataPipeline<T> FailOnValidationError() => _inner.FailOnValidationError();
    public IDataPipeline<T> OnErrorContinue() => _inner.OnErrorContinue();
    public IDataPipeline<T> OnErrorStop() => _inner.OnErrorStop();
    public IDataPipeline<T> OnErrorSkip() => _inner.OnErrorSkip();
    public IDataPipeline<T> OnError(Func<Exception, T, ErrorAction> handler) => _inner.OnError(handler);
    public Task<TResult> AggregateAsync<TResult>(Func<TResult, T, TResult> aggregator, TResult seed, CancellationToken cancellationToken = default) => _inner.AggregateAsync(aggregator, seed, cancellationToken);
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) { var r = new List<T>(); await foreach (var i in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) r.Add(i); return r; }
    public async Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default) => (await ToListAsync(cancellationToken).ConfigureAwait(false)).ToArray();
    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) { await foreach (var i in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) return i; return default; }
    public async Task<int> CountAsync(CancellationToken cancellationToken = default) { var c = 0; await foreach (var _ in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) c++; return c; }
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) { await foreach (var _ in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) return true; return false; }
    public Task<ExportResults> ToCsvAsync(string filePath, CsvExportOptions? options = null, CancellationToken cancellationToken = default) => _inner.ToCsvAsync(filePath, options, cancellationToken);
    public Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default) => _inner.ToJsonAsync(filePath, options, cancellationToken);
    public Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default) => _inner.ToExcelAsync(filePath, options, cancellationToken);
    public Task<ExportResults> ToConsoleAsync(Func<T, string>? formatter = null, CancellationToken cancellationToken = default) => _inner.ToConsoleAsync(formatter, cancellationToken);
    public Task<ExportResults> ToStreamAsync(System.IO.Stream stream, ExportFormat format, CancellationToken cancellationToken = default) => _inner.ToStreamAsync(stream, format, cancellationToken);
}

internal class CounterReportingPipeline<T> : IDataPipeline<T>
{
    private readonly IDataPipeline<T> _inner;
    private readonly PerformanceCounter _counter;

    public CounterReportingPipeline(IDataPipeline<T> inner, PerformanceCounter counter)
    {
        _inner = inner;
        _counter = counter;
    }

    public async IAsyncEnumerable<T> AsAsyncEnumerable([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _counter.Start();
        await foreach (var item in _inner.AsAsyncEnumerable(cancellationToken).ConfigureAwait(false))
        {
            _counter.IncrementProcessed();
            yield return item;
        }
        _counter.Stop();
    }

    // 实现其他接口方法...
    public IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector) => _inner.Select(selector);
    public IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector) => _inner.SelectAsync(selector);
    public IDataPipeline<TResult> SelectMany<TResult>(Func<T, IEnumerable<TResult>> selector) => _inner.SelectMany(selector);
    public IDataPipeline<T> Where(Func<T, bool> predicate) => _inner.Where(predicate);
    public IDataPipeline<T> WhereAsync(Func<T, Task<bool>> predicate) => _inner.WhereAsync(predicate);
    public IDataPipeline<T> OrderBy<TKey>(Func<T, TKey> keySelector) => _inner.OrderBy(keySelector);
    public IDataPipeline<T> OrderByDescending<TKey>(Func<T, TKey> keySelector) => _inner.OrderByDescending(keySelector);
    public IDataPipeline<T> ThenBy<TKey>(Func<T, TKey> keySelector) => _inner.ThenBy(keySelector);
    public IDataPipeline<T> ThenByDescending<TKey>(Func<T, TKey> keySelector) => _inner.ThenByDescending(keySelector);
    public IDataPipeline<T> Distinct() => _inner.Distinct();
    public IDataPipeline<T> DistinctBy<TKey>(Func<T, TKey> keySelector) => _inner.DistinctBy(keySelector);
    public IDataPipeline<T> Skip(int count) => _inner.Skip(count);
    public IDataPipeline<T> Take(int count) => _inner.Take(count);
    public IDataPipeline<List<T>> Batch(int batchSize) => _inner.Batch(batchSize);
    public IGroupedDataPipeline<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull => _inner.GroupBy(keySelector);
    public IDataPipeline<(T First, TSecond Second)> Zip<TSecond>(IDataPipeline<TSecond> second) => _inner.Zip(second);
    public IDataPipeline<TResult> TransformWith<TResult>(IDataTransform<T, TResult> transform) => _inner.TransformWith(transform);
    public IDataPipeline<T> ValidateWith(IValidator<T> validator) => _inner.ValidateWith(validator);
    public IDataPipeline<T> ContinueOnValidationError() => _inner.ContinueOnValidationError();
    public IDataPipeline<T> FailOnValidationError() => _inner.FailOnValidationError();
    public IDataPipeline<T> OnErrorContinue() => _inner.OnErrorContinue();
    public IDataPipeline<T> OnErrorStop() => _inner.OnErrorStop();
    public IDataPipeline<T> OnErrorSkip() => _inner.OnErrorSkip();
    public IDataPipeline<T> OnError(Func<Exception, T, ErrorAction> handler) => _inner.OnError(handler);
    public Task<TResult> AggregateAsync<TResult>(Func<TResult, T, TResult> aggregator, TResult seed, CancellationToken cancellationToken = default) => _inner.AggregateAsync(aggregator, seed, cancellationToken);
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) { var r = new List<T>(); await foreach (var i in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) r.Add(i); return r; }
    public async Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default) => (await ToListAsync(cancellationToken).ConfigureAwait(false)).ToArray();
    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) { await foreach (var i in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) return i; return default; }
    public async Task<int> CountAsync(CancellationToken cancellationToken = default) { var c = 0; await foreach (var _ in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) c++; return c; }
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) { await foreach (var _ in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) return true; return false; }
    public Task<ExportResults> ToCsvAsync(string filePath, CsvExportOptions? options = null, CancellationToken cancellationToken = default) => _inner.ToCsvAsync(filePath, options, cancellationToken);
    public Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default) => _inner.ToJsonAsync(filePath, options, cancellationToken);
    public Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default) => _inner.ToExcelAsync(filePath, options, cancellationToken);
    public Task<ExportResults> ToConsoleAsync(Func<T, string>? formatter = null, CancellationToken cancellationToken = default) => _inner.ToConsoleAsync(formatter, cancellationToken);
    public Task<ExportResults> ToStreamAsync(System.IO.Stream stream, ExportFormat format, CancellationToken cancellationToken = default) => _inner.ToStreamAsync(stream, format, cancellationToken);
}

internal class ParallelPipeline<T> : IDataPipeline<T>
{
    private readonly IDataPipeline<T> _inner;
    private readonly int _maxDegreeOfParallelism;
    private readonly bool _preserveOrder;

    public ParallelPipeline(IDataPipeline<T> inner, int maxDegreeOfParallelism, bool preserveOrder)
    {
        _inner = inner;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _preserveOrder = preserveOrder;
    }

    public async IAsyncEnumerable<T> AsAsyncEnumerable([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_preserveOrder)
        {
            var semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
            var orderedItems = new List<(int Index, T Item)>();
            var index = 0;

            await foreach (var item in _inner.AsAsyncEnumerable(cancellationToken).ConfigureAwait(false))
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                var currentIndex = index++;

                var task = Task.Run(async () =>
                {
                    try
                    {
                        return (currentIndex, item);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);

                var result = await task.ConfigureAwait(false);
                orderedItems.Add(result);

                orderedItems.Sort((a, b) => a.Index.CompareTo(b.Index));

                while (orderedItems.Count > 0 && orderedItems[0].Index == orderedItems[0].Index)
                {
                    yield return orderedItems[0].Item;
                    orderedItems.RemoveAt(0);
                }
            }
        }
        else
        {
            var semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
            var tasks = new List<Task<T>>();

            await foreach (var item in _inner.AsAsyncEnumerable(cancellationToken).ConfigureAwait(false))
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        return item;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var result in results)
            {
                yield return result;
            }
        }
    }

    // 实现其他接口方法...
    public IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector) => _inner.Select(selector);
    public IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector) => _inner.SelectAsync(selector);
    public IDataPipeline<TResult> SelectMany<TResult>(Func<T, IEnumerable<TResult>> selector) => _inner.SelectMany(selector);
    public IDataPipeline<T> Where(Func<T, bool> predicate) => _inner.Where(predicate);
    public IDataPipeline<T> WhereAsync(Func<T, Task<bool>> predicate) => _inner.WhereAsync(predicate);
    public IDataPipeline<T> OrderBy<TKey>(Func<T, TKey> keySelector) => _inner.OrderBy(keySelector);
    public IDataPipeline<T> OrderByDescending<TKey>(Func<T, TKey> keySelector) => _inner.OrderByDescending(keySelector);
    public IDataPipeline<T> ThenBy<TKey>(Func<T, TKey> keySelector) => _inner.ThenBy(keySelector);
    public IDataPipeline<T> ThenByDescending<TKey>(Func<T, TKey> keySelector) => _inner.ThenByDescending(keySelector);
    public IDataPipeline<T> Distinct() => _inner.Distinct();
    public IDataPipeline<T> DistinctBy<TKey>(Func<T, TKey> keySelector) => _inner.DistinctBy(keySelector);
    public IDataPipeline<T> Skip(int count) => _inner.Skip(count);
    public IDataPipeline<T> Take(int count) => _inner.Take(count);
    public IDataPipeline<List<T>> Batch(int batchSize) => _inner.Batch(batchSize);
    public IGroupedDataPipeline<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull => _inner.GroupBy(keySelector);
    public IDataPipeline<(T First, TSecond Second)> Zip<TSecond>(IDataPipeline<TSecond> second) => _inner.Zip(second);
    public IDataPipeline<TResult> TransformWith<TResult>(IDataTransform<T, TResult> transform) => _inner.TransformWith(transform);
    public IDataPipeline<T> ValidateWith(IValidator<T> validator) => _inner.ValidateWith(validator);
    public IDataPipeline<T> ContinueOnValidationError() => _inner.ContinueOnValidationError();
    public IDataPipeline<T> FailOnValidationError() => _inner.FailOnValidationError();
    public IDataPipeline<T> OnErrorContinue() => _inner.OnErrorContinue();
    public IDataPipeline<T> OnErrorStop() => _inner.OnErrorStop();
    public IDataPipeline<T> OnErrorSkip() => _inner.OnErrorSkip();
    public IDataPipeline<T> OnError(Func<Exception, T, ErrorAction> handler) => _inner.OnError(handler);
    public Task<TResult> AggregateAsync<TResult>(Func<TResult, T, TResult> aggregator, TResult seed, CancellationToken cancellationToken = default) => _inner.AggregateAsync(aggregator, seed, cancellationToken);
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) { var r = new List<T>(); await foreach (var i in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) r.Add(i); return r; }
    public async Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default) => (await ToListAsync(cancellationToken).ConfigureAwait(false)).ToArray();
    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) { await foreach (var i in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) return i; return default; }
    public async Task<int> CountAsync(CancellationToken cancellationToken = default) { var c = 0; await foreach (var _ in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) c++; return c; }
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) { await foreach (var _ in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false)) return true; return false; }
    public Task<ExportResults> ToCsvAsync(string filePath, CsvExportOptions? options = null, CancellationToken cancellationToken = default) => _inner.ToCsvAsync(filePath, options, cancellationToken);
    public Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default) => _inner.ToJsonAsync(filePath, options, cancellationToken);
    public Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default) => _inner.ToExcelAsync(filePath, options, cancellationToken);
    public Task<ExportResults> ToConsoleAsync(Func<T, string>? formatter = null, CancellationToken cancellationToken = default) => _inner.ToConsoleAsync(formatter, cancellationToken);
    public Task<ExportResults> ToStreamAsync(System.IO.Stream stream, ExportFormat format, CancellationToken cancellationToken = default) => _inner.ToStreamAsync(stream, format, cancellationToken);
}
