using Cronos;
using DataForge.Sync.Models;

namespace DataForge.Sync.Execution;

public static class JobScheduler
{
    public static CronExpression ParseSchedule(string cronExpression) =>
        CronExpression.Parse(cronExpression, CronFormat.Standard);

    public static DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset fromUtc) =>
        ParseSchedule(cronExpression).GetNextOccurrence(fromUtc, TimeZoneInfo.Local);

    public static async Task WatchAsync(
        JobRunner runner,
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var yaml = await File.ReadAllTextAsync(jobFilePath, cancellationToken).ConfigureAwait(false);
        var job = Parsing.JobYamlParser.Parse(yaml);

        if (string.IsNullOrWhiteSpace(job.Schedule))
            throw new InvalidOperationException("Job has no schedule field. Add schedule: \"0 2 * * *\" or use 'run'.");

        var cron = ParseSchedule(job.Schedule);
        Console.WriteLine($"Watching job '{job.Name ?? Path.GetFileName(jobFilePath)}' — schedule: {job.Schedule}");

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var next = cron.GetNextOccurrence(now, TimeZoneInfo.Local);
            if (next == null)
            {
                Console.Error.WriteLine("No upcoming schedule occurrence.");
                return;
            }

            var delay = next.Value - now;
            if (delay > TimeSpan.Zero)
            {
                Console.WriteLine($"Next run at {next.Value.LocalDateTime:yyyy-MM-dd HH:mm:ss} (in {delay:g})");
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            Console.WriteLine($"Running scheduled job at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            var result = await runner.RunAsync(jobFilePath, variables, cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                Console.Error.WriteLine($"Scheduled run failed: {result.ErrorMessage}");
            }
            else
            {
                Console.WriteLine(
                    $"Scheduled run completed: {result.ExportResults!.RecordsWritten} record(s) written.");
            }
        }
    }
}
