using DataForge.Core.Core.Targets;
using DataForge.Core.Core.Validation;
using System.Collections.Generic;
using System.Diagnostics;
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

    public DataPipeline(IAsyncEnumerable<T> source)
    {
        _sourceFactory = _ => source;
    }

    private DataPipeline(
        Func<CancellationToken, IAsyncEnumerable<T>> sourceFactory,
        IValidator<T>? validator = null,
        bool continueOnValidationError = false,
        bool failOnValidationError = true)
    {
        _sourceFactory = sourceFactory;
        _validator = validator;
        _continueOnValidationError = continueOnValidationError;
        _failOnValidationError = failOnValidationError;
    }

    public IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return new DataPipeline<TResult>((ct) =>
            SelectInternal(_sourceFactory(ct), selector));
    }

    public IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector)
    {
        return new DataPipeline<TResult>((ct) =>
            SelectAsyncInternal(_sourceFactory(ct), selector, ct));
    }

    public IDataPipeline<T> Where(Func<T, bool> predicate)
    {
        return new DataPipeline<T>((ct) =>
            WhereInternal(_sourceFactory(ct), predicate));
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

    public IGroupedDataPipeline<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull
    {
        return new GroupedDataPipeline<TKey, T>(_sourceFactory, keySelector);
    }

    public IDataPipeline<T> ValidateWith(IValidator<T> validator)
    {
        return new DataPipeline<T>(_sourceFactory, validator, _continueOnValidationError, _failOnValidationError);
    }

    public IDataPipeline<T> ContinueOnValidationError()
    {
        return new DataPipeline<T>(_sourceFactory, _validator, continueOnValidationError: true, failOnValidationError: false);
    }

    public IDataPipeline<T> FailOnValidationError()
    {
        return new DataPipeline<T>(_sourceFactory, _validator, continueOnValidationError: false, failOnValidationError: true);
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

    private async IAsyncEnumerable<T> GetValidatedEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var source = _sourceFactory(cancellationToken);
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (_validator != null)
            {
                var validationResult = await _validator.ValidateAsync(item, cancellationToken).ConfigureAwait(false);
                if (!validationResult.IsValid)
                {
                    if (_continueOnValidationError)
                    {
                        continue;
                    }
                    if (_failOnValidationError)
                    {
                        throw new ValidationException(validationResult.Errors);
                    }
                }
            }
            yield return item;
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
}