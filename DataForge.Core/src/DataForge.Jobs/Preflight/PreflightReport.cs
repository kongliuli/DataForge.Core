using System.Text;

namespace DataForge.Jobs.Preflight;

/// <summary>
/// Severity of a <see cref="PreflightIssue"/>. <c>Error</c> means the job would fail at runtime with the
/// current adapters registered; <c>Warning</c> means a silent data or configuration risk; <c>Info</c>
/// carries advisory notes (for example "check not performed").
/// </summary>
public enum PreflightSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

/// <summary>
/// Machine-readable issue codes. Every <see cref="PreflightIssue"/> emitted by <see cref="JobPreflight"/>
/// uses one of these constants.
/// </summary>
public static class PreflightCodes
{
    public const string FileNotFound = "file.notFound";
    public const string YamlSyntax = "yaml.syntax";
    public const string YamlShape = "yaml.shape";

    public const string SourceTypeRequired = "source.type.required";
    public const string SourceTypeUnregistered = "source.type.unregistered";
    public const string SourceConnectionRequired = "source.connection.required";
    public const string SourceTableRequired = "source.table.required";
    public const string SourceQueryRequired = "source.query.required";
    public const string SourcePathRequired = "source.path.required";
    public const string SourceQueryIgnored = "source.query.ignored";
    public const string SourcePathMissing = "source.path.missing";
    public const string SourceUrlInvalid = "source.url.invalid";
    public const string SourceVariableUnresolved = "source.variable.unresolved";
    public const string SourceOptionIgnored = "source.option.ignored";
    public const string SourceConnectionFailed = "source.connection.failed";
    public const string SourceTableMissing = "source.table.missing";
    public const string SourceColumnMissing = "source.column.missing";
    public const string SourceConnectionSkipped = "source.connection.skipped";

    public const string JoinOnRequired = "join.on.required";
    public const string JoinOnInvalid = "join.on.invalid";
    public const string JoinPrimaryIgnored = "join.primaryIgnored";

    public const string SinkTypeRequired = "sink.type.required";
    public const string SinkTypeUnregistered = "sink.type.unregistered";
    public const string SinkConnectionRequired = "sink.connection.required";
    public const string SinkTableRequired = "sink.table.required";
    public const string SinkPathRequired = "sink.path.required";
    public const string SinkUrlInvalid = "sink.url.invalid";
    public const string SinkModeUnknown = "sink.mode.unknown";
    public const string SinkModeUnsupported = "sink.mode.unsupported";
    public const string SinkKeysRequired = "sink.keys.required";
    public const string SinkKeysIgnored = "sink.keys.ignored";
    public const string SinkKeysDuplicated = "sink.keys.duplicated";
    public const string SinkCreateTableUnsupported = "sink.createTable.unsupported";
    public const string SinkOptionIgnored = "sink.option.ignored";
    public const string SinkBatchSizeNonPositive = "sink.batchSize.nonPositive";
    public const string SinkTableMissing = "sink.table.missing";
    public const string SinkTableCreateHint = "sink.table.createHint";
    public const string SinkKeyMissing = "sink.key.missing";
    public const string SinkConnectionFailed = "sink.connection.failed";
    public const string SinkVariableUnresolved = "sink.variable.unresolved";

    public const string WatermarkColumnRequired = "watermark.column.required";
    public const string WatermarkStatePathRequired = "watermark.statePath.required";
    public const string WatermarkStateDirectoryMissing = "watermark.stateDirectory.missing";
    public const string WatermarkColumnMissing = "watermark.column.missing";

    public const string ValidateOnUnknown = "validate.onError.unknown";
    public const string ValidateRuleFieldRequired = "validate.rule.field.required";
    public const string ValidateRuleConstraintsMissing = "validate.rule.constraints.missing";
    public const string ValidateRuleMinAboveMax = "validate.rule.minAboveMax";
    public const string ValidateRulePatternInvalid = "validate.rule.pattern.invalid";
    public const string ValidateNoRules = "validate.noRules";
    public const string ValidateBadRowWithoutRules = "validate.badRowOutput.noRules";
    public const string ValidateFieldMissing = "validate.field.missing";

    public const string TransformWhereInvalid = "transform.where.invalid";
    public const string TransformWhereUndefinedVariable = "transform.where.undefinedVariable";
    public const string TransformSelectSourceRequired = "transform.select.source.required";

    public const string ExecutionParallelismNonPositive = "execution.parallelism.nonPositive";
    public const string ExecutionBatchSizeNonPositive = "execution.batchSize.nonPositive";
    public const string ExecutionProgressEveryNegative = "execution.progressEvery.negative";
    public const string ExecutionBatchSizeIgnored = "execution.batchSize.ignored";

    public const string ScheduleInvalid = "schedule.invalid";

    public const string CatalogUnavailable = "catalog.unavailable";
    public const string CatalogCheckFailed = "catalog.checkFailed";
    public const string SchemaNotChecked = "schema.notChecked";

    public const string LimitationTypeMappingNotPerformed = "limitation.typeMapping.notPerformed";
    public const string LimitationPermissionsNotVerified = "limitation.permissions.notVerified";
    public const string LimitationFileContentNotRead = "limitation.fileContent.notRead";
}

/// <summary>A single structured preflight finding.</summary>
public sealed class PreflightIssue
{
    public PreflightIssue(PreflightSeverity severity, string code, string message, string? path = null)
    {
        Severity = severity;
        Code = code;
        Message = message;
        Path = path;
    }

    public PreflightSeverity Severity { get; }

    /// <summary>Machine-readable code from <see cref="PreflightCodes"/>.</summary>
    public string Code { get; }

    /// <summary>Human-readable description (zh-CN).</summary>
    public string Message { get; }

    /// <summary>Configuration path such as <c>source.path</c> or <c>sources[1].on</c>.</summary>
    public string? Path { get; }

    public override string ToString() =>
        Path is null ? $"[{Severity}] {Code}: {Message}" : $"[{Severity}] {Code} ({Path}): {Message}";
}

/// <summary>Aggregate preflight result for one job file.</summary>
public sealed class PreflightReport
{
    public PreflightReport(string? jobName, string? jobFilePath, IReadOnlyList<PreflightIssue> issues)
    {
        JobName = jobName;
        JobFilePath = jobFilePath;
        Issues = issues;
    }

    public string? JobName { get; }

    public string? JobFilePath { get; }

    public IReadOnlyList<PreflightIssue> Issues { get; }

    public IReadOnlyList<PreflightIssue> Errors =>
        Issues.Where(i => i.Severity == PreflightSeverity.Error).ToArray();

    public IReadOnlyList<PreflightIssue> Warnings =>
        Issues.Where(i => i.Severity == PreflightSeverity.Warning).ToArray();

    /// <summary>true when no <see cref="PreflightSeverity.Error"/> issue exists.</summary>
    public bool Success => Issues.All(i => i.Severity != PreflightSeverity.Error);

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("preflight ").Append(Success ? "passed" : "failed");
        sb.Append(" (errors=").Append(Errors.Count)
          .Append(", warnings=").Append(Warnings.Count)
          .Append(", info=").Append(Issues.Count - Errors.Count - Warnings.Count).Append(')');
        foreach (var issue in Issues)
            sb.AppendLine().Append("  ").Append(issue);
        return sb.ToString();
    }
}
