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

public class DataPipeline<T> : IDataPipeline<T>
{
    private readonly Func<CancellationToken, IAsyncEnumerable<T>> _sourceFactory;
    private readonly IValidator<T>? _validator;
    private readonly bool _continueOnValidationError;
    private readonly bool _failOnValidationError;
    private readonly Func<Exception, T, ErrorAction>? _errorHandler;
    private readonly bool _onErrorContinue;
    private readonly IReadOnlyList<PipelineInterceptor<T>> _interceptors;
    private readonly IReadOnlyList<SortKeySpec> _sortKeys;
    private readonly ExternalSortOptions? _externalSortOptions;
    private readonly PipelineDiagnostics? _diagnostics;
    private readonly string? _badRowOutputPath;

    public DataPipeline(IAsyncEnumerable<T> source)
    {
        _sourceFactory = _ => source;
        _failOnValidationError = true;
        _interceptors = [];
        _sortKeys = [];
    }

    internal DataPipeline(
        Func<CancellationToken, IAsyncEnumerable<T>> sourceFactory,
        IValidator<T>? validator = null,
        bool continueOnValidationError = false,
        bool failOnValidationError = true,
        Func<Exception, T, ErrorAction>? errorHandler = null,
        bool onErrorContinue = false,
        IReadOnlyList<PipelineInterceptor<T>>? interceptors = null,
        IReadOnlyList<SortKeySpec>? sortKeys = null,
        ExternalSortOptions? externalSortOptions = null,
        PipelineDiagnostics? diagnostics = null,
        string? badRowOutputPath = null)
    {
        _sourceFactory = sourceFactory;
        _validator = validator;
        _continueOnValidationError = continueOnValidationError;
        _failOnValidationError = failOnValidationError;
        _errorHandler = errorHandler;
        _onErrorContinue = onErrorContinue;
        _interceptors = interceptors ?? [];
        _sortKeys = sortKeys ?? [];
        _externalSortOptions = externalSortOptions;
        _diagnostics = diagnostics;
        _badRowOutputPath = badRowOutputPath;
    }

    internal DataPipeline<T> WithBadRowOutput(string filePath)
    {
        var diagnostics = _diagnostics ?? new PipelineDiagnostics();
        return new DataPipeline<T>(
            _sourceFactory,
            _validator,
            _continueOnValidationError,
            _failOnValidationError,
            _errorHandler,
            _onErrorContinue,
            _interceptors,
            _sortKeys,
            _externalSortOptions,
            diagnostics,
            filePath);
    }

    IDataPipeline<T> IDataPipeline<T>.WithBadRowOutput(string filePath) => WithBadRowOutput(filePath);

    internal DataPipeline<T> WithPipelineInterceptor(PipelineInterceptor<T> interceptor)
    {
        var list = new List<PipelineInterceptor<T>>(_interceptors) { interceptor };
        return Clone(_sourceFactory, _sortKeys, _externalSortOptions, list);
    }

    public IDataPipeline<T> WithExternalSort(ExternalSortOptions? options = null)
        => Clone(_sourceFactory, _sortKeys, options ?? new ExternalSortOptions());

    private DataPipeline<T> Clone(
        Func<CancellationToken, IAsyncEnumerable<T>> sourceFactory,
        IReadOnlyList<SortKeySpec>? sortKeys = null,
        ExternalSortOptions? externalSortOptions = null,
        IReadOnlyList<PipelineInterceptor<T>>? interceptors = null)
    {
        return new DataPipeline<T>(
            sourceFactory,
            _validator,
            _continueOnValidationError,
            _failOnValidationError,
            _errorHandler,
            _onErrorContinue,
            interceptors ?? _interceptors,
            sortKeys ?? _sortKeys,
            externalSortOptions ?? _externalSortOptions,
            _diagnostics,
            _badRowOutputPath);
    }

    private DataPipeline<TNew> CreateChild<TNew>(Func<CancellationToken, IAsyncEnumerable<TNew>> sourceFactory)
        => new(sourceFactory, diagnostics: _diagnostics, badRowOutputPath: _badRowOutputPath);

    private Func<CancellationToken, IAsyncEnumerable<T>> BuildEnumeration()
    {
        if (_sortKeys.Count == 0)
        {
            return _sourceFactory;
        }

        return ct => SortEngine.SortAsync(_sourceFactory(ct), _sortKeys, _externalSortOptions, ct);
    }

    public IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        var preSelect = (Func<CancellationToken, IAsyncEnumerable<T>>)(ct => GetValidatedEnumerableWithoutInterceptors(ct));
        if (_errorHandler != null || _onErrorContinue)
        {
            return new DataPipeline<TResult>(
                ct => SelectSafeInternal(preSelect(ct), selector, _errorHandler, ct),
                diagnostics: _diagnostics,
                badRowOutputPath: _badRowOutputPath);
        }

        return CreateChild(ct => SelectInternal(preSelect(ct), selector, ct));
    }

    public IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector)
    {
        var preSelect = (Func<CancellationToken, IAsyncEnumerable<T>>)(ct => GetValidatedEnumerableWithoutInterceptors(ct));
        return CreateChild(ct => SelectAsyncInternal(preSelect(ct), selector, ct));
    }

    public IDataPipeline<TResult> SelectParallelAsync<TResult>(Func<T, Task<TResult>> selector, int maxDegreeOfParallelism = 4)
    {
        var preSelect = (Func<CancellationToken, IAsyncEnumerable<T>>)(ct => GetValidatedEnumerableWithoutInterceptors(ct));
        return CreateChild(ct => SelectParallelAsyncInternal(preSelect(ct), selector, maxDegreeOfParallelism, ct));
    }

    public IDataPipeline<TResult> SelectMany<TResult>(Func<T, IEnumerable<TResult>> selector)
    {
        var preSelect = (Func<CancellationToken, IAsyncEnumerable<T>>)(ct => GetValidatedEnumerableWithoutInterceptors(ct));
        return CreateChild(ct => SelectManyInternal(preSelect(ct), selector, ct));
    }

    public IDataPipeline<T> Where(Func<T, bool> predicate)
    {
        if (_errorHandler != null || _onErrorContinue)
        {
            return Clone(
                ct => WhereSafeInternal(_sourceFactory(ct), predicate, _errorHandler, ct));
        }

        return Clone(ct => WhereInternal(_sourceFactory(ct), predicate, ct));
    }

    public IDataPipeline<T> WhereAsync(Func<T, Task<bool>> predicate)
        => Clone(ct => WhereAsyncInternal(_sourceFactory(ct), predicate, ct));

    public IDataPipeline<T> OrderBy<TKey>(Func<T, TKey> keySelector)
    {
        var keys = new List<SortKeySpec> { SortKeySpec.Create(keySelector, descending: false) };
        return Clone(_sourceFactory, keys, _externalSortOptions);
    }

    public IDataPipeline<T> OrderByDescending<TKey>(Func<T, TKey> keySelector)
    {
        var keys = new List<SortKeySpec> { SortKeySpec.Create(keySelector, descending: true) };
        return Clone(_sourceFactory, keys, _externalSortOptions);
    }

    public IDataPipeline<T> ThenBy<TKey>(Func<T, TKey> keySelector)
    {
        var keys = _sortKeys.ToList();
        keys.Add(SortKeySpec.Create(keySelector, descending: false));
        return Clone(_sourceFactory, keys, _externalSortOptions);
    }

    public IDataPipeline<T> ThenByDescending<TKey>(Func<T, TKey> keySelector)
    {
        var keys = _sortKeys.ToList();
        keys.Add(SortKeySpec.Create(keySelector, descending: true));
        return Clone(_sourceFactory, keys, _externalSortOptions);
    }

    public IDataPipeline<T> Distinct()
        => Clone(ct => DistinctInternal(_sourceFactory(ct), ct));

    public IDataPipeline<T> DistinctBy<TKey>(Func<T, TKey> keySelector)
        => Clone(ct => DistinctByInternal(_sourceFactory(ct), keySelector, ct));

    public IDataPipeline<T> Skip(int count)
        => Clone(ct => SkipInternal(_sourceFactory(ct), count, ct));

    public IDataPipeline<T> Take(int count)
        => Clone(ct => TakeInternal(_sourceFactory(ct), count, ct));

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
        => new DataPipeline<T>(_sourceFactory, validator, _continueOnValidationError, _failOnValidationError, _errorHandler, _onErrorContinue, _interceptors, _sortKeys, _externalSortOptions, _diagnostics, _badRowOutputPath);

    public IDataPipeline<T> ContinueOnValidationError()
        => new DataPipeline<T>(_sourceFactory, _validator, continueOnValidationError: true, failOnValidationError: false, _errorHandler, _onErrorContinue, _interceptors, _sortKeys, _externalSortOptions, _diagnostics, _badRowOutputPath);

    public IDataPipeline<T> FailOnValidationError()
        => new DataPipeline<T>(_sourceFactory, _validator, continueOnValidationError: false, failOnValidationError: true, _errorHandler, _onErrorContinue, _interceptors, _sortKeys, _externalSortOptions, _diagnostics, _badRowOutputPath);

    public IDataPipeline<T> OnErrorContinue()
        => new DataPipeline<T>(_sourceFactory, _validator, _continueOnValidationError, _failOnValidationError,
            errorHandler: (ex, item) => ErrorAction.Continue, onErrorContinue: true, _interceptors, _sortKeys, _externalSortOptions, _diagnostics, _badRowOutputPath);

    public IDataPipeline<T> OnErrorStop()
        => new DataPipeline<T>(_sourceFactory, _validator, _continueOnValidationError, _failOnValidationError,
            errorHandler: null, onErrorContinue: false, _interceptors, _sortKeys, _externalSortOptions, _diagnostics, _badRowOutputPath);

    public IDataPipeline<T> OnErrorSkip()
        => new DataPipeline<T>(_sourceFactory, _validator, _continueOnValidationError, _failOnValidationError,
            errorHandler: (ex, item) => ErrorAction.Skip, onErrorContinue: true, _interceptors, _sortKeys, _externalSortOptions, _diagnostics, _badRowOutputPath);

    public IDataPipeline<T> OnError(Func<Exception, T, ErrorAction> handler)
        => new DataPipeline<T>(_sourceFactory, _validator, _continueOnValidationError, _failOnValidationError,
            errorHandler: handler, onErrorContinue: true, _interceptors, _sortKeys, _externalSortOptions, _diagnostics, _badRowOutputPath);

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
        return await FinalizeExportAsync(result, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new JsonTarget<T>(options ?? new JsonExportOptions());
        var sw = Stopwatch.StartNew();
        var result = await target.ExportAsync(GetValidatedEnumerable(cancellationToken), filePath, cancellationToken).ConfigureAwait(false);
        sw.Stop();
        result.Duration = sw.Elapsed;
        return await FinalizeExportAsync(result, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new ExcelTarget<T>(options ?? new ExcelExportOptions());
        var sw = Stopwatch.StartNew();
        var result = await target.ExportAsync(GetValidatedEnumerable(cancellationToken), filePath, cancellationToken).ConfigureAwait(false);
        sw.Stop();
        result.Duration = sw.Elapsed;
        return await FinalizeExportAsync(result, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExportResults> ToConsoleAsync(Func<T, string>? formatter = null, CancellationToken cancellationToken = default)
    {
        var target = new ConsoleTarget<T>(formatter);
        var sw = Stopwatch.StartNew();
        var result = await target.ExportAsync(GetValidatedEnumerable(cancellationToken), "", cancellationToken).ConfigureAwait(false);
        sw.Stop();
        result.Duration = sw.Elapsed;
        return await FinalizeExportAsync(result, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExportResults> ToStreamAsync(Stream stream, ExportFormat format, CancellationToken cancellationToken = default)
    {
        var target = new StreamTarget<T>(format);
        var sw = Stopwatch.StartNew();
        var result = await target.ExportToStreamAsync(GetValidatedEnumerable(cancellationToken), stream, cancellationToken).ConfigureAwait(false);
        sw.Stop();
        result.Duration = sw.Elapsed;
        return await FinalizeExportAsync(result, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ExportResults> FinalizeExportAsync(ExportResults result, CancellationToken cancellationToken)
    {
        if (_diagnostics == null)
        {
            return result;
        }

        foreach (var rowError in _diagnostics.RowErrors)
        {
            result.RowErrors.Add(rowError);
            result.Errors.Add(rowError.Message);
        }

        if (!string.IsNullOrWhiteSpace(_badRowOutputPath) && _diagnostics.RowErrors.Count > 0)
        {
            await BadRowExporter.WriteAsync(_badRowOutputPath, _diagnostics.RowErrors, cancellationToken).ConfigureAwait(false);
            result.OutputPath = string.IsNullOrEmpty(result.OutputPath) ? _badRowOutputPath : result.OutputPath;
        }

        return result;
    }

    internal Task<ExportResults> FinalizeExportResultsAsync(ExportResults result, CancellationToken cancellationToken) =>
        FinalizeExportAsync(result, cancellationToken);

    private async IAsyncEnumerable<T> GetValidatedEnumerableWithoutInterceptors(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in EnumerateValidatedCore(BuildEnumeration()(cancellationToken), cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private async IAsyncEnumerable<T> GetValidatedEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stream = EnumerateValidatedCore(BuildEnumeration()(cancellationToken), cancellationToken);
        foreach (var interceptor in _interceptors)
        {
            stream = interceptor(stream, cancellationToken);
        }

        await foreach (var item in stream.ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private async IAsyncEnumerable<T> EnumerateValidatedCore(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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
            long rowNumber = 0;
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
                    rowNumber++;
                }
                catch (Exception ex) when (_errorHandler != null || _onErrorContinue)
                {
                    var action = _errorHandler?.Invoke(ex, default!) ?? ErrorAction.Skip;
                    RecordTransformError(rowNumber, default!, ex);
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
                            RecordValidationErrors(rowNumber, item, validationResult);
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
                        RecordTransformError(rowNumber, item, ex);
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

    private void RecordValidationErrors(long rowNumber, T item, ValidationResult validationResult)
    {
        if (_diagnostics == null)
        {
            return;
        }

        var raw = TrySerialize(item);
        foreach (var error in validationResult.Errors)
        {
            _diagnostics.Add(new RowError
            {
                RowNumber = rowNumber,
                PropertyName = error.PropertyName,
                RuleName = error.ErrorCode.ToString(),
                Message = error.Message,
                RawValue = raw,
                Kind = ErrorKind.Validation
            });
        }
    }

    private void RecordTransformError(long rowNumber, T item, Exception ex)
    {
        if (_diagnostics == null)
        {
            return;
        }

        _diagnostics.Add(new RowError
        {
            RowNumber = rowNumber > 0 ? rowNumber : null,
            Message = ex.Message,
            RawValue = rowNumber > 0 ? TrySerialize(item) : null,
            Kind = ErrorKind.Transform
        });
    }

    private static string? TrySerialize(T item)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(item);
        }
        catch
        {
            return item?.ToString();
        }
    }

    private static async IAsyncEnumerable<TResult> SelectParallelAsyncInternal<TResult>(
        IAsyncEnumerable<T> source,
        Func<T, Task<TResult>> selector,
        int maxDegreeOfParallelism,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = new List<Task<(int Index, TResult Result)>>();
        var index = 0;

        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            var currentIndex = index++;
            tasks.Add(RunSelectAsync(currentIndex, item, selector, semaphore, ct));
        }

        foreach (var (_, result) in (await Task.WhenAll(tasks).ConfigureAwait(false)).OrderBy(x => x.Index))
        {
            yield return result;
        }
    }

    private static async Task<(int Index, TResult Result)> RunSelectAsync<TResult>(
        int index,
        T item,
        Func<T, Task<TResult>> selector,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        try
        {
            return (index, await selector(item).ConfigureAwait(false));
        }
        finally
        {
            semaphore.Release();
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
