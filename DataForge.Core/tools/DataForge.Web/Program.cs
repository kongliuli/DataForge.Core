using System.Text.Json;
using DataForge.Jobs.Catalog;
using DataForge.Jobs.Execution;
using DataForge.Jobs.Parsing;
using DataForge.Jobs.Scheduling;
using DataForge.Web;

var builder = WebApplication.CreateBuilder(args);
JobProviders.RegisterAll();
builder.Services.AddSingleton<JobRunHistory>();
builder.Services.AddSingleton<ConnectionStore>();
builder.Services.AddSingleton<JobScheduleService>();
var app = builder.Build();

// Resident scheduler: schedules registered via /api/schedules run inside this process.
app.Services.GetRequiredService<JobScheduleService>().Start();

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

app.MapPost("/api/schedules", (RegisterScheduleRequest request, JobScheduleService schedules) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
        return Results.BadRequest(new { error = "yaml is required" });

    try
    {
        var status = schedules.Add(
            request.Yaml,
            request.Id,
            variables: request.Variables,
            runOnStart: request.RunOnStart == true);

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

app.MapDelete("/api/schedules/{id}", (string id, JobScheduleService schedules) =>
    schedules.Remove(id) ? Results.NoContent() : Results.NotFound());

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
    var events = new List<JobProgress>();
    var progress = new Progress<JobProgress>(item => events.Add(item));

    async Task FlushAsync()
    {
        while (events.Count > 0)
        {
            var batch = events.ToArray();
            events.Clear();
            foreach (var item in batch)
                await WriteEventAsync(http, "progress", item, cancellationToken);
        }
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
