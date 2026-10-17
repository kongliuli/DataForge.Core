namespace DataForge.Jobs.Providers;

/// <summary>
/// Process-wide source/sink provider registry. Adapter packages call <see cref="Register"/> at startup.
/// Built-in csv/json providers are always present.
/// </summary>
public static class JobProviderRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, IJobSourceProvider> Sources = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IJobSinkProvider> Sinks = new(StringComparer.OrdinalIgnoreCase);
    private static bool _builtinsRegistered;

    public static void Register(IJobSourceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (Gate)
        {
            EnsureBuiltins();
            foreach (var type in provider.SourceTypes)
                Sources[type] = provider;
        }
    }

    public static void Register(IJobSinkProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (Gate)
        {
            EnsureBuiltins();
            foreach (var type in provider.SinkTypes)
                Sinks[type] = provider;
        }
    }

    public static bool TryGetSource(string type, out IJobSourceProvider provider)
    {
        lock (Gate)
        {
            EnsureBuiltins();
            return Sources.TryGetValue(type, out provider!);
        }
    }

    public static bool TryGetSink(string type, out IJobSinkProvider provider)
    {
        lock (Gate)
        {
            EnsureBuiltins();
            return Sinks.TryGetValue(type, out provider!);
        }
    }

    private static void EnsureBuiltins()
    {
        if (_builtinsRegistered)
            return;

        var file = new FileJobProviders();
        foreach (var type in file.SourceTypes)
            Sources.TryAdd(type, file);
        foreach (var type in file.SinkTypes)
            Sinks.TryAdd(type, file);

        _builtinsRegistered = true;
    }
}
