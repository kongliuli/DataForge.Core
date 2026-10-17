using System.Text.Json;
using DataForge.Jobs.Scheduling;
using Xunit;

namespace DataForge.Sync.Tests;

public class SchedulePersistenceTests
{
    [Fact]
    public async Task RepeatedRuns_ResolveEnvironmentFromOriginalYaml()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var key = "DF_SCHEDULE_TEST_" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "a.csv"), "Id,Name\n1,First\n");
            await File.WriteAllTextAsync(Path.Combine(dir, "b.csv"), "Id,Name\n2,Second\n");
            var yaml = "name: repeat\nschedule: '0 0 1 1 *'\nsource:\n  type: csv\n  path: ${" + key + "}\nsink:\n  type: json\n  path: ./out.json\n";
            await using var service = new JobScheduleService();
            service.Add(yaml, "repeat", Path.Combine(dir, "job.yaml"));
            Environment.SetEnvironmentVariable(key, "./a.csv");
            Assert.True((await service.RunNowAsync("repeat")).Success);
            Assert.Contains("First", await File.ReadAllTextAsync(Path.Combine(dir, "out.json")));
            Environment.SetEnvironmentVariable(key, "./b.csv");
            Assert.True((await service.RunNowAsync("repeat")).Success);
            Assert.Contains("Second", await File.ReadAllTextAsync(Path.Combine(dir, "out.json")));
            Assert.Equal(2, service.Get("repeat")!.RunCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FileStore_RoundTripReplacementAndCancelledSave()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "schedules.json");
        try
        {
            Assert.Empty(await ScheduleFileStore.LoadAsync(path));
            var registration = new ScheduleRegistration("one", "yaml", "job.yaml",
                new Dictionary<string, string> { ["KEY"] = "value" }, false);
            await ScheduleFileStore.SaveAsync(path, new[] { registration });
            var loaded = Assert.Single(await ScheduleFileStore.LoadAsync(path));
            Assert.Equal("one", loaded.Id);
            Assert.Equal("value", loaded.Variables!["KEY"]);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ScheduleFileStore.SaveAsync(path, Array.Empty<ScheduleRegistration>(), cts.Token));
            Assert.Single(await ScheduleFileStore.LoadAsync(path));
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
            await ScheduleFileStore.SaveAsync(path, Array.Empty<ScheduleRegistration>());
            Assert.Empty(await ScheduleFileStore.LoadAsync(path));
            await File.WriteAllTextAsync(path, "not json");
            await Assert.ThrowsAsync<JsonException>(() => ScheduleFileStore.LoadAsync(path));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task ExportAndRestore_PreservesOriginalYamlAndDetachedVariables()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "in.csv"), "Id,Name\n1,Ada\n");
            var yaml = """
                name: restored
                schedule: "0 0 1 1 *"
                source:
                  type: csv
                  path: ${INPUT}
                sink:
                  type: json
                  path: ./out.json
                """;
            var variables = new Dictionary<string, string> { ["INPUT"] = "./in.csv" };
            await using var original = new JobScheduleService();
            original.Add(yaml, "saved", Path.Combine(dir, "job.yaml"), variables);
            variables["INPUT"] = "missing.csv";
            Assert.True((await original.RunNowAsync("saved")).Success);
            var json = JsonSerializer.Serialize(original.ExportRegistrations());
            var saved = JsonSerializer.Deserialize<ScheduleRegistration[]>(json)!;
            Assert.Contains("${INPUT}", saved[0].Yaml);
            Assert.Equal("./in.csv", saved[0].Variables!["INPUT"]);
            await using var restored = new JobScheduleService();
            restored.Restore(saved[0]);
            Assert.True((await restored.RunNowAsync("saved")).Success);
            Assert.Contains("Ada", await File.ReadAllTextAsync(Path.Combine(dir, "out.json")));
            Assert.Throws<InvalidOperationException>(() => restored.Restore(saved[0]));
        }
        finally { Directory.Delete(dir, true); }
    }
}
