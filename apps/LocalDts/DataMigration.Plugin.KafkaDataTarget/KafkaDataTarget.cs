using DataMigration.Contracts;
using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.KafkaDataTarget;

public class KafkaDataTarget : IDataTarget
{
    private IProducer<Null, string> _producer;

    public string Id => "DataMigration.Plugin.KafkaDataTarget";
    public string Name => "Kafka 目标源";
    public string Description => "支持将数据写入 Kafka 消息队列";
    public Version Version => new Version(1, 0, 0);

    public Task ExecuteAsync(CancellationToken ct)
    {
        // 这个方法在 IDataTarget 中不需要实现，因为数据写入是通过 LoadAsync 方法完成的
        return Task.CompletedTask;
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        // 初始化逻辑，这里可以为空，因为生产者是在 LoadAsync 中创建的
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        // 关闭生产者
        if (_producer != null)
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();
        }
    }

    public async Task LoadAsync(IAsyncEnumerable<DataRecord> data, TargetConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var bootstrapServers = config["BootstrapServers"];
        var topic = config["Topic"];
        var messageKeyField = config.TryGetValue("MessageKeyField", out var keyFieldValue) ? keyFieldValue : null;

        // 验证必需的配置参数
        if (string.IsNullOrEmpty(bootstrapServers))
            throw new ConfigurationException("BootstrapServers is required");
        if (string.IsNullOrEmpty(topic))
            throw new ConfigurationException("Topic is required");

        // 配置生产者
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        };

        // 创建生产者
        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();

        // 写入数据
        await foreach (var record in data)
        {
            // 将 DataRecord 转换为 JSON 字符串
            var messageValue = JsonSerializer.Serialize(record);

            // 发送消息
            await _producer.ProduceAsync(topic, new Message<Null, string> { Value = messageValue }, ct);
        }

        // 刷新生产者
        _producer.Flush(TimeSpan.FromSeconds(10));
    }
}
