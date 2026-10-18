using System.Globalization;

namespace DataForge.Jobs.Execution;

/// <summary>
/// 一次 <c>run</c> 的行数对账快照（只读）。用来回答"迁移到底丢没丢数据"。
/// </summary>
/// <remarks>
/// <para><b>口径</b>（全部来自执行期计数器）：</para>
/// <list type="bullet">
/// <item><description><see cref="RowsRead"/>：源端产出的原始行数。
/// 单源路径 = 源管道吐出、进入 <c>transforms</c> 之前的行数；
/// JOIN 路径（<c>sources[]</c>）= 各来源按 <c>on</c> 内连接之后、进入 <c>transforms</c> 之前的行数。
/// 注意：关系型来源（sqlite/postgresql/sqlserver/mysql）会把 watermark 条件下推到 SQL，
/// 这些来源在增量运行时本来就不会返回旧行，因此这部分行既不计入 <see cref="RowsRead"/>，也不计入 <see cref="RowsSkippedByWatermark"/>。</description></item>
/// <item><description><see cref="RowsSkippedByWatermark"/>：被 watermark 过滤掉的行数。
/// 只统计 runner 侧的 watermark 阶段（发生在 <c>transforms</c> 之后）；下推到 SQL 的那部分见上一条。</description></item>
/// <item><description><see cref="RowsFiltered"/>：被 <c>transforms.where</c> 过滤掉的行数（在 where 谓词处计数）。</description></item>
/// <item><description><see cref="RowsRejectedByValidation"/>：被 <c>validate</c> 判定为坏行并丢弃的行数（<b>按行</b>计：
/// 同一行触发多条规则只算一行）。未配置 <c>validate</c> 时为 <c>0</c>；
/// 配置了 <c>validate</c> 但来源提供者返回的不是 DataForge 管道实例（计数器不可观测）时为 <c>null</c>；
/// JOIN 路径不执行 <c>validate</c> 阶段，因此 JOIN 作业即使写了 <c>validate</c> 也记为 <c>null</c>（不代表校验通过）。</description></item>
/// <item><description><see cref="RowsWritten"/>：sink 实际写入行数，取自 <c>ExportResults.RecordsWritten</c>
/// （取坏行文件等收尾动作之后的最终值）。</description></item>
/// <item><description><see cref="Difference"/> = <see cref="RowsRead"/> − <see cref="RowsFiltered"/> − <see cref="RowsSkippedByWatermark"/>
/// − (<see cref="RowsRejectedByValidation"/> ?? 0) − <see cref="RowsWritten"/>。
/// 非 0 表示存在上述环节之外的丢行（例如 sink 的 upsert 去重、批量写入失败等）。</description></item>
/// </list>
/// <para><b>局限</b>：本次对账完全基于执行期计数器，<b>不会</b>重新读取目标端核对落库行数
/// （<c>IJobCatalogProvider</c> 没有 COUNT 能力，无凭据、无连接即无法核对）。
/// 目标端已有数据、sink 的写入语义造成的差异只体现在 <see cref="Difference"/> 上。</para>
/// <para><b>严格模式</b>：<see cref="Reconciled"/> 为 <c>false</c> 只是一个提示，
/// <see cref="JobRunner"/> 不会因此把作业判为失败（<see cref="JobRunResult.Success"/> 仍为 <c>true</c>），
/// 报告 JSON 会如实给出差异。需要"差异即失败"的调用方可自行读取 <see cref="JobRunResult.Reconciliation"/> 判定；
/// CLI 有意不新增开关。</para>
/// </remarks>
public sealed record JobRunReconciliation
{
    /// <summary>源端产出的原始行数（单源路径 = 进 transforms 之前；JOIN 路径 = 内连接结果行数）。</summary>
    public long RowsRead { get; init; }

    /// <summary>被 watermark 过滤掉的行数（runner 侧过滤）。</summary>
    public long RowsSkippedByWatermark { get; init; }

    /// <summary>被 <c>transforms.where</c> 过滤掉的行数。</summary>
    public long RowsFiltered { get; init; }

    /// <summary>
    /// 被 <c>validate</c> 判定为坏行并丢弃的行数（按行计）。
    /// 未配置 validate 时为 0；计数器不可观测（非 DataForge 管道实现）或 JOIN 路径下配置了 validate 时为 null。
    /// </summary>
    public long? RowsRejectedByValidation { get; init; }

    /// <summary>sink 实际写入行数（ExportResults.RecordsWritten 的最终值）。</summary>
    public long RowsWritten { get; init; }

    /// <summary>对账差异 = RowsRead − filtered − skipped − rejected − written。0 表示执行期计数器自洽。</summary>
    public long Difference =>
        RowsRead - RowsFiltered - RowsSkippedByWatermark - (RowsRejectedByValidation ?? 0) - RowsWritten;

    /// <summary>是否对账平衡（<see cref="Difference"/> == 0）。为 false 只是提示，不会让作业失败。</summary>
    public bool Reconciled => Difference == 0;

    /// <summary>单行摘要，供 CLI/日志输出，例如 <c>Reconciled: read=3 skipped=0 filtered=1 rejected=0 written=2 difference=0</c>。</summary>
    /// <returns>对账摘要文本。</returns>
    public string ToSummary()
    {
        var prefix = Reconciled ? "Reconciled" : "Not reconciled";
        var rejected = RowsRejectedByValidation is { } value
            ? value.ToString(CultureInfo.InvariantCulture)
            : "n/a";
        return string.Create(CultureInfo.InvariantCulture,
            $"{prefix}: read={RowsRead} skipped={RowsSkippedByWatermark} filtered={RowsFiltered} rejected={rejected} written={RowsWritten} difference={Difference}");
    }
}
