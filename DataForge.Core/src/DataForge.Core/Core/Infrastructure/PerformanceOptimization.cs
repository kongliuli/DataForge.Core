using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Infrastructure;

/// <summary>
/// 性能计数器
/// </summary>
public class PerformanceCounter
{
    private long _totalItems;
    private long _processedItems;
    private DateTime _startTime;
    private DateTime? _endTime;
    private readonly List<PerformanceSnapshot> _snapshots = new();

    public long TotalItems => _totalItems;
    public long ProcessedItems => _processedItems;
    public DateTime StartTime => _startTime;
    public DateTime? EndTime => _endTime;
    public TimeSpan Elapsed => (_endTime ?? DateTime.UtcNow) - _startTime;
    public double ItemsPerSecond => Elapsed.TotalSeconds > 0 ? ProcessedItems / Elapsed.TotalSeconds : 0;
    public double ProgressPercentage => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;

    public PerformanceCounter(long totalItems = 0)
    {
        _totalItems = totalItems;
        _startTime = DateTime.UtcNow;
    }

    public void Start()
    {
        _startTime = DateTime.UtcNow;
        _endTime = null;
        _processedItems = 0;
    }

    public void SetTotalItems(long total)
    {
        Interlocked.Exchange(ref _totalItems, total);
    }

    public void IncrementProcessed(long count = 1)
    {
        Interlocked.Add(ref _processedItems, count);
    }

    public void RecordSnapshot()
    {
        _snapshots.Add(new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            ProcessedItems = _processedItems,
            ItemsPerSecond = ItemsPerSecond
        });
    }

    public IReadOnlyList<PerformanceSnapshot> GetSnapshots() => _snapshots.AsReadOnly();

    public PerformanceSnapshot GetLatestSnapshot()
    {
        return new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            ProcessedItems = _processedItems,
            ItemsPerSecond = ItemsPerSecond
        };
    }

    public void Stop()
    {
        _endTime = DateTime.UtcNow;
    }

    public void Reset()
    {
        _totalItems = 0;
        _processedItems = 0;
        _startTime = DateTime.UtcNow;
        _endTime = null;
        _snapshots.Clear();
    }
}

public class PerformanceSnapshot
{
    public DateTime Timestamp { get; init; }
    public long ProcessedItems { get; init; }
    public double ItemsPerSecond { get; init; }
}

/// <summary>
/// 批量处理优化器
/// </summary>
public class BatchOptimizer<T>
{
    private readonly int _batchSize;
    private readonly List<T> _buffer = new();
    private readonly Func<List<T>, CancellationToken, Task> _flushAction;
    private readonly CancellationToken _cancellationToken;

    public BatchOptimizer(int batchSize, Func<List<T>, CancellationToken, Task> flushAction, CancellationToken cancellationToken = default)
    {
        _batchSize = batchSize;
        _flushAction = flushAction;
        _cancellationToken = cancellationToken;
    }

    public async Task AddAsync(T item)
    {
        _buffer.Add(item);

        if (_buffer.Count >= _batchSize)
        {
            await FlushAsync().ConfigureAwait(false);
        }
    }

    public async Task FlushAsync()
    {
        if (_buffer.Count == 0) return;

        var itemsToFlush = new List<T>(_buffer);
        _buffer.Clear();

        await _flushAction(itemsToFlush, _cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// 并行处理选项
/// </summary>
public class ParallelProcessingOptions
{
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
    public bool PreserveOrder { get; set; } = true;
    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// 内存优化工具
/// </summary>
public static class MemoryOptimizer
{
    public static long GetManagedMemorySize()
    {
        return GC.GetTotalMemory(false);
    }

    public static void ForceGarbageCollection(bool fullCollection = true)
    {
        if (fullCollection)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        }
        else
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
        }
    }

    public static void ClearBufferPool()
    {
        // 清空 ArrayPool 等缓冲区池
        // 这在处理完大量数据后可以帮助释放内存
    }

    public static long GetTotalAvailableMemory()
    {
        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    }

    public static bool IsMemoryPressureHigh()
    {
        var memoryInfo = GC.GetGCMemoryInfo();
        return memoryInfo.MemoryLoadBytes > memoryInfo.TotalAvailableMemoryBytes * 0.8;
    }
}

/// <summary>
/// 进度报告器
/// </summary>
public class ProgressReporter<T>
{
    private readonly Action<ProgressReport<T>> _reportAction;
    private readonly int _reportInterval;
    private long _lastReportedCount;
    private long _lastReportedTime;
    private readonly PerformanceCounter _counter;

    public ProgressReporter(Action<ProgressReport<T>> reportAction, int reportInterval = 1000)
    {
        _reportAction = reportAction;
        _reportInterval = reportInterval;
        _counter = new PerformanceCounter();
        _lastReportedTime = DateTime.UtcNow.Ticks;
    }

    public void Report(T item, long currentCount)
    {
        _counter.IncrementProcessed();

        if (currentCount - _lastReportedCount >= _reportInterval ||
            DateTime.UtcNow.Ticks - _lastReportedTime >= TimeSpan.TicksPerSecond)
        {
            _lastReportedCount = currentCount;
            _lastReportedTime = DateTime.UtcNow.Ticks;

            _reportAction(new ProgressReport<T>
            {
                CurrentItem = item,
                ProcessedCount = currentCount,
                ItemsPerSecond = _counter.ItemsPerSecond,
                ProgressPercentage = _counter.ProgressPercentage
            });
        }
    }
}

public class ProgressReport<T>
{
    public T? CurrentItem { get; init; }
    public long ProcessedCount { get; init; }
    public double ItemsPerSecond { get; init; }
    public double ProgressPercentage { get; init; }
}

/// <summary>
/// 异步队列缓冲区
/// </summary>
public class AsyncBuffer<T>
{
    private readonly Queue<T> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly int _maxSize;
    private bool _isCompleted;

    public AsyncBuffer(int maxSize = 1000)
    {
        _maxSize = maxSize;
    }

    public async ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);

            lock (_queue)
            {
                if (_queue.Count > 0)
                {
                    return _queue.Dequeue();
                }

                if (_isCompleted)
                {
                    throw new OperationCanceledException();
                }
            }
        }
    }

    public void Enqueue(T item)
    {
        lock (_queue)
        {
            _queue.Enqueue(item);
            _signal.Release();
        }
    }

    public void Complete()
    {
        lock (_queue)
        {
            _isCompleted = true;
            _signal.Release();
        }
    }
}
