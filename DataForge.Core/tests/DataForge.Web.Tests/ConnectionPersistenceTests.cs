using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DataForge.Web.Tests;

public class ConnectionPersistenceTests
{
    [Fact]
    public async Task ConfiguredPath_PersistsConnectionsAcrossHostRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "df-connections-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "profiles", "connections.json");
        WebApplicationFactory<Program> CreateHost() => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseSetting("Connections:Path", path)
                .UseSetting("Schedules:Path", Path.Combine(directory, "schedules.json")));
        try
        {
            await using (var host = CreateHost())
            {
                var response = await host.CreateClient().PostAsJsonAsync("/api/connections", new
                {
                    id = "test", name = "isolated", type = "sqlite", connectionString = "Data Source=test.db"
                });
                response.EnsureSuccessStatusCode();
                Assert.True(File.Exists(path));
            }
            await using (var host = CreateHost())
            {
                var profiles = await host.CreateClient().GetFromJsonAsync<JsonElement>("/api/connections");
                Assert.Contains(profiles.EnumerateArray(), profile => profile.GetProperty("id").GetString() == "test");
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
