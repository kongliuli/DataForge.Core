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
}
