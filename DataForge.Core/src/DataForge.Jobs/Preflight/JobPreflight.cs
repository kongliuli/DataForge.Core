using DataForge.Jobs.Execution;
using DataForge.Jobs.Models;
using DataForge.Jobs.Parsing;
using DataForge.Jobs.Preflight.Internal;

namespace DataForge.Jobs.Preflight;

/// <summary>
/// Non-destructive job preflight: validates a job file (or an in-memory <see cref="JobDefinition"/>) against
/// the configuration rules and the adapters currently registered in <c>DataForge.Jobs.Providers.JobProviderRegistry</c>,
/// and returns a structured <see cref="PreflightReport"/> of errors, warnings and info notes.
/// </summary>
/// <remarks>
/// Preflight never reads data, never writes to the sink, never executes DDL and never touches the watermark
/// state file. It catches the failures that would otherwise surface only at run time (missing required
/// configuration, unregistered source/sink types, invalid <c>where</c> expressions, invalid cron expressions,
/// unsupported sink modes) plus silent misconfiguration (options a sink ignores, upsert on a sink that cannot
/// upsert).
/// <para>
/// Scope limits, reported explicitly in every report: column <em>types</em> are not verified, table/permission
/// existence is only checked when <see cref="JobPreflightOptions.CheckSchema"/> is set and a catalog provider is
/// registered, and file contents (csv/json/excel/parquet) are never opened — so column-level problems inside
/// them cannot be detected. See <see cref="Limitations"/>.
/// </para>
/// </remarks>
public static class JobPreflight
{
    /// <summary>Explicitly documented checks that preflight does not perform.</summary>
    public static IReadOnlyList<string> Limitations { get; } =
    [
        "不校验列的数据类型与源/目标类型兼容性（不做类型映射检查）。",
        "不检查数据库权限（SELECT/INSERT/DDL 授权）是否具备。",
        "不读取文件内容（csv/json/excel/parquet），因此文件内部列名/列类型/编码错误无法发现。",
        "不实际执行 where 表达式或验证表达式引用的列是否存在（除非 CheckSchema 且已注册 catalog provider）。",
        "不校验 join 的列语义，只校验 on 的格式（left=right）。",
        "不执行任何 DDL/DML：不建表、不写文件、不写 watermark 状态文件。",
        "CheckSchema 下的表/列存在性为粗检，依赖已注册的 catalog provider；未注册时降级为警告。"
    ];

    /// <summary>Check a job file on disk (single job or <c>jobs:</c> batch).</summary>
    public static async Task<PreflightReport> CheckAsync(
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        bool checkSchema = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobFilePath);
        var options = new JobPreflightOptions
        {
            Variables = variables,
            CheckSchema = checkSchema
        };
        return await CheckAsync(jobFilePath, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Check a job file on disk (single job or <c>jobs:</c> batch).</summary>
    public static async Task<PreflightReport> CheckAsync(
        string jobFilePath,
        JobPreflightOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobFilePath);
        options ??= new JobPreflightOptions();

        var fullPath = TryGetFullPath(jobFilePath);
        var issues = new IssueBag();
        if (!File.Exists(jobFilePath))
        {
            issues.Error(PreflightCodes.FileNotFound, $"作业文件不存在：{fullPath}", "jobFilePath");
            return Report(options.JobName, fullPath, issues, options);
        }

        string yaml;
        try
        {
            yaml = await File.ReadAllTextAsync(jobFilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Error(PreflightCodes.FileNotFound, $"作业文件无法读取：{ex.Message}", "jobFilePath");
            return Report(options.JobName, fullPath, issues, options);
        }

        return await CheckYamlAsync(yaml, fullPath, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Check YAML text. <paramref name="jobFilePath"/> only supplies name and relative-path resolution.</summary>
    public static async Task<PreflightReport> CheckYamlAsync(
        string yaml,
        string jobFilePath = "job.yaml",
        JobPreflightOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        options ??= new JobPreflightOptions();

        var fullPath = TryGetFullPath(jobFilePath);
        var issues = new IssueBag();
        JobFile file;
        try
        {
            file = JobYamlParser.ParseForPreflight(yaml);
        }
        catch (Exception)
        {
            issues.Error(PreflightCodes.YamlSyntax, "YAML 语法或文档结构无效。", "yaml");
            return Report(options.JobName, fullPath, issues, options);
        }

        if (file.Jobs.Count == 0)
        {
            issues.Error(PreflightCodes.YamlShape, "作业文件未定义任何 job。", "jobs");
            return Report(options.JobName, fullPath, issues, options);
        }

        var jobDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var targets = new List<PreflightTarget>(file.Jobs.Count);
        foreach (var (target, index) in file.Jobs.Select((j, i) => (j, i)))
        {
            var label = file.Jobs.Count > 1 ? $"jobs[{index}]" : "job";
            cancellationToken.ThrowIfCancellationRequested();
            targets.Add(new PreflightTarget(label, target, jobDirectory));
        }

        foreach (var issue in JobExecutionPreflight.Check(file.Execution, "execution"))
            issues.Add(issue);

        await CheckCoreAsync(targets, options, issues, cancellationToken).ConfigureAwait(false);
        return Report(options.JobName, fullPath, issues, options);
    }

    /// <summary>Check an already-parsed job. The instance is deep-copied first, so the caller's object is never mutated.</summary>
    public static async Task<PreflightReport> CheckAsync(
        JobDefinition job,
        string jobFilePath = "job.yaml",
        JobPreflightOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        options ??= new JobPreflightOptions();

        var fullPath = TryGetFullPath(jobFilePath);
        var jobDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var issues = new IssueBag();
        await CheckCoreAsync([new PreflightTarget("job", job, jobDirectory)], options, issues, cancellationToken)
            .ConfigureAwait(false);
        return Report(options.JobName, fullPath, issues, options);
    }

    /// <summary>Synchronous configuration-only check of an already-parsed job. No catalog access is performed.</summary>
    public static PreflightReport Check(
        JobDefinition job,
        string jobFilePath = "job.yaml",
        IReadOnlyDictionary<string, string>? variables = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        var options = new JobPreflightOptions { Variables = variables, CheckSchema = false };
        return CheckAsync(job, jobFilePath, options, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static async Task CheckCoreAsync(
        IReadOnlyList<PreflightTarget> targets,
        JobPreflightOptions options,
        IssueBag issues,
        CancellationToken cancellationToken)
    {
        var resolved = new List<ResolvedTarget>(targets.Count);

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clone = target.Job.DeepClone();
            var vars = VariableResolver.MergeVariables(options.Variables ?? new Dictionary<string, string>());
            VariableResolver.Apply(clone, vars);
            ResolvePaths(clone, target.JobDirectory);
            JobExecutionPreflight.ApplyBatchSizeHint(clone);

            JobSourcePreflight.Check(clone, target.Label, options, issues);
            JobSinkPreflight.Check(clone, target.Label, options, issues);
            JobTransformPreflight.Check(clone, target.Label, vars, issues);
            JobValidatePreflight.Check(clone, target.Label, issues);
            JobWatermarkPreflight.Check(clone, target.Label, issues);
            JobSchedulePreflight.Check(clone, target.Label, issues);
            JobEnvironmentPreflight.Check(clone, target.Label, options, issues);

            resolved.Add(new ResolvedTarget(target.Label, clone, target.JobDirectory));
        }

        if (!options.CheckSchema)
            return;

        foreach (var target in resolved)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await JobSchemaPreflight.CheckAsync(target, options, issues, cancellationToken).ConfigureAwait(false);
        }
    }

    private static PreflightReport Report(string? jobName, string jobFilePath, IssueBag issues, JobPreflightOptions options)
    {
        JobPreflightFooter.Append(new PreflightOptionsView(options.CheckSchema), issues);
        return new PreflightReport(jobName, jobFilePath, issues.Issues.ToArray());
    }

    private static string TryGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private static void ResolvePaths(JobDefinition job, string jobDirectory)
    {
        if (!string.IsNullOrWhiteSpace(job.Source.Path))
            job.Source.Path = VariableResolver.ResolvePath(job.Source.Path, jobDirectory);

        foreach (var source in job.Sources)
        {
            if (!string.IsNullOrWhiteSpace(source.Path))
                source.Path = VariableResolver.ResolvePath(source.Path, jobDirectory);
        }

        if (!string.IsNullOrWhiteSpace(job.Sink.Path))
            job.Sink.Path = VariableResolver.ResolvePath(job.Sink.Path, jobDirectory);

        if (job.Validate?.BadRowOutput != null)
            job.Validate.BadRowOutput = VariableResolver.ResolvePath(job.Validate.BadRowOutput, jobDirectory);

        if (!string.IsNullOrWhiteSpace(job.Watermark?.StatePath))
            job.Watermark.StatePath = VariableResolver.ResolvePath(job.Watermark.StatePath, jobDirectory);
    }
}

/// <summary>Options for a preflight run.</summary>
public sealed class JobPreflightOptions
{
    /// <summary>Variables applied with the same rules as the runner (<c>${NAME}</c>).</summary>
    public IReadOnlyDictionary<string, string>? Variables { get; init; }

    /// <summary>Override the reported job name.</summary>
    public string? JobName { get; init; }

    /// <summary>
    /// When true, additionally contact the catalog providers of relational sources/sinks to check connectivity,
    /// table existence and (as a coarse hint) column existence. Requires the matching
    /// <c>AddDataForgeJobs*</c> registration; a missing catalog provider is reported as a warning, not an error.
    /// </summary>
    public bool CheckSchema { get; init; }

    /// <summary>Per-connection timeout used by the optional schema/connectivity checks.</summary>
    public TimeSpan CheckTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Resolve <c>${NAME}</c> from process environment variables before validating.</summary>
    public bool UseEnvironmentVariables { get; init; } = true;
}

internal readonly record struct PreflightTarget(string Label, JobDefinition Job, string JobDirectory);

internal readonly record struct ResolvedTarget(string Label, JobDefinition Job, string JobDirectory);

internal sealed class IssueBag
{
    private readonly List<PreflightIssue> _issues = [];

    public IReadOnlyList<PreflightIssue> Issues => _issues;

    public void Add(PreflightIssue issue) => _issues.Add(issue);

    public void Error(string code, string message, string? path = null) =>
        _issues.Add(new PreflightIssue(PreflightSeverity.Error, code, message, path));

    public void Warning(string code, string message, string? path = null) =>
        _issues.Add(new PreflightIssue(PreflightSeverity.Warning, code, message, path));

    public void Info(string code, string message, string? path = null) =>
        _issues.Add(new PreflightIssue(PreflightSeverity.Info, code, message, path));
}
