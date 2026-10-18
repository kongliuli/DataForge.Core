using System.Text.Json;
using System.Text.RegularExpressions;
using DataForge.Jobs.Catalog;
using DataForge.Jobs.Execution;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;
using DataForge.Jobs.Scheduling;

namespace DataForge.Jobs.Preflight.Internal;

/// <summary>
/// Capability matrix for the adapters shipped in this repository. These flags mirror the current
/// implementations (DataForge.Jobs.* sinks/sources); they intentionally do not guess what a third-party
/// provider might support — unregistered types only fail the "is registered" check.
/// </summary>
internal static class PreflightCapabilities
{
    /// <summary>Types parsed by JobYamlParser (required-field rules below mirror it).</summary>
    internal static readonly IReadOnlySet<string> KnownSourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "csv", "json", "sqlserver", "sqlite", "postgresql", "postgres", "mysql",
        "excel", "http", "rest", "parquet", "duckdb"
    };

    internal static readonly IReadOnlySet<string> KnownSinkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "json", "csv", "sqlserver", "sqlite", "postgresql", "postgres", "mysql",
        "parquet", "duckdb", "excel", "http", "rest"
    };

    /// <summary>Types whose data arrives as a local file (path must exist before reading).</summary>
    internal static readonly IReadOnlySet<string> FileSourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "csv", "json", "excel", "parquet", "duckdb"
    };

    internal static readonly IReadOnlySet<string> RelationalSourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sqlserver", "sqlite", "postgresql", "postgres", "mysql"
    };

    internal static readonly IReadOnlySet<string> UrlTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "http", "rest"
    };

    /// <summary>Sinks that write into a SQL table (connection + table required, mode/keys meaningful).</summary>
    internal static readonly IReadOnlySet<string> SqlTableSinkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sqlite", "postgresql", "postgres", "sqlserver", "mysql"
    };

    /// <summary>Sinks that honor mode=upsert (sink implementations that implement merge/ON CONFLICT).</summary>
    internal static readonly IReadOnlySet<string> UpsertCapableSinkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sqlite", "postgresql", "postgres", "sqlserver"
    };

    /// <summary>Sinks that honor sink.createTable.</summary>
    internal static readonly IReadOnlySet<string> CreateTableCapableSinkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sqlite", "postgresql", "postgres"
    };

    /// <summary>Relational sinks that honor sink.options.batchSize.</summary>
    internal static readonly IReadOnlySet<string> BatchSizeCapableSinkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sqlite", "postgresql", "postgres", "sqlserver"
    };

    /// <summary>File sinks that create the output directory when missing.</summary>
    internal static readonly IReadOnlySet<string> DirectoryCreatingSinkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "json", "csv", "duckdb"
    };
}

internal static class JobDeepClone
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Deep-copies a job so preflight can resolve variables without mutating the caller's instance.</summary>
    public static JobDefinition DeepClone(this JobDefinition job) =>
        JsonSerializer.SerializeToNode(job, JsonOptions)?.Deserialize<JobDefinition>(JsonOptions)
        ?? throw new InvalidOperationException("Job deep clone produced null.");
}

/// <summary>Required-field, capability and side-effect-free checks shared by single jobs and join sources.</summary>
internal static class JobSourcePreflight
{
    private static readonly Regex UnresolvedVariable = new(@"\$\{[^}]+\}", RegexOptions.CultureInvariant);

    public static void Check(JobDefinition job, string label, JobPreflightOptions options, IssueBag issues)
    {
        var primaryIgnored = job.Sources.Count > 0;
        if (primaryIgnored)
        {
            var p = job.Source;
            if (!string.IsNullOrWhiteSpace(p.Path) || !string.IsNullOrWhiteSpace(p.Connection) || !string.IsNullOrWhiteSpace(p.Table))
                issues.Warning(
                    PreflightCodes.JoinPrimaryIgnored,
                    "配置了 sources 列表时 source 将被忽略（运行时只读取 sources）。",
                    "source");
        }

        if (job.Sources.Count == 0)
        {
            // Primary source issues are attributed to "<label>.source" so batch jobs stay distinguishable.
            CheckOne(job.Source.Type, job.Source.Path, job.Source.Connection, job.Source.Table,
                job.Source.Query, job.Source.Options, label, $"{label}.source", null, options, issues);
            return;
        }

        for (var i = 0; i < job.Sources.Count; i++)
        {
            var join = job.Sources[i];
            var prefix = $"{label}.sources[{i}]";
            CheckOne(join.Type, join.Path, join.Connection, join.Table, join.Query,
                join.Options, label, prefix, join.On, options, issues);

            if (i == 0)
                continue;

            if (string.IsNullOrWhiteSpace(join.On))
            {
                issues.Error(
                    PreflightCodes.JoinOnRequired,
                    $"sources[{i}].on 为空：join 的第二个及以后的来源必须提供等值连接条件（LeftCol = RightCol）。",
                    $"{prefix}.on");
            }
            else if (!join.On.Contains('='))
            {
                issues.Error(
                    PreflightCodes.JoinOnInvalid,
                    $"sources[{i}].on 缺少 '='：运行时要求格式 LeftCol = RightCol，实际为 '{join.On}'。",
                    $"{prefix}.on");
            }
        }
    }

    private static void CheckOne(
        string type,
        string path,
        string? connection,
        string? table,
        string? query,
        SourceOptionsDefinition? options,
        string label,
        string prefix,
        string? joinOn,
        JobPreflightOptions preflightOptions,
        IssueBag issues)
    {
        var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        var pathKey = $"{prefix}.path";

        if (string.IsNullOrWhiteSpace(type))
        {
            issues.Error(PreflightCodes.SourceTypeRequired, $"{label} 的 source.type 不能为空。", $"{prefix}.type");
            return;
        }

        if (!PreflightCapabilities.KnownSourceTypes.Contains(normalized))
        {
            issues.Warning(
                PreflightCodes.SourceTypeUnregistered,
                $"source.type '{type}' 未由当前 JobYamlParser 识别，作业文件解析阶段将失败。",
                $"{prefix}.type");
        }

        if (!JobProviderRegistry.TryGetSource(normalized, out _))
            issues.Error(
                PreflightCodes.SourceTypeUnregistered,
                $"source.type '{type}' 的适配器未注册：请引用对应的 DataForge.Jobs.* 包并在启动时调用其注册方法（内置类型：csv、json）。",
                $"{prefix}.type");

        // Required fields per type — mirrors JobYamlParser.ValidateSource.
        if (PreflightCapabilities.RelationalSourceTypes.Contains(normalized))
        {
            if (string.IsNullOrWhiteSpace(connection))
                issues.Error(PreflightCodes.SourceConnectionRequired, $"{normalized} 来源必须提供 source.connection。", $"{prefix}.connection");
            if (string.IsNullOrWhiteSpace(table))
                issues.Error(PreflightCodes.SourceTableRequired, $"{normalized} 来源必须提供 source.table。", $"{prefix}.table");
            if (!string.IsNullOrWhiteSpace(query))
                issues.Warning(
                    PreflightCodes.SourceQueryIgnored,
                    $"当前 {normalized} 适配器忽略 source.query，只读取整表（可配合 watermark 增量）。",
                    $"{prefix}.query");
        }
        else if (normalized is "duckdb")
        {
            if (string.IsNullOrWhiteSpace(path))
                issues.Error(PreflightCodes.SourcePathRequired, "duckdb 来源必须提供 source.path（数据库文件）。", pathKey);
            if (string.IsNullOrWhiteSpace(query))
                issues.Error(PreflightCodes.SourceQueryRequired, "duckdb 来源必须提供 source.query。", $"{prefix}.query");
        }
        else if (PreflightCapabilities.FileSourceTypes.Contains(normalized) || PreflightCapabilities.UrlTypes.Contains(normalized))
        {
            if (string.IsNullOrWhiteSpace(path))
                issues.Error(PreflightCodes.SourcePathRequired, $"{normalized} 来源必须提供 source.path。", pathKey);
        }

        // Path checks — never open the file, only File.Exists / URL syntax.
        if (!string.IsNullOrWhiteSpace(path) && !UnresolvedVariable.IsMatch(path))
        {
            if (PreflightCapabilities.UrlTypes.Contains(normalized))
            {
                if (!Uri.TryCreate(path, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    issues.Error(PreflightCodes.SourceUrlInvalid, $"source.path 不是有效的 http(s) URL：'{path}'。", pathKey);
            }
            else if (PreflightCapabilities.FileSourceTypes.Contains(normalized) && !File.Exists(path))
            {
                issues.Warning(
                    PreflightCodes.SourcePathMissing,
                    $"source.path 文件不存在（不读取内容，仅检查存在性）：'{path}'。相对路径基于作业文件目录解析。",
                    pathKey);
            }
        }

        CheckUnusedOptions(normalized, options, prefix, issues);

        if (string.IsNullOrWhiteSpace(connection))
            return;

        if (UnresolvedVariable.IsMatch(connection))
        {
            issues.Warning(
                PreflightCodes.SinkVariableUnresolved,
                $"source.connection 仍包含未解析的 ${{NAME}} 变量，运行时将原样传给适配器。",
                $"{prefix}.connection");
            return;
        }

        if (preflightOptions.CheckSchema)
        {
            // Connection failures are sink/source agnostic but reported with the source code set.
            if (!JobCatalog.TryGet(normalized, out _))
            {
                issues.Warning(
                    PreflightCodes.CatalogUnavailable,
                    $"{prefix}（{label} 的来源）：类型 '{normalized}' 未注册 catalog provider，跳过连接与对象检查（调用对应 AddDataForgeJobs* 注册）。",
                    prefix);
                return;
            }

            try
            {
                using var cts = new CancellationTokenSource(preflightOptions.CheckTimeout);
                var result = JobCatalog.TestConnectionAsync(normalized, connection, cts.Token).GetAwaiter().GetResult();
                if (!result.Success)
                    issues.Error(PreflightCodes.SourceConnectionFailed, $"{prefix}（{label} 的来源）：连接测试失败 —— {result.Error}", $"{prefix}.connection");
            }
            catch (OperationCanceledException)
            {
                issues.Warning(PreflightCodes.CatalogCheckFailed, $"{prefix}（{label} 的来源）：连接测试超时（{preflightOptions.CheckTimeout.TotalSeconds:0}s）。", $"{prefix}.connection");
            }
            catch (Exception ex)
            {
                issues.Warning(PreflightCodes.CatalogCheckFailed, $"{prefix}（{label} 的来源）：连接检查异常 —— {ex.Message}", $"{prefix}.connection");
            }
        }
    }

    private static void CheckUnusedOptions(
        string normalizedType,
        SourceOptionsDefinition? options,
        string prefix,
        IssueBag issues)
    {
        if (options is null)
            return;

        var hasHeaderUsed = normalizedType is "csv";
        var delimiterUsed = normalizedType is "csv";
        if (options.HasHeader is not null && !hasHeaderUsed)
            issues.Warning(PreflightCodes.SourceOptionIgnored, $"source.options.hasHeader 对 {normalizedType} 来源无效（当前仅 csv 使用）。", $"{prefix}.options.hasHeader");
        if (options.Delimiter is not null && !delimiterUsed)
            issues.Warning(PreflightCodes.SourceOptionIgnored, $"source.options.delimiter 对 {normalizedType} 来源无效（当前仅 csv 使用）。", $"{prefix}.options.delimiter");
    }

    /// <summary>Non-destructive connectivity probe via the catalog provider. Read-only (SELECT 1 / list tables).</summary>
    internal static void CheckSinkConnection(
        string type,
        string connectionString,
        string what,
        IssueBag issues,
        TimeSpan timeout)
    {
        if (!JobCatalog.TryGet(type, out _))
        {
            issues.Warning(
                PreflightCodes.CatalogUnavailable,
                $"{what}：类型 '{type}' 未注册 catalog provider，跳过连接与对象检查（调用对应 AddDataForgeJobs* 注册）。",
                what);
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var result = JobCatalog.TestConnectionAsync(type, connectionString, cts.Token).GetAwaiter().GetResult();
            if (!result.Success)
                issues.Error(PreflightCodes.SinkConnectionFailed, $"{what}：连接测试失败 —— {result.Error}", what);
        }
        catch (OperationCanceledException)
        {
            issues.Warning(PreflightCodes.CatalogCheckFailed, $"{what}：连接测试超时（{timeout.TotalSeconds:0}s）。", what);
        }
        catch (Exception ex)
        {
            issues.Warning(PreflightCodes.CatalogCheckFailed, $"{what}：连接检查异常 —— {ex.Message}", what);
        }
    }
}

internal static class JobSinkPreflight
{
    private static readonly Regex UnresolvedVariable = new(@"\$\{[^}]+\}", RegexOptions.CultureInvariant);

    public static void Check(JobDefinition job, string label, JobPreflightOptions options, IssueBag issues)
    {
        var sink = job.Sink;
        var normalized = (sink.Type ?? string.Empty).Trim().ToLowerInvariant();
        var pathKey = $"{label}.sink.path";

        if (string.IsNullOrWhiteSpace(sink.Type))
        {
            issues.Error(PreflightCodes.SinkTypeRequired, $"{label} 的 sink.type 不能为空。", $"{label}.sink.type");
            return;
        }

        if (!PreflightCapabilities.KnownSinkTypes.Contains(normalized))
            issues.Warning(
                PreflightCodes.SinkTypeUnregistered,
                $"sink.type '{sink.Type}' 未由当前 JobYamlParser 识别，作业文件解析阶段将失败。",
                $"{label}.sink.type");

        if (!JobProviderRegistry.TryGetSink(normalized, out _))
            issues.Error(
                PreflightCodes.SinkTypeUnregistered,
                $"sink.type '{sink.Type}' 的适配器未注册：请引用对应的 DataForge.Jobs.* 包并在启动时调用其注册方法（内置类型：csv、json）。",
                $"{label}.sink.type");

        // Required fields — mirrors JobYamlParser.ValidateSink.
        if (PreflightCapabilities.SqlTableSinkTypes.Contains(normalized))
        {
            if (string.IsNullOrWhiteSpace(sink.Connection))
                issues.Error(PreflightCodes.SinkConnectionRequired, $"{normalized} 目标必须提供 sink.connection。", $"{label}.sink.connection");
            if (string.IsNullOrWhiteSpace(sink.Table))
                issues.Error(PreflightCodes.SinkTableRequired, $"{normalized} 目标必须提供 sink.table。", $"{label}.sink.table");
        }
        else if (normalized is "duckdb")
        {
            if (string.IsNullOrWhiteSpace(sink.Path))
                issues.Error(PreflightCodes.SinkPathRequired, "duckdb 目标必须提供 sink.path（数据库文件）。", pathKey);
            if (string.IsNullOrWhiteSpace(sink.Table))
                issues.Error(PreflightCodes.SinkTableRequired, "duckdb 目标必须提供 sink.table。", $"{label}.sink.table");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(sink.Path))
                issues.Error(PreflightCodes.SinkPathRequired, $"{normalized} 目标必须提供 sink.path。", pathKey);
        }

        CheckModeAndKeys(sink, normalized, label, issues);
        CheckOptions(sink, normalized, label, issues);
        CheckPaths(sink, normalized, job, label, pathKey, issues);

        if (!string.IsNullOrWhiteSpace(sink.Connection) && UnresolvedVariable.IsMatch(sink.Connection))
            issues.Warning(
                PreflightCodes.SinkVariableUnresolved,
                "sink.connection 仍包含未解析的 ${NAME} 变量，运行时将原样传给适配器。",
                $"{label}.sink.connection");
    }

    private static void CheckModeAndKeys(SinkDefinition sink, string normalized, string label, IssueBag issues)
    {
        var mode = sink.Mode?.Trim().ToLowerInvariant() ?? "insert";
        if (mode is not ("insert" or "upsert"))
            issues.Warning(
                PreflightCodes.SinkModeUnknown,
                $"sink.mode '{sink.Mode}' 未被任何适配器识别：运行时一律按 insert（追加行）处理，不会替换或删除已有数据。",
                $"{label}.sink.mode");

        var isUpsert = mode is "upsert";
        if (isUpsert && !PreflightCapabilities.UpsertCapableSinkTypes.Contains(normalized))
            issues.Warning(
                PreflightCodes.SinkModeUnsupported,
                $"{normalized} 适配器当前不支持 mode=upsert：运行时会退化为普通 insert，可能产生重复行；且 sink.keys 不生效。",
                $"{label}.sink.mode");

        if (isUpsert && PreflightCapabilities.UpsertCapableSinkTypes.Contains(normalized) && sink.Keys.Count == 0)
            issues.Error(
                PreflightCodes.SinkKeysRequired,
                $"mode=upsert 时必须提供 sink.keys（运行时 sink 会抛出 \"Upsert mode requires sink.keys.\"）。",
                $"{label}.sink.keys");

        if (!isUpsert && sink.Keys.Count > 0 && PreflightCapabilities.SqlTableSinkTypes.Contains(normalized))
            issues.Warning(
                PreflightCodes.SinkKeysIgnored,
                $"sink.keys 仅在 mode=upsert 时生效，当前 mode='{sink.Mode}' 将忽略 keys。",
                $"{label}.sink.keys");

        if (sink.Keys.Count > 1)
        {
            var duplicated = sink.Keys
                .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
            if (duplicated.Length > 0)
                issues.Warning(
                    PreflightCodes.SinkKeysDuplicated,
                    $"sink.keys 存在重复列：{string.Join(", ", duplicated)}。",
                    $"{label}.sink.keys");
        }

        if (sink.CreateTable && !PreflightCapabilities.CreateTableCapableSinkTypes.Contains(normalized))
            issues.Warning(
                PreflightCodes.SinkCreateTableUnsupported,
                $"sink.createTable=true 对 {normalized} 无效：当前仅 sqlite/postgresql/postgres 支持按首行建表；其它适配器要求目标已存在。",
                $"{label}.sink.createTable");
    }

    private static void CheckOptions(SinkDefinition sink, string normalized, string label, IssueBag issues)
    {
        var options = sink.Options;
        if (options?.BatchSize is { } batchSize && batchSize <= 0)
            issues.Error(
                PreflightCodes.SinkBatchSizeNonPositive,
                $"sink.options.batchSize 必须大于 0（当前 {batchSize}）。",
                $"{label}.sink.options.batchSize");
        else if (options?.BatchSize is not null && !PreflightCapabilities.BatchSizeCapableSinkTypes.Contains(normalized))
            issues.Warning(
                PreflightCodes.SinkOptionIgnored,
                $"sink.options.batchSize 对 {normalized} 目标无效：该 sink 当前不按批次写入或被忽略。",
                $"{label}.sink.options.batchSize");

        if (options?.IncludeHeader is not null && normalized is not ("csv"))
            issues.Warning(
                PreflightCodes.SinkOptionIgnored,
                $"sink.options.includeHeader 对 {normalized} 目标无效（当前仅 csv sink 使用）。",
                $"{label}.sink.options.includeHeader");

        if (options?.Indented is not null && normalized is not ("json"))
            issues.Warning(
                PreflightCodes.SinkOptionIgnored,
                $"sink.options.indented 对 {normalized} 目标无效（当前仅 json sink 使用）。",
                $"{label}.sink.options.indented");
    }

    private static void CheckPaths(SinkDefinition sink, string normalized, JobDefinition job, string label, string pathKey, IssueBag issues)
    {
        var path = sink.Path;
        if (string.IsNullOrWhiteSpace(path) || UnresolvedVariable.IsMatch(path))
            return;

        if (PreflightCapabilities.UrlTypes.Contains(normalized))
        {
            if (!Uri.TryCreate(path, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                issues.Error(PreflightCodes.SinkUrlInvalid, $"sink.path 不是有效的 http(s) URL：'{path}'。", pathKey);
            return;
        }

        // Non-file sinks (sqltable) have no path; duckdb/json/csv/parquet/excel do.
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) &&
            !Directory.Exists(directory) &&
            !PreflightCapabilities.DirectoryCreatingSinkTypes.Contains(normalized))
            issues.Warning(
                PreflightCodes.SinkPathRequired,
                $"sink.path 的父目录不存在：'{directory}'。{normalized} sink 不会自动创建目录，运行时将失败（不创建任何内容，仅检查）。",
                pathKey);

        foreach (var sourcePath in EnumerateSourcePaths(job))
        {
            if (!string.Equals(sourcePath, path, StringComparison.OrdinalIgnoreCase))
                continue;
            issues.Warning(
                PreflightCodes.SinkPathRequired,
                "sink.path 与某个 source.path 指向同一文件：读写同一文件可能覆盖或污染输入数据。",
                pathKey);
            break;
        }
    }

    private static IEnumerable<string> EnumerateSourcePaths(JobDefinition job)
    {
        if (!string.IsNullOrWhiteSpace(job.Source.Path))
            yield return Path.GetFullPath(job.Source.Path);
        foreach (var source in job.Sources)
        {
            if (!string.IsNullOrWhiteSpace(source.Path))
                yield return Path.GetFullPath(source.Path);
        }
    }
}

internal static class JobTransformPreflight
{
    public static void Check(JobDefinition job, string label, IReadOnlyDictionary<string, string> variables, IssueBag issues)
    {
        for (var i = 0; i < job.Transforms.Count; i++)
        {
            var step = job.Transforms[i];
            var prefix = $"{label}.transforms[{i}]";

            if (!string.IsNullOrWhiteSpace(step.Where))
            {
                try
                {
                    WhereExpression.Compile(step.Where, variables);
                }
                catch (InvalidOperationException ex)
                {
                    var code = ex.Message.StartsWith("Variable '@", StringComparison.Ordinal)
                        ? PreflightCodes.TransformWhereUndefinedVariable
                        : PreflightCodes.TransformWhereInvalid;
                    issues.Error(code, $"where 表达式在运行时将失败：{ex.Message}", $"{prefix}.where");
                }
            }

            if (step.Select is null)
                continue;

            foreach (var (target, sourceField) in step.Select)
            {
                if (!string.IsNullOrWhiteSpace(sourceField))
                    continue;
                issues.Warning(
                    PreflightCodes.TransformSelectSourceRequired,
                    $"select 映射 {target} →（空）将输出空值列：source 字段未指定。",
                    $"{prefix}.select.{target}");
            }
        }
    }
}

internal static class JobValidatePreflight
{
    public static void Check(JobDefinition job, string label, IssueBag issues)
    {
        var validate = job.Validate;
        if (validate is null)
            return;

        var prefix = $"{label}.validate";
        var onError = validate.OnError?.Trim().ToLowerInvariant() ?? "fail";
        if (onError is not ("fail" or "continue"))
            issues.Warning(
                PreflightCodes.ValidateOnUnknown,
                $"validate.onError '{validate.OnError}' 未被识别：运行时将按 fail（首个坏行中止）处理。",
                $"{prefix}.onError");

        if (validate.Rules.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(validate.BadRowOutput))
                issues.Warning(
                    PreflightCodes.ValidateBadRowWithoutRules,
                    "validate.badRowOutput 已配置但没有 validate.rules：运行时不会校验任何行，badRowOutput 不会生效。",
                    $"{prefix}.badRowOutput");
            return;
        }

        for (var i = 0; i < validate.Rules.Count; i++)
        {
            var rule = validate.Rules[i];
            var rulePath = $"{prefix}.rules[{i}]";

            if (string.IsNullOrWhiteSpace(rule.Field))
            {
                issues.Warning(
                    PreflightCodes.ValidateRuleFieldRequired,
                    $"rules[{i}].field 为空：运行时该规则会被静默跳过（JobRowValidator 跳过空 field），不会报错也不会校验。",
                    $"{rulePath}.field");
                continue;
            }

            if (rule.Required != true && rule.Min is null && rule.Max is null && string.IsNullOrWhiteSpace(rule.Pattern))
                issues.Warning(
                    PreflightCodes.ValidateRuleConstraintsMissing,
                    $"rules[{i}]（field='{rule.Field}'）没有任何约束（required/min/max/pattern）：该规则不会产生任何效果。",
                    rulePath);

            if (rule.Min is { } min && rule.Max is { } max && min > max)
                issues.Error(
                    PreflightCodes.ValidateRuleMinAboveMax,
                    $"rules[{i}]（field='{rule.Field}'）的 min（{min}）大于 max（{max}）：所有非空值都会校验失败。",
                    rulePath);

            if (!string.IsNullOrWhiteSpace(rule.Pattern))
            {
                try
                {
                    _ = new System.Text.RegularExpressions.Regex(
                        rule.Pattern,
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromSeconds(1));
                }
                catch (ArgumentException ex)
                {
                    issues.Error(
                        PreflightCodes.ValidateRulePatternInvalid,
                        $"rules[{i}]（field='{rule.Field}'）的 pattern 不是合法正则：{ex.Message}",
                        $"{rulePath}.pattern");
                }
            }
        }
    }
}

internal static class JobWatermarkPreflight
{
    public static void Check(JobDefinition job, string label, IssueBag issues)
    {
        var watermark = job.Watermark;
        if (watermark is null)
            return;

        var prefix = $"{label}.watermark";
        if (string.IsNullOrWhiteSpace(watermark.Column))
            issues.Error(PreflightCodes.WatermarkColumnRequired, "watermark.column 不能为空。", $"{prefix}.column");

        if (string.IsNullOrWhiteSpace(watermark.StatePath))
        {
            issues.Error(PreflightCodes.WatermarkStatePathRequired, "watermark.statePath 不能为空。", $"{prefix}.statePath");
            return;
        }

        var directory = Path.GetDirectoryName(watermark.StatePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            issues.Warning(
                PreflightCodes.WatermarkStateDirectoryMissing,
                $"watermark.statePath 的父目录不存在（运行时写状态文件会失败，除非执行前创建）：'{directory}'。预检不会创建任何目录。",
                $"{prefix}.statePath");
    }
}

internal static class JobSchedulePreflight
{
    public static void Check(JobDefinition job, string label, IssueBag issues)
    {
        if (string.IsNullOrWhiteSpace(job.Schedule))
            return;

        try
        {
            _ = JobScheduler.ParseSchedule(job.Schedule);
        }
        catch (Exception ex) when (ex is ArgumentException or Cronos.CronFormatException)
        {
            issues.Error(PreflightCodes.ScheduleInvalid, "schedule 不是合法的 cron 表达式。", $"{label}.schedule");
        }
    }
}

internal static class JobExecutionPreflight
{
    public static IReadOnlyList<PreflightIssue> Check(JobExecutionOptions? options, string path)
    {
        var bag = new IssueBag();
        if (options is null)
            return bag.Issues;

        if (options.Parallelism <= 0)
            bag.Error(PreflightCodes.ExecutionParallelismNonPositive, $"execution.parallelism 必须大于 0（当前 {options.Parallelism}）。", $"{path}.parallelism");
        if (options.BatchSize is <= 0)
            bag.Error(PreflightCodes.ExecutionBatchSizeNonPositive, $"execution.batchSize 必须大于 0（当前 {options.BatchSize}）。", $"{path}.batchSize");
        if (options.ProgressEvery < 0)
            bag.Error(PreflightCodes.ExecutionProgressEveryNegative, $"execution.progressEvery 不能为负（当前 {options.ProgressEvery}）。", $"{path}.progressEvery");

        return bag.Issues;
    }

    /// <summary>Mirrors JobRunner.ApplyExecutionHints so capability checks see the same effective sink options.</summary>
    public static void ApplyBatchSizeHint(JobDefinition job)
    {
        if (job.Execution?.BatchSize is int batchSize && batchSize > 0)
        {
            job.Sink.Options ??= new SinkOptionsDefinition();
            job.Sink.Options.BatchSize ??= batchSize;
        }
    }
}

internal static class JobPreflightFooter
{
    /// <summary>
    /// Appends the explicit "not verified" limitations so no consumer mistakes preflight for a full
    /// type / permission validation. Uses the <c>limitation.*</c> info codes.
    /// </summary>
    public static void Append(PreflightOptionsView view, IssueBag issues)
    {
        issues.Info(
            PreflightCodes.LimitationTypeMappingNotPerformed,
            "未做列级类型映射校验：预检不会比对来源与目标的列类型，仅对已知列做粗检（min/max/pattern 与列类型是否明显冲突）。",
            "limitations");
        issues.Info(
            PreflightCodes.LimitationPermissionsNotVerified,
            "未校验数据库权限：SELECT/INSERT/DDL 授权由数据库侧决定，预检不尝试执行任何 DML/DDL。",
            "limitations");
        issues.Info(
            PreflightCodes.LimitationFileContentNotRead,
            "不读取文件内容：csv/json/excel/parquet 仅做存在性检查，列名、编码、表头等文件内部问题无法在预检发现。",
            "limitations");
        issues.Info(
            PreflightCodes.SchemaNotChecked,
            view.CheckSchema
                ? "CheckSchema=true 时的表/列检查依赖已注册的 catalog provider；未注册的类型会降级为 warning（catalog.unavailable）。"
                : "未启用 CheckSchema：未检查连接可用性、表/列是否存在。需要时传入 CheckSchema=true（只读元数据查询）。",
            "limitations");
    }
}

/// <summary>Minimal view of the options needed by the footer (avoids coupling to the public options type).</summary>
internal readonly record struct PreflightOptionsView(bool CheckSchema);

internal static class JobEnvironmentPreflight
{
    public static void Check(JobDefinition job, string label, JobPreflightOptions options, IssueBag issues)
    {
        if (!options.UseEnvironmentVariables)
            return;

        Func<string, string?> environment = Environment.GetEnvironmentVariable;
        foreach (var (key, path) in CollectVariableKeys(job, label))
        {
            var value = environment(key);
            if (!string.IsNullOrEmpty(value))
                continue;
            if (options.Variables?.ContainsKey(key) == true)
                continue;
            issues.Warning(
                PreflightCodes.SourceVariableUnresolved,
                $"变量 ${{{key}}} 未在参数或环境变量中定义，运行时将保留字面量 ${{{key}}}，路径/连接/条件几乎必然失效。",
                path);
        }
    }

    private static IEnumerable<(string Key, string Path)> CollectVariableKeys(JobDefinition job, string label)
    {
        foreach (var (value, path) in FieldValues(job, label))
        {
            if (string.IsNullOrEmpty(value))
                continue;
            foreach (Match match in Regex.Matches(value, @"\$\{([^}]+)\}", RegexOptions.CultureInvariant))
                yield return (match.Groups[1].Value, path);
        }
    }

    private static IEnumerable<(string? Value, string Path)> FieldValues(JobDefinition job, string label)
    {
        yield return (job.Source.Path, $"{label}.source.path");
        yield return (job.Source.Connection, $"{label}.source.connection");
        yield return (job.Source.Table, $"{label}.source.table");
        yield return (job.Source.Query, $"{label}.source.query");
        yield return (job.Sink.Path, $"{label}.sink.path");
        yield return (job.Sink.Connection, $"{label}.sink.connection");
        yield return (job.Sink.Table, $"{label}.sink.table");

        for (var i = 0; i < job.Sources.Count; i++)
        {
            var join = job.Sources[i];
            yield return (join.Path, $"{label}.sources[{i}].path");
            yield return (join.Connection, $"{label}.sources[{i}].connection");
            yield return (join.Table, $"{label}.sources[{i}].table");
            yield return (join.Query, $"{label}.sources[{i}].query");
            yield return (join.On, $"{label}.sources[{i}].on");
        }

        for (var i = 0; i < job.Transforms.Count; i++)
        {
            if (job.Transforms[i].Where is { } where)
                yield return (where, $"{label}.transforms[{i}].where");
        }

        if (job.Watermark is { } watermark)
        {
            yield return (watermark.Column, $"{label}.watermark.column");
            yield return (watermark.StatePath, $"{label}.watermark.statePath");
        }

        if (job.Validate?.BadRowOutput is { } badRow)
            yield return (badRow, $"{label}.validate.badRowOutput");
    }
}
