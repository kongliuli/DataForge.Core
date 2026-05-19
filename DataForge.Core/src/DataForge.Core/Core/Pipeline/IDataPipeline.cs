using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using DataForge.Core.Core.Transforms;
using DataForge.Core.Core.Validation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Pipeline;

public interface IDataPipeline<T>
{
    IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector);

    IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector);

    IDataPipeline<TResult> SelectMany<TResult>(Func<T, IEnumerable<TResult>> selector);

    IDataPipeline<T> Where(Func<T, bool> predicate);

    IDataPipeline<T> WhereAsync(Func<T, Task<bool>> predicate);

    IDataPipeline<T> OrderBy<TKey>(Func<T, TKey> keySelector);

    IDataPipeline<T> OrderByDescending<TKey>(Func<T, TKey> keySelector);

    IDataPipeline<T> ThenBy<TKey>(Func<T, TKey> keySelector);

    IDataPipeline<T> ThenByDescending<TKey>(Func<T, TKey> keySelector);

    IDataPipeline<T> Distinct();

    IDataPipeline<T> DistinctBy<TKey>(Func<T, TKey> keySelector);

    IDataPipeline<T> Skip(int count);

    IDataPipeline<T> Take(int count);

    IDataPipeline<List<T>> Batch(int batchSize);

    IGroupedDataPipeline<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull;

    IDataPipeline<(T First, TSecond Second)> Zip<TSecond>(IDataPipeline<TSecond> second);

    IDataPipeline<TResult> TransformWith<TResult>(IDataTransform<T, TResult> transform);

    IDataPipeline<T> ValidateWith(IValidator<T> validator);

    IDataPipeline<T> ContinueOnValidationError();

    IDataPipeline<T> FailOnValidationError();

    IDataPipeline<T> OnErrorContinue();

    IDataPipeline<T> OnErrorStop();

    IDataPipeline<T> OnErrorSkip();

    IDataPipeline<T> OnError(Func<Exception, T, ErrorAction> handler);

    Task<TResult> AggregateAsync<TResult>(Func<TResult, T, TResult> aggregator, TResult seed, CancellationToken cancellationToken = default);

    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);

    Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<T> AsAsyncEnumerable(CancellationToken cancellationToken = default);

    Task<ExportResults> ToCsvAsync(string filePath, CsvExportOptions? options = null, CancellationToken cancellationToken = default);

    Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default);

    Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default);

    Task<ExportResults> ToConsoleAsync(Func<T, string>? formatter = null, CancellationToken cancellationToken = default);

    Task<ExportResults> ToStreamAsync(Stream stream, ExportFormat format, CancellationToken cancellationToken = default);
}

public interface IGroupedDataPipeline<TKey, TElement> where TKey : notnull
{
    IDataPipeline<TResult> Select<TResult>(Func<IGrouping<TKey, TElement>, TResult> selector);

    IDataPipeline<IGrouping<TKey, TElement>> OrderBy<TKey2>(Func<IGrouping<TKey, TElement>, TKey2> keySelector);

    IDataPipeline<IGrouping<TKey, TElement>> OrderByDescending<TKey2>(Func<IGrouping<TKey, TElement>, TKey2> keySelector);

    Task<List<IGrouping<TKey, TElement>>> ToListAsync(CancellationToken cancellationToken = default);

    Task<Dictionary<TKey, List<TElement>>> ToDictionaryAsync(CancellationToken cancellationToken = default);
}