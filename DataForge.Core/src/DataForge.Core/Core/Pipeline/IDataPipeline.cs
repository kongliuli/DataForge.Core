using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using DataForge.Core.Core.Validation;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Pipeline;

public interface IDataPipeline<T>
{
    IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector);

    IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector);

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

    IGroupedDataPipeline<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull;

    IDataPipeline<T> ValidateWith(IValidator<T> validator);

    IDataPipeline<T> ContinueOnValidationError();

    IDataPipeline<T> FailOnValidationError();

    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);

    Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<T> AsAsyncEnumerable(CancellationToken cancellationToken = default);

    Task<ExportResults> ToCsvAsync(string filePath, CsvExportOptions? options = null, CancellationToken cancellationToken = default);

    Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default);

    Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default);
}

public interface IGroupedDataPipeline<TKey, TElement> where TKey : notnull
{
    IDataPipeline<TResult> Select<TResult>(Func<IGrouping<TKey, TElement>, TResult> selector);

    IDataPipeline<IGrouping<TKey, TElement>> OrderBy<TKey2>(Func<IGrouping<TKey, TElement>, TKey2> keySelector);

    IDataPipeline<IGrouping<TKey, TElement>> OrderByDescending<TKey2>(Func<IGrouping<TKey, TElement>, TKey2> keySelector);

    Task<List<IGrouping<TKey, TElement>>> ToListAsync(CancellationToken cancellationToken = default);

    Task<Dictionary<TKey, List<TElement>>> ToDictionaryAsync(CancellationToken cancellationToken = default);
}