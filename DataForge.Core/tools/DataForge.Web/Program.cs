using DataForge.Jobs.Catalog;
using DataForge.Jobs.Execution;
using DataForge.Web;

var builder = WebApplication.CreateBuilder(args);
JobProviders.RegisterAll();
builder.Services.AddSingleton<JobRunHistory>();
builder.Services.AddSingleton<ConnectionStore>();
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
