using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DataForge.Web.Tests;

/// <summary>
/// <c>POST /api/preflight</c> — the full non-destructive preflight exposed over HTTP, including the
/// structured issue list and the explicit "checks not performed" footer.
/// </summary>
public class PreflightApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PreflightApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Preflight_ValidJob_ReturnsStructuredReportWithLimitations()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-preflight-api-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var csv = Path.Combine(dir, "in.csv");
        var json = Path.Combine(dir, "out.json");
        await File.WriteAllTextAsync(csv, "Id,Name\n1,Ada\n");

        try
        {
            var response = await _factory.CreateClient().PostAsJsonAsync("/api/preflight", new
            {
                yaml = $$"""
                    name: preflight-ok
                    source: { type: csv, path: {{csv.Replace('\\', '/')}} }
                    sink: { type: json, path: {{json.Replace('\\', '/')}} }
                    """
            });
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);

            using var doc = JsonDocument.Parse(body);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            var codes = doc.RootElement.GetProperty("issues").EnumerateArray()
                .Select(issue => issue.GetProperty("code").GetString())
                .ToArray();

            // The report must always state what preflight does not cover.
            Assert.Contains("limitation.typeMapping.notPerformed", codes);
            Assert.Contains("limitation.permissions.notVerified", codes);
            Assert.Contains("limitation.fileContent.notRead", codes);
            Assert.Contains("schema.notChecked", codes);
            Assert.Empty(doc.RootElement.GetProperty("errors").EnumerateArray());

            // Non-destructive over HTTP as well: preflight never writes the sink.
            Assert.False(File.Exists(json));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Preflight_MissingSinkPath_ReportsErrorAtItsPath()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/preflight", new
        {
            yaml = """
                source: { type: csv, path: ./in.csv }
                sink: { type: csv }
                """
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        var issue = doc.RootElement.GetProperty("errors").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "sink.path.required");
        Assert.Equal("job.sink.path", issue.GetProperty("path").GetString());
    }

    [Fact]
    public async Task Preflight_SubstitutesRequestVariables()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-preflight-var-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var csv = Path.Combine(dir, "in.csv");
        await File.WriteAllTextAsync(csv, "Id\n1\n");

        try
        {
            var response = await _factory.CreateClient().PostAsJsonAsync("/api/preflight", new
            {
                yaml = """
                    source: { type: csv, path: "${SRC}" }
                    sink: { type: json, path: "${DST}" }
                    """,
                variables = new Dictionary<string, string>
                {
                    ["SRC"] = csv.Replace('\\', '/'),
                    ["DST"] = Path.Combine(dir, "out.json").Replace('\\', '/')
                }
            });
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);

            using var doc = JsonDocument.Parse(body);
            var codes = doc.RootElement.GetProperty("issues").EnumerateArray()
                .Select(issue => issue.GetProperty("code").GetString())
                .ToArray();
            // The substituted source exists, so no missing-path warning may be reported for it.
            Assert.DoesNotContain("source.path.missing", codes);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("jobs: []")]
    [InlineData("source: { type: csv }")]
    [InlineData("secret: [sensitive-password")]
    public async Task Preflight_InvalidYaml_ReturnsBadRequestWithoutEchoingInput(string yaml)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/preflight", new { yaml });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("sensitive-password", await response.Content.ReadAsStringAsync());
    }
}
