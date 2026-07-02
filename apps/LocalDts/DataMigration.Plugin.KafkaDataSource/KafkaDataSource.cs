using DataMigration.Contracts;
using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.KafkaDataSource;

public class KafkaDataSource : IDataSource
{
    private IConsumer<Ignore, string> _consumer;

    public string Id => "DataMigration.Plugin.KafkaDataSource";
    public string Name => "Kafka 数据源";
    public string Description => "从 Kafka 消息队列读取数据";
    public Version Version => new Version(1, 0, 0);

    public Task ExecuteAsync(CancellationToken ct)
    {
        // 这个方法在 IDataSource 中不需要实现，因为数据提取是通过 ExtractAsync 方法完成的
        return Task.CompletedTask;
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        // 初始化逻辑，这里可以为空，因为消费者是在 ExtractAsync 中创建的
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        // 关闭消费者
        if (_consumer != null)
        {
            _consumer.Close();
            _consumer.Dispose();
        }
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(SourceConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var bootstrapServers = config["BootstrapServers"];
        var topic = config["Topic"];
        var groupId = config.TryGetValue("GroupId", out var groupIdValue) ? groupIdValue : "localdts-consumer-group";
        var autoOffsetReset = config.TryGetValue("AutoOffsetReset", out var offsetValue) ? offsetValue : "earliest";
        var maxRecords = config.TryGetValue("MaxRecords", out var maxValue) && int.TryParse(maxValue, out var max) ? max : 1000;

        // 验证必需的配置参数
        if (string.IsNullOrEmpty(bootstrapServers))
            throw new ConfigurationException("BootstrapServers is required");
        if (string.IsNullOrEmpty(topic))
            throw new ConfigurationException("Topic is required");

        // 配置消费者
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = autoOffsetReset switch
            {
                "earliest" => AutoOffsetReset.Earliest,
                "latest" => AutoOffsetReset.Latest,
                _ => AutoOffsetReset.Earliest
            },
            EnableAutoCommit = false
        };

        // 创建消费者
        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();

        // 订阅主题
        _consumer.Subscribe(topic);

        try
        {
            int recordsProcessed = 0;

            // 消费消息
            while (!ct.IsCancellationRequested && recordsProcessed < maxRecords)
            {
                var consumeResult = _consumer.Consume(ct);
                
                // 创建数据记录
                var dataRecord = new DataRecord
                {
                    ["Topic"] = consumeResult.Topic,
                    ["Partition"] = consumeResult.Partition.Value,
                    ["Offset"] = consumeResult.Offset.Value,
                    ["Timestamp"] = consumeResult.Message.Timestamp.UtcDateTime,
                    ["Value"] = consumeResult.Message.Value
                };

                yield return dataRecord;
                recordsProcessed++;
            }
        }
        finally
        {
            // 提交偏移量
            _consumer.Commit();
        }
    }
}
