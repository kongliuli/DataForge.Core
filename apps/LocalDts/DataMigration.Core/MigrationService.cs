using DataMigration.Contracts;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DataMigration.Core;

public class MigrationService : IMigrationService
{
    private readonly ILogger<MigrationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public MigrationService(ILogger<MigrationService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<MigrationResult> ExecuteMigrationAsync(
        IDataSource dataSource,
        IDataTarget dataTarget,
        IEnumerable<ITransformer> transformers,
        SourceConfig sourceConfig,
        TargetConfig targetConfig,
        IEnumerable<TransformConfig> transformerConfigs,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new MigrationResult();
        int processedRecords = 0;

        try
        {
            _logger.LogInformation("Starting migration task");
            progress?.Report(new MigrationProgress 
            { 
                Percentage = 0, 
                Message = "开始数据迁移...",
                ProcessedRecords = 0
            });

            // Initialize data source
            await dataSource.InitializeAsync(_serviceProvider, cancellationToken);
            progress?.Report(new MigrationProgress 
            { 
                Percentage = 10, 
                Message = "数据源初始化完成",
                ProcessedRecords = 0
            });

            // Initialize transformers
            var transformerList = transformers.ToList();
            var configList = transformerConfigs.ToList();
            
            for (int i = 0; i < transformerList.Count; i++)
            {
                await transformerList[i].InitializeAsync(_serviceProvider, cancellationToken);
            }
            
            progress?.Report(new MigrationProgress 
            { 
                Percentage = 20, 
                Message = $"转换器初始化完成 ({transformerList.Count}个)",
                ProcessedRecords = 0
            });

            // Initialize data target
            await dataTarget.InitializeAsync(_serviceProvider, cancellationToken);
            progress?.Report(new MigrationProgress 
            { 
                Percentage = 30, 
                Message = "目标源初始化完成",
                ProcessedRecords = 0
            });

            // Extract data
            var dataFlow = dataSource.ExtractAsync(sourceConfig, cancellationToken);
            progress?.Report(new MigrationProgress 
            { 
                Percentage = 40, 
                Message = "开始提取数据...",
                ProcessedRecords = 0
            });

            // Apply transformers
            for (int i = 0; i < transformerList.Count; i++)
            {
                dataFlow = transformerList[i].TransformAsync(dataFlow, configList[i], cancellationToken);
                progress?.Report(new MigrationProgress 
                { 
                    Percentage = 40 + (i + 1) * 10 / Math.Max(transformerList.Count, 1), 
                    Message = $"应用转换器 {i + 1}/{transformerList.Count}...",
                    ProcessedRecords = processedRecords
                });
            }

            // Load data to target
            progress?.Report(new MigrationProgress 
            { 
                Percentage = 70, 
                Message = "开始加载数据到目标...",
                ProcessedRecords = 0
            });

            // Create a new data flow for progress tracking and loading
            var progressTracker = new ProgressTracker { Count = 0 };
            var trackedDataFlow = TrackProgressAsync(dataFlow, progress, progressTracker, cancellationToken);

            await dataTarget.LoadAsync(trackedDataFlow, targetConfig, cancellationToken);
            processedRecords = progressTracker.Count;

            stopwatch.Stop();
            
            result.Success = true;
            result.ProcessedRecords = processedRecords;
            result.Duration = stopwatch.Elapsed;

            progress?.Report(new MigrationProgress 
            { 
                Percentage = 100, 
                Message = $"迁移完成! 共处理 {processedRecords} 条记录，耗时 {stopwatch.Elapsed.TotalSeconds:F2} 秒",
                ProcessedRecords = processedRecords
            });

            _logger.LogInformation("Migration completed successfully. Processed {Count} records in {Duration}", 
                processedRecords, stopwatch.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            progress?.Report(new MigrationProgress 
            { 
                Percentage = 0, 
                Message = $"迁移失败: {ex.Message}",
                ProcessedRecords = processedRecords
            });

            _logger.LogError(ex, "Migration failed after {Duration}. Processed {Count} records", 
                stopwatch.Elapsed, processedRecords);

            return result;
        }
    }

    private async IAsyncEnumerable<DataRecord> TrackProgressAsync(
        IAsyncEnumerable<DataRecord> dataFlow,
        IProgress<MigrationProgress>? progress,
        ProgressTracker progressTracker,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var record in dataFlow.WithCancellation(cancellationToken))
        {
            progressTracker.Count++;
            
            // Report progress every 100 records
            if (progressTracker.Count % 100 == 0)
            {
                progress?.Report(new MigrationProgress 
                {
                    Percentage = 70 + Math.Min(progressTracker.Count / 10, 25), 
                    Message = $"已处理 {progressTracker.Count} 条记录...",
                    ProcessedRecords = progressTracker.Count
                });
            }
            
            yield return record;
        }
    }

    private class ProgressTracker
    {
        public int Count { get; set; }
    }
}
