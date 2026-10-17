using System.Text.Json;
using DataForge.Jobs.Scheduling;
using Xunit;

namespace DataForge.Sync.Tests;

public class SchedulePersistenceTests
{
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
