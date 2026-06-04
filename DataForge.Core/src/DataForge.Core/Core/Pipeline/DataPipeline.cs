using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using DataForge.Core.Core.Transforms;
using DataForge.Core.Core.Validation;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Pipeline;

internal class DataPipeline<T> : IDataPipeline<T>
{
    private readonly Func<CancellationToken, IAsyncEnumerable<T>> _sourceFactory;
    private readonly IValidator<T>? _validator;
    private readonly bool _continueOnValidationError;
    private readonly bool _failOnValidationError;
    private readonly Func<Exception, T, ErrorAction>? _errorHandler;
    private readonly bool _onErrorContinue;

    public DataPipeline(IAsyncEnumerable<T> source)
    {
        _sourceFactory = _ => source;
    }

    internal DataPipeline(
        Func<CancellationToken, IAsyncEnumerable<T>> sourceFactory,
        IValidator<T>? validator = null,
        bool continueOnValidationError = false,
        bool failOnValidationError = true,
        Func<Exception, T, ErrorAction>? errorHandler = null,
        bool onErrorContinue = false)
    {
        _sourceFactory = sourceFactory;
        _validator = validator;
        _continueOnValidationError = continueOnValidationError;
        _failOnValidationError = failOnValidationError;
        _errorHandler = errorHandler;
        _onErrorContinue = onErrorContinue;
    }

    public IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        if (_errorHandler != null || _onErrorContinue)
        {
            return new DataPipeline<TResult>((ct) =>
                SelectSafeInternal(_sourceFactory(ct), selector, _errorHandler));
        }
        return new DataPipeline<TResult>((ct) =>
            SelectInternal(_sourceFactory(ct), selector));
    }

    public IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector)
    {
        return new DataPipeline<TResult>((ct) =>
            SelectAsyncInternal(_sourceFactory(ct), selector, ct));
    }

    public IDataPipeline<TResult> SelectMany<TResult>(Func<T, IEnumerable<TResult>> selector)
    {
        return new DataPipeline<TResult>((ct) =>
            SelectManyInternal(_sourceFactory(ct), selector));
    }

    public IDataPipeline<T> Where(Func<T, bool> predicate)
    {
        if (_errorHandler != null || _onErrorContinue)
        {
            return new DataPipeline<T>((ct) =>
                WhereSafeInternal(_sourceFactory(ct), predicate, _errorHandler),
                _validator, _continueOnValidationError, _failOnValidationError, _errorHandler, true);
        }
        return new DataPipeline<T>((ct) =>
            WhereInternal(_sourceFactory(ct), predicate),
            _validator, _continueOnValidationError, _failOnValidationError, _errorHandler, _onErrorContinue);
    }

    public IDataPipeline<T> WhereAsync(Func<T, Task<bool>> predicate)
    {
        return new DataPipeline<T>((ct) =>
            WhereAsyncInternal(_sourceFactory(ct), predicate, ct));
    }

    public IDataPipeline<T> OrderBy<TKey>(Func<T, TKey> keySelector)
    {
        return new DataPipeline<T>((ct) =>
            OrderByInternal(_sourceFactory(ct), keySelector, ascending: true, ct));
    }

    public IDataPipeline<T> OrderByDescending<TKey>(Func<T, TKey> keySelector)
    {
        return new DataPipeline<T>((ct) =>
            OrderByInternal(_sourceFactory(ct), keySelector, ascending: false, ct));
    }

    public IDataPipeline<T> ThenBy<TKey>(Func<T, TKey> keySelector)
    {
        return OrderBy(keySelector);
    }

    public IDataPipeline<T> ThenByDescending<TKey>(Func<T, TKey> keySelector)
    {
        return OrderByDescending(keySelector);
    }

    public IDataPipeline<T> Distinct()
    {
        return new DataPipeline<T>((ct) =>
            DistinctInternal(_sourceFactory(ct)));
    }

    public IDataPipeline<T> DistinctBy<TKey>(Func<T, TKey> keySelector)
    {
        return new DataPipeline<T>((ct) =>
            DistinctByInternal(_sourceFactory(ct), keySelector));
    }

    public IDataPipeline<T> Skip(int count)
    {
        return new DataPipeline<T>((ct) =>
            SkipInternal(_sourceFactory(ct), count));
    }

    public IDataPipeline<T> Take(int count)
    {
        return new DataPipeline<T>((ct) =>
            TakeInternal(_sourceFactory(ct), count));
    }

    public IDataPipeline<List<T>> Batch(int batchSize)
    {
        return new DataPipeline<List<T>>((ct) =>
            BatchInternal(_sourceFactory(ct), batchSize));
    }

    public IGroupedDataPipeline<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull
    {
        return new GroupedDataPipeline<TKey, T>(_sourceFactory, keySelector);
    }

    public IDataPipeline<(T First, TSecond Second)> Zip<TSecond>(IDataPipeline<TSecond> second)
    {
        var secondEnum = second.AsAsyncEnumerable();
        return new DataPipeline<(T First, TSecond Second)>((ct) =>
            ZipInternal(_sourceFactory(ct), secondEnum, ct));
    }

    public IDataPipeline<TResult> TransformWith<TResult>(IDataTransform<T, TResult> transform)
    {
        return new DataPipeline<TResult>((ct) =>
            TransformWithInternal(_sourceFactory(ct), transform));
    }

    public IDataPipeline<T> ValidateWith(IValidator<T> validator)
    {
        return new DataPipeline<T>(_sourceFactory, validator, _continueOnValidationError, _failOnValidationError, _errorHandler, _onErrorContinue);
    }

    public IDataPipeline<T> ContinueOnValidationError()
    {
        return new DataPipeline<T>(_sourceFactory, _validator, continueOnValidationError: true, failOnValidationError: false, _errorHandler, _onErrorContinue);
    }

    public IDataPipeline<T> FailOnValidationError()
    {
        return new DataPipeline<T>(_sourceFactory, _validator, continueOnValidationError: false, failOnValidationError: true, _errorHandler, _onErrorContinue);
    }

    public IDataPipeline<T> OnErrorContinue()
    {
        return new DataPipeline<T>(_sourceFactory, _validator, _continueOnValidationError, _failOnValidationError,
            errorHandler: (ex, item) => ErrorAction.Continue, onErrorContinue: true);
    }

    public IDataPipeline<T> OnErrorStop()
    {
        return new DataPipeline<T>(_sourceFactory, _validator, _continueOnValidationError, _failOnValidationError,
            errorHandler: null, onErrorContinue: false);
    }

    public IDataPipeline<T> OnErrorSkip()
    {
        return new DataPipeline<T>(_sourceFactory, _validator, _continueOnValidationError, _failOnValidationError,
            errorHandler: (ex, item) => ErrorAction.Skip, onErrorContinue: true);
    }

    public IDataPipeline<T> OnError(Func<Exception, T, ErrorAction> handler)
    {
        return new DataPipeline<T>(_sourceFactory, _validator, _continueOnValidationError, _failOnValidationError,
            errorHandler: handler, onErrorContinue: true);
    }

    public async Task<TResult> AggregateAsync<TResult>(Func<TResult, T, TResult> aggregator, TResult seed, CancellationToken cancellationToken = default)
    {
        var result = seed;
        await foreach (var item in GetValidatedEnumerable(cancellationToken).ConfigureAwait(false))
        {
            result = aggregator(result, item);
        }
        return result;
    }

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        await foreach (var item in GetValidatedEnumerable(cancellationToken).ConfigureAwait(false))
        {
            results.Add(item);
        }
        return results;
    }

    public async Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default)
    {
        return (await ToListAsync(cancellationToken).ConfigureAwait(false)).ToArray();
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var item in GetValidatedEnumerable(cancellationToken).ConfigureAwait(false))
        {
            return item;
        }
        return default;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        await foreach (var _ in GetValidatedEnumerable(cancellationToken).ConfigureAwait(false))
        {
            count++;
        }
        return count;
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var _ in GetValidatedEnumerable(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }
        return false;
    }

    public IAsyncEnumerable<T> AsAsyncEnumerable(CancellationToken cancellationToken = default)
    {
        return GetValidatedEnumerable(cancellationToken);
    }

    public async Task<ExportResults> ToCsvAsync(string filePath, CsvExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new CsvTarget<T>(options ?? new CsvExportOptions());
        var sw = Stopwatch.StartNew();
        var result = await target.ExportAsync(GetValidatedEnumerable(cancellationToken), filePath, cancellationToken).ConfigureAwait(false);
        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    public async Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new JsonTarget<T>(options ?? new JsonExportOptions());
        var sw = Stopwatch.StartNew();
        var result = await target.ExportAsync(GetValidatedEnumerable(cancellationToken), filePath, cancellationToken).ConfigureAwait(false);
        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    public async Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new ExcelTarget<T>(options ?? new ExcelExportOptions());
        var sw = Stopwatch.StartNew();
        var result = await target.ExportAsync(GetValidatedEnumerable(cancellationToken), filePath, cancellationToken).ConfigureAwait(false);
        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    public async Task<ExportResults> ToConsoleAsync(Func<T, string>? formatter = null, CancellationToken cancellationToken = default)
    {
        var target = new ConsoleTarget<T>(formatter);
        var sw = Stopwatch.StartNew();
        var result = await target.ExportAsync(GetValidatedEnumerable(cancellationToken), "", cancellationToken).ConfigureAwait(false);
        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    public async Task<ExportResults> ToStreamAsync(Stream stream, ExportFormat format, CancellationToken cancellationToken = default)
    {
        var target = new StreamTarget<T>(format);
        var sw = Stopwatch.StartNew();
        var result = await target.ExportToStreamAsync(GetValidatedEnumerable(cancellationToken), stream, cancellationToken).ConfigureAwait(false);
        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    private async IAsyncEnumerable<T> GetValidatedEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var source = _sourceFactory(cancellationToken);
        IAsyncEnumerator<T> enumerator;
        try
        {
            enumerator = source.GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex) when (_errorHandler != null || _onErrorContinue)
        {
            var action = _errorHandler?.Invoke(ex, default!) ?? ErrorAction.Skip;
            if (action == ErrorAction.Stop || action == ErrorAction.Throw) throw;
            yield break;
        }

        await using (enumerator)
        {
            while (true)
            {
                T item;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }
                    item = enumerator.Current;
                }
                catch (Exception ex) when (_errorHandler != null || _onErrorContinue)
                {
                    var action = _errorHandler?.Invoke(ex, default!) ?? ErrorAction.Skip;
                    if (action == ErrorAction.Skip || action == ErrorAction.Continue)
                    {
                        continue;
                    }
                    throw;
                }

                var isValid = true;
                if (_validator != null)
                {
                    try
                    {
                        var validationResult = await _validator.ValidateAsync(item, cancellationToken).ConfigureAwait(false);
                        if (!validationResult.IsValid)
                        {
                            if (_continueOnValidationError)
                            {
                                isValid = false;
                            }
                            else if (_failOnValidationError)
                            {
                                throw new ValidationException(validationResult.Errors);
                            }
                        }
                    }
                    catch (ValidationException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (_errorHandler != null || _onErrorContinue)
                    {
                        var action = _errorHandler?.Invoke(ex, item) ?? ErrorAction.Skip;
                        if (action == ErrorAction.Skip || action == ErrorAction.Continue)
                        {
                            isValid = false;
                        }
                        else if (action == ErrorAction.Stop || action == ErrorAction.Throw)
                        {
                            throw;
                        }
                    }
                }

                if (isValid)
                {
                    yield return item;
                }
            }
        }
    }

    private static async IAsyncEnumerable<TResult> SelectInternal<TResult>(
        IAsyncEnumerable<T> source,
        Func<T, TResult> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return selector(item);
        }
    }

    private static async IAsyncEnumerable<TResult> SelectSafeInternal<TResult>(
        IAsyncEnumerable<T> source,
        Func<T, TResult> selector,
        Func<Exception, T, ErrorAction>? errorHandler,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            TResult? result = default;
            Exception? caughtException = null;
            try
            {
                result = selector(item);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            if (caughtException != null)
            {
                var action = errorHandler?.Invoke(caughtException, item) ?? ErrorAction.Skip;
                if (action == ErrorAction.Stop || action == ErrorAction.Throw)
                {
                    throw caughtException;
                }
                continue;
            }

            yield return result!;
        }
    }

    private static async IAsyncEnumerable<TResult> SelectAsyncInternal<TResult>(
        IAsyncEnumerable<T> source,
        Func<T, Task<TResult>> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return await selector(item).ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<TResult> SelectManyInternal<TResult>(
        IAsyncEnumerable<T> source,
        Func<T, IEnumerable<TResult>> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            foreach (var result in selector(item))
            {
                yield return result;
            }
        }
    }

    private static async IAsyncEnumerable<T> WhereInternal(
        IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<T> WhereSafeInternal(
        IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        Func<Exception, T, ErrorAction>? errorHandler,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            var shouldYield = false;
            Exception? caughtException = null;
            try
            {
                if (predicate(item))
                {
                    shouldYield = true;
                }
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            if (caughtException != null)
            {
                var action = errorHandler?.Invoke(caughtException, item) ?? ErrorAction.Skip;
                if (action == ErrorAction.Stop || action == ErrorAction.Throw)
                {
                    throw caughtException;
                }
                continue;
            }

            if (shouldYield)
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<T> WhereAsyncInternal(
        IAsyncEnumerable<T> source,
        Func<T, Task<bool>> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (await predicate(item).ConfigureAwait(false))
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<T> OrderByInternal<TKey>(
        IAsyncEnumerable<T> source,
        Func<T, TKey> keySelector,
        bool ascending,
        CancellationToken ct)
    {
        var items = new List<T>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            items.Add(item);
        }

        var sorted = ascending
            ? items.OrderBy(keySelector)
            : items.OrderByDescending(keySelector);

        foreach (var item in sorted)
        {
            yield return item;
        }
    }

    private static async IAsyncEnumerable<T> DistinctInternal(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var seen = new HashSet<T>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (seen.Add(item))
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<T> DistinctByInternal<TKey>(
        IAsyncEnumerable<T> source,
        Func<T, TKey> keySelector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var seen = new HashSet<TKey>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (seen.Add(keySelector(item)))
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<T> SkipInternal(
        IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var skipped = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (skipped < count)
            {
                skipped++;
                continue;
            }
            yield return item;
        }
    }

    private static async IAsyncEnumerable<T> TakeInternal(
        IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var taken = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (taken >= count) yield break;
            yield return item;
            taken++;
        }
    }

    private static async IAsyncEnumerable<List<T>> BatchInternal(
        IAsyncEnumerable<T> source,
        int batchSize,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var batch = new List<T>(batchSize);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            batch.Add(item);
            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private static async IAsyncEnumerable<(TFirst First, TSecond Second)> ZipInternal<TFirst, TSecond>(
        IAsyncEnumerable<TFirst> first,
        IAsyncEnumerable<TSecond> second,
        CancellationToken ct)
    {
        await using var e1 = first.GetAsyncEnumerator(ct);
        await using var e2 = second.GetAsyncEnumerator(ct);
        while (await e1.MoveNextAsync().ConfigureAwait(false) && await e2.MoveNextAsync().ConfigureAwait(false))
        {
            yield return (e1.Current, e2.Current);
        }
    }

    private static async IAsyncEnumerable<TResult> TransformWithInternal<TResult>(
        IAsyncEnumerable<T> source,
        IDataTransform<T, TResult> transform,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return transform.Transform(item);
        }
    }
}

    public IDataPipeline<T> WithProgress(Action<ProgressReport<T>> progressHandler, int reportInterval = 1000)
    {
        return new ProgressReportingPipeline<T>(this, progressHandler, reportInterval);
    }

    public IDataPipeline<T> WithCounter(PerformanceCounter counter)
    {
        return new CounterReportingPipeline<T>(this, counter);
    }
