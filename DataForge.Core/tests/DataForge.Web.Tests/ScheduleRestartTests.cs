using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DataForge.Web.Tests;

public class ScheduleRestartTests
{
    [Fact]
    public async Task RegistrationAndDeletionSurviveHostRestart()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-restart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "schedules.json");
        WebApplicationFactory<Program> CreateHost() => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Schedules:Path", path));
        try
        {
            await using (var first = CreateHost())
            {
                var response = await first.CreateClient().PostAsJsonAsync("/api/schedules", new
                {
                    id = "restart",
                    yaml = "name: restart\nschedule: '0 0 1 1 *'\nsource:\n  type: csv\n  path: ./in.csv\nsink:\n  type: json\n  path: ./out.json"
                });
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                Assert.True(File.Exists(path));
            }
            await using (var second = CreateHost())
            {
                var client = second.CreateClient();
                var entries = await client.GetFromJsonAsync<JsonElement>("/api/schedules");
                Assert.Equal("restart", entries[0].GetProperty("id").GetString());
                Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/schedules/restart")).StatusCode);
            }
            await using (var third = CreateHost())
            {
                var entries = await third.CreateClient().GetFromJsonAsync<JsonElement>("/api/schedules");
                Assert.Equal(0, entries.GetArrayLength());
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
