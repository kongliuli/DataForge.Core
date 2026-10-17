using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataForge.Web;
using DataForge.Jobs.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DataForge.Web.Tests;

public class ScheduleRestartTests
{
    [Fact]
    public async Task FailedSave_DoesNotActivateOrRemoveRegistrations()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-store-failure-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "schedules.json");
        Directory.CreateDirectory(dir);
        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseSetting("Schedules:Path", path));
            using var client = factory.CreateClient();
            var store = factory.Services.GetRequiredService<PersistentSchedules>();
            var service = factory.Services.GetRequiredService<JobScheduleService>();
            const string yaml = "name: failure\nschedule: '0 0 1 1 *'\nsource:\n  type: csv\n  path: ./in.csv\nsink:\n  type: json\n  path: ./out.json";
            await store.AddAsync(new RegisterScheduleRequest(yaml, "existing", false, null), default);
            // A directory at the destination makes replacement fail on every platform.
            var backup = path + ".backup";
            File.Move(path, backup);
            Directory.CreateDirectory(path);
            try
            {
                var addError = await Record.ExceptionAsync(() => store.AddAsync(
                    new RegisterScheduleRequest(yaml, "rejected", true, null), default));
                Assert.True(addError is IOException or UnauthorizedAccessException, addError?.ToString());
                Assert.Null(service.Get("rejected"));
                var removeError = await Record.ExceptionAsync(() => store.RemoveAsync("existing", default));
                Assert.True(removeError is IOException or UnauthorizedAccessException, removeError?.ToString());
                Assert.NotNull(service.Get("existing"));
            }
            finally
            {
                Directory.Delete(path);
                File.Move(backup, path);
            }
            Assert.Equal("existing", Assert.Single(await ScheduleFileStore.LoadAsync(path)).Id);
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally { Directory.Delete(dir, true); }
    }

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
