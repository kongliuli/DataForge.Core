using DataMigration.Contracts;
using RulesEngine.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DataMigration.Plugin.RulesEngineTransformer;

public class RulesEngineTransformer : ITransformer
{
    public string Id => "Standard.RulesEngine";
    public string Name => "JSON Rules Engine";
    public Version Version => new(1, 0, 0);

    private RulesEngine.RulesEngine _engine = null!;

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        // 初始化规则引擎
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DataRecord> TransformAsync(
        IAsyncEnumerable<DataRecord> input,
        TransformConfig config,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // 从 config 中读取规则 JSON
        var rulesJson = config["RulesJson"];

        // 直接使用规则 JSON 字符串初始化 RulesEngine
        _engine = new RulesEngine.RulesEngine(new string[] { rulesJson });

        await foreach (var record in input.WithCancellation(ct))
        {
            var result = await _engine.ExecuteAllRulesAsync("workflow", record);
            // 根据规则结果修改 record 或跳过
            if (result.Any(r => r.IsSuccess == false && config.TryGetValue("SkipOnFail", out var skipOnFail) && skipOnFail == "true"))
                continue;

            yield return record;
        }
    }

    public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken ct) => Task.CompletedTask;
}
