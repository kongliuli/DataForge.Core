using System.Text.Json;
using DataForge.Jobs.Catalog;
using DataForge.Jobs.Execution;
using DataForge.Jobs.Parsing;
using DataForge.Jobs.Preflight;
using DataForge.Jobs.Scheduling;
using DataForge.Web;

// A dotnet tool can be started from any working directory, so serve the UI from the
// directory that actually holds wwwroot: the current directory while developing, and the
// tool's own install directory (`dotnet tool install`) otherwise.
var contentRoot = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"))
    ? Directory.GetCurrentDirectory()
    : AppContext.BaseDirectory;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = contentRoot });

// Content root and data root differ only for an installed tool: the tool store should be
// treated as read-only, so mutable state defaults to the user profile.
if (!string.Equals(contentRoot, Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase))
{
    var dataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DataForge.Web");
    builder.Configuration["Connections:Path"] ??= Path.Combine(dataRoot, "connections.json");
    builder.Configuration["Schedules:Path"] ??= Path.Combine(dataRoot, "schedules.json");
}
JobProviders.RegisterAll();
builder.Services.AddSingleton<JobRunHistory>();
builder.Services.AddSingleton<ConnectionStore>();
builder.Services.AddSingleton<JobScheduleService>();
builder.Services.AddSingleton<PersistentSchedules>();
builder.Services.AddHostedService(services => services.GetRequiredService<PersistentSchedules>());
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/catalog/types", () => JobCatalog.RegisteredTypes);

app.MapGet("/api/connections", (ConnectionStore store) => store.List());

app.MapPost("/api/connections", (ConnectionProfile profile, ConnectionStore store) =>
{
    try
    {
        return Results.Ok(store.Upsert(profile));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapDelete("/api/connections/{id}", (string id, ConnectionStore store) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound());

app.MapPost("/api/catalog/test", async (CatalogRequest request, ConnectionStore store, CancellationToken cancellationToken) =>
{
    var resolved = Resolve(request, store);
    if (resolved.Error is not null)
        return Results.BadRequest(new { error = resolved.Error });

    var result = await JobCatalog.TestConnectionAsync(resolved.Type!, resolved.ConnectionString!, cancellationToken);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/catalog/tables", async (CatalogRequest request, ConnectionStore store, CancellationToken cancellationToken) =>
{
    var resolved = Resolve(request, store);
    if (resolved.Error is not null)
        return Results.BadRequest(new { error = resolved.Error });

    try
    {
        var tables = await JobCatalog.ListTablesAsync(resolved.Type!, resolved.ConnectionString!, cancellationToken);
        return Results.Ok(tables);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/catalog/describe", async (CatalogRequest request, ConnectionStore store, CancellationToken cancellationToken) =>
{
    var resolved = Resolve(request, store);
    if (resolved.Error is not null)
        return Results.BadRequest(new { error = resolved.Error });
    if (string.IsNullOrWhiteSpace(request.Table))
        return Results.BadRequest(new { error = "table is required" });

    try
    {
        var columns = await JobCatalog.DescribeTableAsync(resolved.Type!, resolved.ConnectionString!, request.Table, cancellationToken);
        return Results.Ok(columns);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/catalog/preview", async (CatalogRequest request, ConnectionStore store, CancellationToken cancellationToken) =>
{
    var resolved = Resolve(request, store);
    if (resolved.Error is not null)
        return Results.BadRequest(new { error = resolved.Error });
    if (string.IsNullOrWhiteSpace(request.Table))
        return Results.BadRequest(new { error = "table is required" });

    try
    {
        var rows = await JobCatalog.PreviewAsync(resolved.Type!, resolved.ConnectionString!, request.Table, request.Limit ?? 20, cancellationToken);
        return Results.Ok(rows);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/jobs/history", (JobRunHistory history) =>
    history.List().Select(entry => new
    {
        entry.Id,
        entry.At,
        entry.Success,
        entry.Yaml,
        entry.Results
    }));

app.MapGet("/api/schedules", (JobScheduleService schedules) => schedules.List());

app.MapPost("/api/schedules", async (RegisterScheduleRequest request, PersistentSchedules schedules, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
        return Results.BadRequest(new { error = "yaml is required" });

    try
    {
        var status = await schedules.AddAsync(request, token);

        return Results.Created($"/api/schedules/{status.Id}", status);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/schedules/{id}/run", async (string id, JobScheduleService schedules, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await schedules.RunNowAsync(id, cancellationToken);
        return Results.Ok(ToDto(result));
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapDelete("/api/schedules/{id}", async (string id, PersistentSchedules schedules, CancellationToken token) =>
    await schedules.RemoveAsync(id, token) ? Results.NoContent() : Results.NotFound());

app.MapPost("/api/jobs/schedule", (ScheduleRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
        return Results.BadRequest(new { error = "yaml is required" });

    try
    {
        var job = JobYamlParser.Parse(request.Yaml);
        if (string.IsNullOrWhiteSpace(job.Schedule))
            return Results.Ok(new { schedule = (string?)null, next = (DateTimeOffset?)null });

        var scheduler = new JobScheduler();
        var next = scheduler.GetNextOccurrence(job.Schedule, DateTimeOffset.UtcNow);
        return Results.Ok(new { schedule = job.Schedule, next });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/jobs/validate", (RunRequest request) =>
{
    var report = JobConfigurationValidator.Validate(request.Yaml);
    return report.Valid ? Results.Ok(report) : Results.BadRequest(report);
});

// Full preflight: same engine as the CLI/library, including the structured issue list and the
// explicit "checks not performed" footer. Read-only: no DDL/DML, no output file, no watermark write.
app.MapPost("/api/preflight", async (PreflightRequest request, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
        return Results.BadRequest(new { error = "yaml is required" });

    var report = await JobPreflight.CheckYamlAsync(
        request.Yaml,
        "job.yaml",
        new JobPreflightOptions
        {
            Variables = request.Variables,
            CheckSchema = request.CheckSchema == true
        },
        cancellationToken);

    return report.Success ? Results.Ok(report) : Results.BadRequest(report);
});

app.MapPost("/api/jobs/run", async (RunRequest request, JobRunHistory history, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
        return Results.BadRequest(new { error = "yaml is required" });

    try
    {
        var results = await JobWebRunner.RunYamlAsync(request.Yaml, request.Variables, cancellationToken);
        var dto = results.Select(ToDto).ToArray();
        var success = dto.All(item => item.Success);
        history.Add(request.Yaml, dto, success);
        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        history.Add(request.Yaml, [new { success = false, error = ex.Message }], success: false);
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/jobs/run/stream", async (HttpContext http, RunRequest request, JobRunHistory history, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        await http.Response.WriteAsJsonAsync(new { error = "yaml is required" }, cancellationToken);
        return;
    }

    http.Response.Headers.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";
    var progress = new QueuedProgress<JobProgress>();

    async Task FlushAsync()
    {
        while (progress.TryDequeue(out var item))
            await WriteEventAsync(http, "progress", item, cancellationToken);
    }

    try
    {
        var run = JobWebRunner.RunYamlAsync(request.Yaml, request.Variables, cancellationToken, progress);
        while (!run.IsCompleted)
        {
            await Task.WhenAny(run, Task.Delay(100, cancellationToken));
            await FlushAsync();
        }

        var results = await run;
        await FlushAsync();
        var dto = results.Select(ToDto).ToArray();
        var success = dto.All(item => item.Success);
        history.Add(request.Yaml, dto, success);
        await WriteEventAsync(http, "result", dto, cancellationToken);
    }
    catch (Exception ex)
    {
        history.Add(request.Yaml, [new { success = false, error = ex.Message }], success: false);
        await WriteEventAsync(http, "error", new { error = ex.Message }, cancellationToken);
    }
});

app.Run();

static ResolvedConnection Resolve(CatalogRequest request, ConnectionStore store)
{
    if (!string.IsNullOrWhiteSpace(request.ConnectionId))
    {
        if (!store.TryGet(request.ConnectionId, out var profile))
            return new ResolvedConnection(null, null, $"connection '{request.ConnectionId}' not found");
        return new ResolvedConnection(profile.Type, profile.ConnectionString, null);
    }

    if (string.IsNullOrWhiteSpace(request.Type))
        return new ResolvedConnection(null, null, "type is required");
    if (string.IsNullOrWhiteSpace(request.ConnectionString))
        return new ResolvedConnection(null, null, "connectionString is required");
    return new ResolvedConnection(request.Type, request.ConnectionString, null);
}

static async Task WriteEventAsync(HttpContext http, string name, object payload, CancellationToken cancellationToken)
{
    var json = JsonSerializer.Serialize(payload);
    await http.Response.WriteAsync($"event: {name}\ndata: {json}\n\n", cancellationToken);
    await http.Response.Body.FlushAsync(cancellationToken);
}

static JobResultDto ToDto(JobRunResult result) => new(
    result.Success,
    result.JobName,
    result.JobFilePath,
    result.ErrorMessage,
    result.StartedAt,
    result.FinishedAt,
    result.ExportResults?.RecordsWritten,
    result.ExportResults?.OutputPath);

public sealed record RunRequest(string? Yaml, Dictionary<string, string>? Variables);

public sealed record PreflightRequest(string? Yaml, Dictionary<string, string>? Variables, bool? CheckSchema);

public sealed record ScheduleRequest(string? Yaml);

public sealed record RegisterScheduleRequest(
    string? Yaml,
    string? Id,
    bool? RunOnStart,
    Dictionary<string, string>? Variables);

public sealed record CatalogRequest(string? Type, string? ConnectionString, string? ConnectionId, string? Table, int? Limit);

public sealed record ResolvedConnection(string? Type, string? ConnectionString, string? Error);

public sealed record JobResultDto(
    bool Success,
    string? JobName,
    string? JobFilePath,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int? RecordsWritten,
    string? OutputPath);

public partial class Program;
