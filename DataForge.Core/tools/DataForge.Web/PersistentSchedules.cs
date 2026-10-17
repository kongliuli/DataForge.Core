using DataForge.Jobs.Scheduling;

namespace DataForge.Web;

/// <summary>Serializes registration changes and persists them before activating jobs.</summary>
public sealed class PersistentSchedules(JobScheduleService service, IConfiguration configuration, IHostEnvironment environment) : IHostedService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path = Path.GetFullPath(configuration["Schedules:Path"]
        ?? Path.Combine(environment.ContentRootPath, "data", "schedules.json"));

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var registrations = await ScheduleFileStore.LoadAsync(path, cancellationToken);
        // Validate the complete file before activating any job.
        await using var validation = new JobScheduleService();
        foreach (var registration in registrations) validation.Restore(registration);
        foreach (var registration in registrations) service.Restore(registration);
        service.Start();
    }

    public Task StopAsync(CancellationToken cancellationToken) => service.StopAsync();

    public async Task<ScheduledJobStatus> AddAsync(RegisterScheduleRequest request, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            await using var validation = new JobScheduleService();
            validation.Add(request.Yaml!, request.Id, variables: request.Variables, runOnStart: request.RunOnStart == true);
            var registration = validation.ExportRegistrations().Single();
            var registrations = service.ExportRegistrations().ToList();
            if (registrations.Any(item => item.Id == registration.Id))
                throw new InvalidOperationException($"Schedule '{registration.Id}' already exists.");
            registrations.Add(registration);
            await ScheduleFileStore.SaveAsync(path, registrations, token);
            return service.Restore(registration);
        }
        finally { gate.Release(); }
    }

    public async Task<bool> RemoveAsync(string id, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var registrations = service.ExportRegistrations().ToList();
            if (registrations.RemoveAll(item => item.Id == id) == 0) return false;
            await ScheduleFileStore.SaveAsync(path, registrations, token);
            return service.Remove(id);
        }
        finally { gate.Release(); }
    }
}
