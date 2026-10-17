using System.Text.Json;
using DataForge.Jobs.Execution;
using DataForge.Web;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/jobs/run", async (RunRequest request, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
        return Results.BadRequest(new { error = "yaml is required" });

    try
    {
        var results = await JobWebRunner.RunYamlAsync(request.Yaml, request.Variables, cancellationToken);
        return Results.Ok(results.Select(ToDto));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

static object ToDto(JobRunResult result) => new
{
    result.Success,
    result.JobName,
    result.JobFilePath,
    result.ErrorMessage,
    result.StartedAt,
    result.FinishedAt,
    RecordsWritten = result.ExportResults?.RecordsWritten,
    OutputPath = result.ExportResults?.OutputPath
};

public sealed record RunRequest(string? Yaml, Dictionary<string, string>? Variables);

public partial class Program;
