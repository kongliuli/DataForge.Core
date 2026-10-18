using DataForge.Jobs.Catalog;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.Preflight.Internal;

/// <summary>
/// Optional (CheckSchema=true) object-level checks against the catalog providers: connectivity, table
/// existence, and coarse column existence / type-kind hints. Every probe is read-only (connection test and
/// metadata queries); no DDL, no row reads, and preflight never creates a table or directory.
/// </summary>
/// <remarks>
/// Honest limits: the "type" checks are kind-level hints only (numeric / text / temporal / binary), not a full
/// source-to-sink type mapping; permission grants are never verified. When a catalog provider is not
/// registered, the check degrades to a warning. File-based sources (csv/json/excel/parquet) have no catalog
/// provider and are never probed.
/// </remarks>
internal static class JobSchemaPreflight
{
    public static async Task CheckAsync(ResolvedTarget target, JobPreflightOptions options, IssueBag issues, CancellationToken cancellationToken)
    {
        var job = target.Job;
        var label = target.Label;

        var sourceType = job.Source.Type?.Trim().ToLowerInvariant() ?? "";
        List<TableColumnInfo>? sourceColumns = null;

        if (job.Sources.Count == 0 && PreflightCapabilities.RelationalSourceTypes.Contains(sourceType) &&
            !string.IsNullOrWhiteSpace(job.Source.Connection) && !string.IsNullOrWhiteSpace(job.Source.Table))
        {
            sourceColumns = await CheckTableAsync(
                sourceType, job.Source.Connection, job.Source.Table, $"{label}.source",
                issues, options, cancellationToken, sinkSide: false, createTable: false).ConfigureAwait(false);

            if (sourceColumns is not null)
            {
                if (job.Watermark is { Column: var watermarkColumn } && !string.IsNullOrWhiteSpace(watermarkColumn) &&
                    !sourceColumns.Any(c => string.Equals(c.Name, watermarkColumn, StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Error(
                        PreflightCodes.WatermarkColumnMissing,
                        $"watermark.column '{watermarkColumn}' 不存在于来源表 '{job.Source.Table}' 的列中（列：{string.Join(", ", sourceColumns.Select(c => c.Name))}）——增量比较将永远取不到值。",
                        $"{label}.watermark.column");
                }

                CheckRuleColumns(job.Validate, sourceColumns, label, issues);
                ReportTypeHints(sourceColumns, job.Validate, label, issues);
            }
        }

        for (var i = 0; i < job.Sources.Count; i++)
        {
            var join = job.Sources[i];
            var joinType = join.Type?.Trim().ToLowerInvariant() ?? "";
            if (!PreflightCapabilities.RelationalSourceTypes.Contains(joinType) ||
                string.IsNullOrWhiteSpace(join.Connection) || string.IsNullOrWhiteSpace(join.Table))
                continue;

            var columns = await CheckTableAsync(
                joinType, join.Connection, join.Table, $"{label}.sources[{i}]",
                issues, options, cancellationToken, sinkSide: false, createTable: false).ConfigureAwait(false);

            if (columns is null || string.IsNullOrWhiteSpace(join.On) || !join.On.Contains('='))
                continue;

            var rightKey = join.On.Split('=', 2, StringSplitOptions.TrimEntries)[1];
            if (!columns.Any(c => string.Equals(c.Name, rightKey, StringComparison.OrdinalIgnoreCase)))
                issues.Warning(
                    PreflightCodes.SourceColumnMissing,
                    $"sources[{i}].on 的右侧列 '{rightKey}' 不存在于表 '{join.Table}' 中：join 结果将为空集。",
                    $"{label}.sources[{i}].on");
        }

        var sinkType = job.Sink.Type?.Trim().ToLowerInvariant() ?? "";
        if (!PreflightCapabilities.SqlTableSinkTypes.Contains(sinkType) ||
            string.IsNullOrWhiteSpace(job.Sink.Connection) || string.IsNullOrWhiteSpace(job.Sink.Table))
            return;

        var sinkColumns = await CheckTableAsync(
            sinkType, job.Sink.Connection, job.Sink.Table, $"{label}.sink",
            issues, options, cancellationToken, sinkSide: true, createTable: job.Sink.CreateTable).ConfigureAwait(false);

        if (sinkColumns is null)
            return;

        if (job.Sink.Keys.Count > 0 && sourceColumns is not null)
        {
            foreach (var key in job.Sink.Keys)
            {
                if (sourceColumns.Any(c => string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase)))
                    continue;
                issues.Warning(
                    PreflightCodes.SinkKeyMissing,
                    $"sink.keys 列 '{key}' 不存在于来源列中（来源列：{string.Join(", ", sourceColumns.Select(c => c.Name))}）：upsert 时该键恒为空值。",
                    $"{label}.sink.keys");
            }
        }
    }

    private static async Task<List<TableColumnInfo>?> CheckTableAsync(
        string type,
        string connectionString,
        string table,
        string path,
        IssueBag issues,
        JobPreflightOptions options,
        CancellationToken cancellationToken,
        bool sinkSide,
        bool createTable)
    {
        if (!JobCatalog.TryGet(type, out _))
        {
            issues.Warning(
                PreflightCodes.CatalogUnavailable,
                $"{path}：类型 '{type}' 未注册 catalog provider，已跳过连接/表/列检查（调用对应 AddDataForgeJobs* 注册后再试）。",
                path);
            return null;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(options.CheckTimeout);

            var test = await JobCatalog.TestConnectionAsync(type, connectionString, cts.Token).ConfigureAwait(false);
            if (!test.Success)
            {
                issues.Error(
                    sinkSide ? PreflightCodes.SinkConnectionFailed : PreflightCodes.SourceConnectionFailed,
                    $"{path}：连接测试失败 —— {test.Error}（未读取任何业务数据）。",
                    path);
                return null;
            }

            var exists = await JobCatalog.TableExistsAsync(type, connectionString, table, cts.Token).ConfigureAwait(false);
            if (!exists)
            {
                if (sinkSide)
                {
                    var canCreate = createTable && PreflightCapabilities.CreateTableCapableSinkTypes.Contains(type);
                    if (canCreate)
                        issues.Info(
                            PreflightCodes.SinkTableCreateHint,
                            $"{path}：目标表 '{table}' 不存在，但 sink.createTable=true 且 {type} 支持，运行时将按首行自动建表（预检不建表）。",
                            path);
                    else
                        issues.Error(
                            PreflightCodes.SinkTableMissing,
                            $"{path}：目标表 '{table}' 不存在，且当前 {type} sink 不会自动建表"
                            + (PreflightCapabilities.CreateTableCapableSinkTypes.Contains(type) ? "（需设置 sink.createTable=true）" : "（仅 sqlite/postgresql/postgres 支持 createTable）")
                            + "。",
                            path);
                }
                else
                {
                    var tables = await SafeListTablesAsync(type, connectionString, cts.Token).ConfigureAwait(false);
                    issues.Error(
                        PreflightCodes.SourceTableMissing,
                        $"{path}：来源表 '{table}' 不存在（现有表：{string.Join(", ", tables)}）。",
                        path);
                }

                return null;
            }

            return (await JobCatalog.DescribeTableAsync(type, connectionString, table, cts.Token).ConfigureAwait(false)).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            issues.Warning(PreflightCodes.CatalogCheckFailed, $"{path}：catalog 检查超时（{options.CheckTimeout.TotalSeconds:0}s）。", path);
            return null;
        }
        catch (Exception ex)
        {
            issues.Warning(PreflightCodes.CatalogCheckFailed, $"{path}：catalog 检查异常 —— {ex.Message}", path);
            return null;
        }
    }

    private static async Task<IReadOnlyList<string>> SafeListTablesAsync(string type, string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            return await JobCatalog.ListTablesAsync(type, connectionString, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Column existence for validate rules. Only runs when a real schema is available.</summary>
    private static void CheckRuleColumns(ValidateDefinition? validate, List<TableColumnInfo> columns, string label, IssueBag issues)
    {
        if (validate is null || validate.Rules.Count == 0)
            return;

        for (var i = 0; i < validate.Rules.Count; i++)
        {
            var field = validate.Rules[i].Field;
            if (string.IsNullOrWhiteSpace(field))
                continue;
            if (columns.Any(c => string.Equals(c.Name, field, StringComparison.OrdinalIgnoreCase)))
                continue;
            issues.Warning(
                PreflightCodes.ValidateFieldMissing,
                $"validate.rules[{i}].field '{field}' 不存在于来源列中（列：{string.Join(", ", columns.Select(c => c.Name))}）。",
                $"{label}.validate.rules[{i}].field");
        }
    }

    /// <summary>
    /// Kind-level type hints. Deliberately coarse: this is NOT full source-to-sink type mapping and does not
    /// claim to verify compatibility; it only flags rules that clearly cannot work against a known column kind.
    /// </summary>
    private static void ReportTypeHints(List<TableColumnInfo> columns, ValidateDefinition? validate, string label, IssueBag issues)
    {
        if (validate is null || validate.Rules.Count == 0)
            return;

        var byName = columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < validate.Rules.Count; i++)
        {
            var rule = validate.Rules[i];
            if (string.IsNullOrWhiteSpace(rule.Field) || !byName.TryGetValue(rule.Field, out var column))
                continue;

            var kind = Classify(column.Type);
            if ((rule.Min is not null || rule.Max is not null) && kind is ColumnKind.Text or ColumnKind.Temporal or ColumnKind.Binary)
                issues.Warning(
                    PreflightCodes.LimitationTypeMappingNotPerformed,
                    $"rules[{i}]（field='{rule.Field}'）设置了 min/max，但来源列类型为 '{column.Type}'（粗检判定为非数值）：只有能被解析为数值的值才会参与比较，多数行会被静默放行。",
                    $"{label}.validate.rules[{i}]");
            else if (!string.IsNullOrWhiteSpace(rule.Pattern) && kind is ColumnKind.Numeric)
                issues.Warning(
                    PreflightCodes.LimitationTypeMappingNotPerformed,
                    $"rules[{i}]（field='{rule.Field}'）设置了 pattern，但来源列类型为 '{column.Type}'（粗检判定为数值）：正则很少命中，可能是配置错误。",
                    $"{label}.validate.rules[{i}]");
        }
    }

    private enum ColumnKind
    {
        Unknown,
        Numeric,
        Text,
        Temporal,
        Binary
    }

    private static ColumnKind Classify(string type)
    {
        var t = type.ToLowerInvariant();
        if (t.Contains("char") || t.Contains("text") || t.Contains("string") || t.Contains("uuid") || t.Contains("clob") || t.Contains("enum"))
            return ColumnKind.Text;
        if (t.Contains("int") || t.Contains("numeric") || t.Contains("decimal") || t.Contains("number") || t.Contains("float") || t.Contains("double") || t.Contains("real") || t.Contains("money"))
            return ColumnKind.Numeric;
        if (t.Contains("date") || t.Contains("time") || t.Contains("timestamp"))
            return ColumnKind.Temporal;
        if (t.Contains("blob") || t.Contains("binary") || t.Contains("bytea"))
            return ColumnKind.Binary;
        return ColumnKind.Unknown;
    }
}
