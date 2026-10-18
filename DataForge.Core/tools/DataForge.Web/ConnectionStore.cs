using System.Collections.Concurrent;
using System.Text.Json;

namespace DataForge.Web;

public sealed class ConnectionStore
{
    private readonly string _path;
    private readonly ConcurrentDictionary<string, ConnectionProfile> _items = new(StringComparer.OrdinalIgnoreCase);

    public ConnectionStore(IWebHostEnvironment env, IConfiguration configuration)
    {
        _path = Path.GetFullPath(configuration["Connections:Path"]
            ?? Path.Combine(env.ContentRootPath, "data", "connections.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        Load();
    }

    public IReadOnlyList<ConnectionProfile> List() =>
        _items.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public ConnectionProfile Upsert(ConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
            profile = profile with { Id = Guid.NewGuid().ToString("N") };
        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new ArgumentException("name is required");
        if (string.IsNullOrWhiteSpace(profile.Type))
            throw new ArgumentException("type is required");
        if (string.IsNullOrWhiteSpace(profile.ConnectionString))
            throw new ArgumentException("connectionString is required");

        _items[profile.Id] = profile;
        Save();
        return profile;
    }

    public bool TryGet(string id, out ConnectionProfile profile) => _items.TryGetValue(id, out profile!);

    public bool Delete(string id)
    {
        var removed = _items.TryRemove(id, out _);
        if (removed) Save();
        return removed;
    }

    private void Load()
    {
        if (!File.Exists(_path))
            return;
        try
        {
            var json = File.ReadAllText(_path);
            var items = JsonSerializer.Deserialize<List<ConnectionProfile>>(json) ?? [];
            foreach (var item in items)
                _items[item.Id] = item;
        }
        catch
        {
            // ignore corrupt store; start empty
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_items.Values.ToArray(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}

public sealed record ConnectionProfile(string Id, string Name, string Type, string ConnectionString);
