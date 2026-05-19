using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Pipeline;

public interface IDataPipeline<out T>
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
    
    IDataPipeline<T> ValidateWith(IValidator<T> validator);
    
    IDataPipeline<T> ContinueOnValidationError();
    
    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
    
    Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default);
    
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    
    IAsyncEnumerable<T> AsAsyncEnumerable();
    
    Task<ExportResults> ToCsvAsync(string filePath, CsvExportOptions? options = null, CancellationToken cancellationToken = default);
    
    Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default);
    
    Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default);
}

public interface IGroupedDataPipeline<TKey, TElement>
{
    IDataPipeline<TResult> Select<TResult>(Func<IGrouping<TKey, TElement>, TResult> selector);
    
    Task<List<IGrouping<TKey, TElement>>> ToListAsync(CancellationToken cancellationToken = default);
}