namespace DataForge.Sync;

/// <summary>
/// Dynamic row for YAML jobs — column values keyed by header / field name.
/// </summary>
public sealed class JobRow
{
    public Dictionary<string, string?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Get(string key) => Values.TryGetValue(key, out var value) ? value : null;

    public JobRow Clone() => new() { Values = new Dictionary<string, string?>(Values, StringComparer.OrdinalIgnoreCase) };
}
