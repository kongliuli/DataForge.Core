using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DataForge.Web.Tests;

public class JobApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public JobApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Run_CsvToJson_WritesRecords()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-web-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var csv = Path.Combine(dir, "in.csv");
        var json = Path.Combine(dir, "out.json");
        await File.WriteAllTextAsync(csv, "Id,Name\n1,Ada\n2,Lin\n");

        var yaml = $"""
            name: web-copy
            source:
              type: csv
              path: {csv.Replace('\\', '/')}
            sink:
              type: json
              path: {json.Replace('\\', '/')}
            """;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/jobs/run", new { yaml });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement[0];
        Assert.True(first.GetProperty("success").GetBoolean());
        Assert.Equal(2, first.GetProperty("recordsWritten").GetInt32());
        Assert.Contains("Ada", await File.ReadAllTextAsync(json));
    }

    [Fact]
    public async Task Run_EmptyYaml_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/jobs/run", new { yaml = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
