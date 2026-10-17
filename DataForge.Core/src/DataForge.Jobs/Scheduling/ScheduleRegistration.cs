namespace DataForge.Jobs.Scheduling;

/// <summary>Portable registration; may contain connection secrets. Store securely.</summary>
public sealed record ScheduleRegistration(
    string Id,
    string Yaml,
    string JobFilePath,
    Dictionary<string, string>? Variables,
    bool RunOnStart);
