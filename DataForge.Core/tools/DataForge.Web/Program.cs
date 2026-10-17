using DataForge.Jobs.Execution;
using DataForge.Web;

var builder = WebApplication.CreateBuilder(args);
JobProviders.RegisterAll();
builder.Services.AddSingleton<JobRunHistory>();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

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
