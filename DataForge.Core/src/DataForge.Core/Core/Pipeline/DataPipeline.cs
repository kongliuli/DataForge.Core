using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Pipeline;

internal class DataPipeline<T> : IDataPipeline<T>
{
    private readonly IAsyncEnumerable<T> _source;
    private List<IDataTransform<T, T>>? _transforms;
    private IValidator<T>? _validator;
    private bool _continueOnValidationError;

    public DataPipeline(IAsyncEnumerable<T> source)
    {
        _source = source;
    }

    public IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return new DataPipeline<TResult>(ApplyTransform(new SelectTransform<T, TResult>(selector)));
    }

    public IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector)
    {
        return new DataPipeline<TResult>(ApplyTransform(new AsyncSelectTransform<T, TResult>(selector)));
    }

    public IDataPipeline<T> Where(Func<T, bool> predicate)
    {
        _transforms ??= [];
        _transforms.Add(new WhereTransform<T>(predicate));
        return this;
    }

    public IDataPipeline<T> WhereAsync(Func<T, Task<bool>> predicate)
    {
        _transforms ??= [];
        _transforms.Add(new AsyncWhereTransform<T>(predicate));
        return this;
    }

    public IDataPipeline<T> OrderBy<TKey>(Func<T, TKey> keySelector)
    {
        _transforms ??= [];
        _transforms.Add(new OrderByTransform<T, TKey>(keySelector, ascending: true));
        return this;
    }

    public IDataPipeline<T> OrderByDescending<TKey>(Func<T, TKey> keySelector)
    {
        _transforms ??= [];
        _transforms.Add(new OrderByTransform<T, TKey>(keySelector, ascending: false));
        return this;
    }

    public IDataPipeline<T> ThenBy<TKey>(Func<T, TKey> keySelector)
    {
        _transforms ??= [];
        _transforms.Add(new ThenByTransform<T, TKey>(keySelector, ascending: true));
        return this;
    }

    public IDataPipeline<T> ThenByDescending<TKey>(Func<T, TKey> keySelector)
    {
        _transforms ??= [];
        _transforms.Add(new ThenByTransform<T, TKey>(keySelector, ascending: false));
        return this;
    }

    public IDataPipeline<T> Distinct()
    {
        _transforms ??= [];
        _transforms.Add(new DistinctTransform<T>());
        return this;
    }

    public IDataPipeline<T> DistinctBy<TKey>(Func<T, TKey> keySelector)
    {
        _transforms ??= [];
        _transforms.Add(new DistinctByTransform<T, TKey>(keySelector));
        return this;
    }

    public IDataPipeline<T> Skip(int count)
    {
        _transforms ??= [];
        _transforms.Add(new SkipTransform<T>(count));
        return this;
    }

    public IDataPipeline<T> Take(int count)
    {
        _transforms ??= [];
        _transforms.Add(new TakeTransform<T>(count));
        return this;
    }

    public IDataPipeline<T> ValidateWith(IValidator<T> validator)
    {
        _validator = validator;
        return this;
    }

    public IDataPipeline<T> ContinueOnValidationError()
    {
        _continueOnValidationError = true;
        return this;
    }

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        await foreach (var item in ExecutePipeline(cancellationToken).ConfigureAwait(false))
        {
            results.Add(item);
        }
        return results;
    }

    public async Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default)
    {
        return (await ToListAsync(cancellationToken).ConfigureAwait(false)).ToArray();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        await foreach (var _ in ExecutePipeline(cancellationToken).ConfigureAwait(false))
        {
            count++;
        }
        return count;
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var _ in ExecutePipeline(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }
        return false;
    }

    public IAsyncEnumerable<T> AsAsyncEnumerable()
    {
        return ExecutePipeline(CancellationToken.None);
    }

    public async Task<ExportResults> ToCsvAsync(string filePath, CsvExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new CsvTarget<T>(options ?? new CsvExportOptions());
        return await target.ExportAsync(ExecutePipeline(cancellationToken), filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new JsonTarget<T>(options ?? new JsonExportOptions());
        return await target.ExportAsync(ExecutePipeline(cancellationToken), filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new ExcelTarget<T>(options ?? new ExcelExportOptions());
        return await target.ExportAsync(ExecutePipeline(cancellationToken), filePath, cancellationToken).ConfigureAwait(false);
    }

    private IAsyncEnumerable<TResult> ApplyTransform<TResult>(IDataTransform<T, TResult> transform)
    {
        return TransformAsyncEnumerable(_source, transform);
    }

    private async IAsyncEnumerable<T> ExecutePipeline([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var current = _source;

        if (_transforms != null)
        {
            foreach (var transform in _transforms)
            {
                current = TransformAsyncEnumerable(current, transform);
            }
        }

        await foreach (var item in current.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (_validator != null)
            {
                var result = await _validator.ValidateAsync(item, cancellationToken).ConfigureAwait(false);
                if (!result.IsValid)
                {
                    if (_continueOnValidationError)
                    {
                        continue;
                    }
                    throw new ValidationException(result.Errors);
                }
            }
            yield return item;
        }
    }

    private static async IAsyncEnumerable<TResult> TransformAsyncEnumerable<TSource, TResult>(
        IAsyncEnumerable<TSource> source,
        IDataTransform<TSource, TResult> transform)
    {
        await foreach (var item in source.ConfigureAwait(false))
        {
            yield return transform.Transform(item);
        }
    }
}