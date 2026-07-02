using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataMigration.Contracts;
using DataMigration.Core;

namespace DataMigration.Wpf.Services;

// 事件参数类
public class MigrationProgressEventArgs : EventArgs
{
    public int Progress { get; set; }
    public int CurrentItem { get; set; }
    public int TotalItems { get; set; }
}

public class MigrationCompletedEventArgs : EventArgs
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public interface IMigrationService
{
    Task StartMigrationAsync(MigrationTask task, CancellationToken cancellationToken = default);
    Task PauseMigrationAsync();
    Task ResumeMigrationAsync();
    Task StopMigrationAsync();
    bool IsMigrationRunning { get; }
    bool IsMigrationPaused { get; }
    int Progress { get; }
    event EventHandler<MigrationProgressEventArgs> ProgressChanged;
    event EventHandler<MigrationCompletedEventArgs> MigrationCompleted;
}

public class MigrationService : IMigrationService
{
    private bool _isRunning;
    private bool _isPaused;
    private int _progress;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly IPluginManager _pluginManager;

    public bool IsMigrationRunning => _isRunning;
    public bool IsMigrationPaused => _isPaused;
    public int Progress => _progress;

    public event EventHandler<MigrationProgressEventArgs>? ProgressChanged;
    public event EventHandler<MigrationCompletedEventArgs>? MigrationCompleted;

    public MigrationService(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    public async Task StartMigrationAsync(MigrationTask task, CancellationToken cancellationToken = default)
    {
        _isRunning = true;
        _isPaused = false;
        _progress = 0;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // 验证任务配置
            if (string.IsNullOrEmpty(task.Source.ComponentId))
            {
                throw new ArgumentException("Data source component ID is required");
            }
            
            if (string.IsNullOrEmpty(task.Target.ComponentId))
            {
                throw new ArgumentException("Data target component ID is required");
            }

            // 获取数据源、转换器和数据目标实例
            var dataSource = _pluginManager.GetDataSource(task.Source.ComponentId);
            if (dataSource == null)
            {
                throw new InvalidOperationException($"Failed to get data source with ID: {task.Source.ComponentId}");
            }

            var dataTarget = _pluginManager.GetTarget(task.Target.ComponentId);
            if (dataTarget == null)
            {
                throw new InvalidOperationException($"Failed to get data target with ID: {task.Target.ComponentId}");
            }

            ITransformer? transformer = null;
            if (task.Transforms.Count > 0)
            {
                transformer = _pluginManager.GetTransformer(task.Transforms[0].ComponentId);
                if (transformer == null)
                {
                    throw new InvalidOperationException($"Failed to get transformer with ID: {task.Transforms[0].ComponentId}");
                }
            }

            // 处理数据流
            IAsyncEnumerable<DataRecord> dataStream = dataSource.ExtractAsync(task.Source, _cancellationTokenSource.Token);

            // 转换数据
            if (transformer != null)
            {
                dataStream = transformer.TransformAsync(dataStream, task.Transforms[0], _cancellationTokenSource.Token);
            }

            // 写入数据
            await dataTarget.LoadAsync(dataStream, task.Target, _cancellationTokenSource.Token);

            // 完成迁移
            OnMigrationCompleted(new MigrationCompletedEventArgs
            {
                Success = true,
                Message = "迁移任务执行成功"
            });
        }
        catch (OperationCanceledException)
        {
            OnMigrationCompleted(new MigrationCompletedEventArgs
            {
                Success = false,
                Message = "迁移任务已被取消"
            });
        }
        catch (ArgumentException ex)
        {
            // 记录错误日志
            Console.WriteLine($"Migration argument error: {ex.Message}");
            
            OnMigrationCompleted(new MigrationCompletedEventArgs
            {
                Success = false,
                Message = $"参数错误: {ex.Message}"
            });
        }
        catch (InvalidOperationException ex)
        {
            // 记录错误日志
            Console.WriteLine($"Migration operation error: {ex.Message}");
            
            OnMigrationCompleted(new MigrationCompletedEventArgs
            {
                Success = false,
                Message = $"操作错误: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            // 记录错误日志
            Console.WriteLine($"Migration error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            OnMigrationCompleted(new MigrationCompletedEventArgs
            {
                Success = false,
                Message = $"迁移失败: {ex.Message}"
            });
        }
        finally
        {
            _isRunning = false;
            _isPaused = false;
            _cancellationTokenSource?.Dispose();
        }
    }

    public async Task PauseMigrationAsync()
    {
        _isPaused = true;
    }

    public async Task ResumeMigrationAsync()
    {
        _isPaused = false;
    }

    public async Task StopMigrationAsync()
    {
        _cancellationTokenSource?.Cancel();
        _isRunning = false;
        _isPaused = false;
    }

    protected virtual void OnProgressChanged(MigrationProgressEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }

    protected virtual void OnMigrationCompleted(MigrationCompletedEventArgs e)
    {
        MigrationCompleted?.Invoke(this, e);
    }
}