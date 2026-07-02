using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DataMigration.Core;

public class MigrationEngine
{
    private readonly IPluginManager _pluginManager;
    private readonly IServiceProvider _serviceProvider;

    public MigrationEngine(IPluginManager pluginManager, IServiceProvider serviceProvider = null)
    {
        _pluginManager = pluginManager;
        _serviceProvider = serviceProvider;
    }

    public async Task RunAsync(MigrationTask task, CancellationToken ct)
    {
        try
        {
            // 获取数据源插件
            var source = _pluginManager.GetDataSource(task.Source.ComponentId);
            await ErrorHandling.ExecuteWithRetry(() => source.InitializeAsync(_serviceProvider!, ct), ct: ct);

            // 获取转换器插件
            var transformers = task.Transforms.Select(t => _pluginManager.GetTransformer(t.ComponentId)).ToList();
            foreach (var transformer in transformers)
            {
                await ErrorHandling.ExecuteWithRetry(() => transformer.InitializeAsync(_serviceProvider!, ct), ct: ct);
            }

            // 获取目标源插件
            var target = _pluginManager.GetTarget(task.Target.ComponentId);
            await ErrorHandling.ExecuteWithRetry(() => target.InitializeAsync(_serviceProvider!, ct), ct: ct);

            // 构建数据流
            var dataFlow = source.ExtractAsync(task.Source, ct);

            // 应用转换器
            for (int i = 0; i < transformers.Count; i++)
            {
                if (task.Options.MaxDegreeOfParallelism > 1 && transformers[i] is IParallelTransformer parallelTransformer)
                {
                    // 优化并行处理，使用更合理的并行度
                    int parallelism = Math.Min(task.Options.MaxDegreeOfParallelism, Environment.ProcessorCount);
                    // 限制最大并行度，避免过度消耗系统资源
                    parallelism = Math.Min(parallelism, 8); // 最多8个并行任务
                    dataFlow = parallelTransformer.TransformAsync(
                        dataFlow, 
                        task.Transforms[i], 
                        parallelism,
                        ct
                    );
                }
                else if (task.Options.MaxDegreeOfParallelism > 1)
                {
                    // 对于非并行转换器，使用并行处理包装
                    var transformer = transformers[i];
                    var transformConfig = task.Transforms[i];
                    int parallelism = Math.Min(task.Options.MaxDegreeOfParallelism, Environment.ProcessorCount);
                    parallelism = Math.Min(parallelism, 8); // 最多8个并行任务
                    
                    dataFlow = ProcessInParallel(
                        dataFlow, 
                        async (record, token) => await transformer.TransformAsync(
                            new[] { record }.ToAsyncEnumerable(), 
                            transformConfig, 
                            token
                        ).FirstOrDefaultAsync(token),
                        parallelism,
                        ct
                    );
                }
                else
                {
                    dataFlow = transformers[i].TransformAsync(dataFlow, task.Transforms[i], ct);
                }
            }

            // 加载到目标源
            if (task.Options.BatchSize > 1 && target is IBatchDataTarget batchTarget)
            {
                await ErrorHandling.ExecuteWithRetry(
                    () => batchTarget.LoadAsync(
                        dataFlow, 
                        task.Target, 
                        task.Options.BatchSize,
                        ct
                    ), 
                    ct: ct
                );
            }
            else
            {
                await ErrorHandling.ExecuteWithRetry(
                    () => target.LoadAsync(dataFlow, task.Target, ct), 
                    ct: ct
                );
            }

            // 关闭所有插件
            await ErrorHandling.ExecuteWithRetry(() => source.ShutdownAsync(ct), ct: ct);
            foreach (var transformer in transformers)
            {
                await ErrorHandling.ExecuteWithRetry(() => transformer.ShutdownAsync(ct), ct: ct);
            }
            await ErrorHandling.ExecuteWithRetry(() => target.ShutdownAsync(ct), ct: ct);
        }
        catch (DataMigration.Contracts.ConfigurationException ex)
        {
            Logger.Instance.DetailedError("Configuration error", ex, "MigrationEngine", "RunAsync");
            throw;
        }
        catch (DataMigration.Contracts.DataException ex)
        {
            Logger.Instance.DetailedError("Data error", ex, "MigrationEngine", "RunAsync");
            throw;
        }
        catch (DataMigration.Contracts.PluginException ex)
        {
            Logger.Instance.DetailedError("Plugin error", ex, "MigrationEngine", "RunAsync");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Instance.DetailedError("Error executing migration task", ex, "MigrationEngine", "RunAsync");
            throw;
        }
    }

    public async Task<IEnumerable<DataRecord>> PreviewAsync(MigrationTask task, int previewLimit = 100, CancellationToken ct = default)
    {
        try
        {
            // 获取数据源插件
            var source = _pluginManager.GetDataSource(task.Source.ComponentId);
            await ErrorHandling.ExecuteWithRetry(() => source.InitializeAsync(_serviceProvider!, ct), ct: ct);

            // 获取转换器插件
            var transformers = task.Transforms.Select(t => _pluginManager.GetTransformer(t.ComponentId)).ToList();
            foreach (var transformer in transformers)
            {
                await ErrorHandling.ExecuteWithRetry(() => transformer.InitializeAsync(_serviceProvider!, ct), ct: ct);
            }

            // 构建数据流
            var dataFlow = source.ExtractAsync(task.Source, ct);

            // 应用转换器
            for (int i = 0; i < transformers.Count; i++)
            {
                if (task.Options.MaxDegreeOfParallelism > 1 && transformers[i] is IParallelTransformer parallelTransformer)
                {
                    // 优化并行处理，使用更合理的并行度
                    int parallelism = Math.Min(task.Options.MaxDegreeOfParallelism, Environment.ProcessorCount);
                    // 限制最大并行度，避免过度消耗系统资源
                    parallelism = Math.Min(parallelism, 8); // 最多8个并行任务
                    dataFlow = parallelTransformer.TransformAsync(
                        dataFlow, 
                        task.Transforms[i], 
                        parallelism,
                        ct
                    );
                }
                else if (task.Options.MaxDegreeOfParallelism > 1)
                {
                    // 对于非并行转换器，使用并行处理包装
                    var transformer = transformers[i];
                    var transformConfig = task.Transforms[i];
                    int parallelism = Math.Min(task.Options.MaxDegreeOfParallelism, Environment.ProcessorCount);
                    parallelism = Math.Min(parallelism, 8); // 最多8个并行任务
                    
                    dataFlow = ProcessInParallel(
                        dataFlow, 
                        async (record, token) => await transformer.TransformAsync(
                            new[] { record }.ToAsyncEnumerable(), 
                            transformConfig, 
                            token
                        ).FirstOrDefaultAsync(token),
                        parallelism,
                        ct
                    );
                }
                else
                {
                    dataFlow = transformers[i].TransformAsync(dataFlow, task.Transforms[i], ct);
                }
            }

            // 收集预览数据
            var previewData = new List<DataRecord>();
            int count = 0;
            await foreach (var record in dataFlow.WithCancellation(ct))
            {
                if (count >= previewLimit)
                {
                    break;
                }
                previewData.Add(record);
                count++;
            }

            // 关闭所有插件
            await ErrorHandling.ExecuteWithRetry(() => source.ShutdownAsync(ct), ct: ct);
            foreach (var transformer in transformers)
            {
                await ErrorHandling.ExecuteWithRetry(() => transformer.ShutdownAsync(ct), ct: ct);
            }

            return previewData;
        }
        catch (DataMigration.Contracts.ConfigurationException ex)
        {
            Logger.Instance.DetailedError("Configuration error", ex, "MigrationEngine", "PreviewAsync");
            throw;
        }
        catch (DataMigration.Contracts.DataException ex)
        {
            Logger.Instance.DetailedError("Data error", ex, "MigrationEngine", "PreviewAsync");
            throw;
        }
        catch (DataMigration.Contracts.PluginException ex)
        {
            Logger.Instance.DetailedError("Plugin error", ex, "MigrationEngine", "PreviewAsync");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Instance.DetailedError("Error executing preview task", ex, "MigrationEngine", "PreviewAsync");
            throw;
        }
    }

    /// <summary>
    /// 并行处理数据记录
    /// </summary>
    /// <param name="input">输入数据流</param>
    /// <param name="processFunc">处理函数</param>
    /// <param name="maxDegreeOfParallelism">最大并行度</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>处理后的数据流</returns>
    private async IAsyncEnumerable<DataRecord> ProcessInParallel(
        IAsyncEnumerable<DataRecord> input,
        Func<DataRecord, CancellationToken, Task<DataRecord>> processFunc,
        int maxDegreeOfParallelism,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct
    )
    {
        // 创建一个处理块，用于并行处理数据
        var processorBlock = new TransformBlock<DataRecord, DataRecord>(
            async record => await processFunc(record, ct),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = ct,
                EnsureOrdered = false // 不需要保持原始顺序，提高性能
            }
        );

        // 启动一个任务，将输入数据发送到处理块
        var sendTask = Task.Run(async () =>
        {
            await foreach (var record in input.WithCancellation(ct))
            {
                await processorBlock.SendAsync(record, ct);
            }
            processorBlock.Complete();
        }, ct);

        // 从处理块接收处理后的数据
        while (await processorBlock.OutputAvailableAsync(ct))
        {
            while (processorBlock.TryReceive(out var record))
            {
                if (record != null)
                {
                    yield return record;
                }
            }
        }

        // 等待所有处理完成
        await sendTask;
        await processorBlock.Completion;
    }
}
