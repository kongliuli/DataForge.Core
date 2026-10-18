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
    public async Task Validate_ReportsBatchCountWithoutExecuting()
    {
        var client = _factory.CreateClient();
        var yaml = """
            jobs:
              - source: { type: csv, path: missing.csv }
                sink: { type: json, path: unused.json }
              - source: { type: csv, path: missing2.csv }
                sink: { type: json, path: unused2.json }
            """;
        var response = await client.PostAsJsonAsync("/api/jobs/validate", new { yaml });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("valid").GetBoolean());
        Assert.Equal(2, result.GetProperty("jobCount").GetInt32());
        Assert.Equal("configuration-only", result.GetProperty("scope").GetString());
        Assert.Contains("connectivity", result.GetProperty("checksNotPerformed").EnumerateArray().Select(x => x.GetString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("jobs: []")]
    [InlineData("source: { type: csv }")]
    [InlineData("secret: [sensitive-password")]
    public async Task Validate_RejectsInvalidYamlWithoutEchoingInput(string yaml)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/jobs/validate", new { yaml });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("sensitive-password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResidentSchedule_RegisterRunAndRemove()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-resident-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var csv = Path.Combine(dir, "in.csv");
        var json = Path.Combine(dir, "out.json");
        var id = Guid.NewGuid().ToString("N");
        var client = _factory.CreateClient();
        try
        {
            await File.WriteAllTextAsync(csv, "Id,Name\n1,Ada\n");
            var yaml = $"""
                name: resident
                schedule: "0 0 1 1 *"
                source:
                  type: csv
                  path: {csv.Replace('\\', '/')}
                sink:
                  type: json
                  path: {json.Replace('\\', '/')}
                """;
            var registered = await client.PostAsJsonAsync("/api/schedules", new { id, yaml });
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
            var run = await client.PostAsync($"/api/schedules/{id}/run", null);
            run.EnsureSuccessStatusCode();
            Assert.True((await run.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
            Assert.Contains("Ada", await File.ReadAllTextAsync(json));
            var statuses = await client.GetFromJsonAsync<JsonElement>("/api/schedules");
            var status = statuses.EnumerateArray().Single(x => x.GetProperty("id").GetString() == id);
            Assert.Equal(1, status.GetProperty("runCount").GetInt32());
            Assert.True(status.GetProperty("lastSuccess").GetBoolean());
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/schedules/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/schedules/{id}/run", null)).StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"/api/schedules/{id}");
            Directory.Delete(dir, true);
        }
    }

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

    [Fact]
    public async Task Run_ExcelRoundTrip_AndHistory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-web-xlsx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var source = Path.Combine(dir, "in.xlsx");
        var target = Path.Combine(dir, "out.xlsx");

        using (var book = new ClosedXML.Excel.XLWorkbook())
        {
            var sheet = book.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "Id";
            sheet.Cell(1, 2).Value = "Name";
            sheet.Cell(2, 1).Value = "1";
            sheet.Cell(2, 2).Value = "Ada";
            book.SaveAs(source);
        }

        var yaml = $"""
            name: excel-copy
            source:
              type: excel
              path: {source.Replace('\\', '/')}
            sink:
              type: excel
              path: {target.Replace('\\', '/')}
            """;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/jobs/run", new { yaml });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.True(File.Exists(target));

        var history = await client.GetFromJsonAsync<JsonElement>("/api/jobs/history");
        Assert.True(history.GetArrayLength() >= 1);
        Assert.True(history[0].GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task RunStream_EmitsProgressAndResult()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-sse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var csv = Path.Combine(dir, "in.csv");
        var json = Path.Combine(dir, "out.json");
        await File.WriteAllTextAsync(csv, "Id,Name\n1,sse\n");
        var yaml = $"""
            name: web-sse
            execution:
              progressEvery: 1
            source:
              type: csv
              path: {csv.Replace('\\', '/')}
            sink:
              type: json
              path: {json.Replace('\\', '/')}
            """;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/jobs/run/stream", new { yaml });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("event: progress", body);
        Assert.Contains("event: result", body);
        Assert.Contains("web-sse", body);
    }

    [Fact]
    public async Task Schedule_ReturnsNextOccurrence()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/jobs/schedule", new
        {
            yaml = """
                name: sched
                schedule: "0 2 * * *"
                source:
                  type: csv
                  path: ./in.csv
                sink:
                  type: json
                  path: ./out.json
                """
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("0 2 * * *", body);
        Assert.Contains("next", body);
    }

    [Fact]
    public async Task Schedule_NoScheduleReturnsNullNext()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/jobs/schedule", new
        {
            yaml = """
                name: no-sched
                source:
                  type: csv
                  path: ./in.csv
                sink:
                  type: json
                  path: ./out.json
                """
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("\"next\":null", body.Replace(" ", ""));
    }

    [Fact]
    public async Task Run_SubstitutesRequestVariables()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-var-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var csv = Path.Combine(dir, "in.csv");
        var json = Path.Combine(dir, "out.json");
        await File.WriteAllTextAsync(csv, "Id,Name\n1,var\n");
        var yaml = """
            name: web-vars
            source:
              type: csv
              path: ${SRC}
            sink:
              type: json
              path: ${DST}
            """;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/jobs/run", new
        {
            yaml,
            variables = new Dictionary<string, string>
            {
                ["SRC"] = csv.Replace('\\', '/'),
                ["DST"] = json.Replace('\\', '/')
            }
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("var", await File.ReadAllTextAsync(json));
    }
}
