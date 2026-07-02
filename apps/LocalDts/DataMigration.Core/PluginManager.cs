using DataMigration.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace DataMigration.Core;

public interface IPluginManager
{
    void LoadPlugins(string directory);
    IDataSource GetDataSource(string id);
    ITransformer GetTransformer(string id);
    IDataTarget GetTarget(string id);
    IEnumerable<PluginInfo> ListAllComponents();
    IEnumerable<PluginInfo> GetAvailableDataSources();
    IEnumerable<PluginInfo> GetAvailableTargets();
    IEnumerable<PluginInfo> GetAvailableTransformers();
}

public class PluginInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Version Version { get; set; } = new(1, 0, 0);
    public string Type { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new List<string>();
    public string AssemblyPath { get; set; } = string.Empty;
}

public class PluginManager : IPluginManager
{
    private readonly ConcurrentDictionary<string, IDataSource> _dataSources = new();
    private readonly ConcurrentDictionary<string, ITransformer> _transformers = new();
    private readonly ConcurrentDictionary<string, IDataTarget> _dataTargets = new();
    private readonly List<AssemblyLoadContext> _loadContexts = new();
    private readonly IServiceProvider _serviceProvider;

    public PluginManager(IServiceProvider serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    public void LoadPlugins(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Logger.Instance.Info("Created plugins directory: {0}", directory);
            return;
        }

        var dllFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
        Logger.Instance.Info("Found {0} potential plugin files", dllFiles.Length);

        foreach (var dllFile in dllFiles)
        {
            try
            {
                var loadContext = new AssemblyLoadContext($"Plugin_{Path.GetFileNameWithoutExtension(dllFile)}", true);
                _loadContexts.Add(loadContext);

                var assembly = loadContext.LoadFromAssemblyPath(dllFile);
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    if (typeof(IDataSource).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        var instance = CreatePluginInstance<IDataSource>(type);
                        if (_dataSources.TryAdd(instance.Id, instance))
                        {
                            Logger.Instance.Info("Loaded DataSource plugin: {0} (v{1})", instance.Name, instance.Version);
                        }
                        else
                        {
                            Logger.Instance.Warn("DataSource plugin with id {0} already exists, skipping", instance.Id);
                        }
                    }
                    else if (typeof(ITransformer).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        var instance = CreatePluginInstance<ITransformer>(type);
                        if (_transformers.TryAdd(instance.Id, instance))
                        {
                            Logger.Instance.Info("Loaded Transformer plugin: {0} (v{1})", instance.Name, instance.Version);
                        }
                        else
                        {
                            Logger.Instance.Warn("Transformer plugin with id {0} already exists, skipping", instance.Id);
                        }
                    }
                    else if (typeof(IDataTarget).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        var instance = CreatePluginInstance<IDataTarget>(type);
                        if (_dataTargets.TryAdd(instance.Id, instance))
                        {
                            Logger.Instance.Info("Loaded DataTarget plugin: {0} (v{1})", instance.Name, instance.Version);
                        }
                        else
                        {
                            Logger.Instance.Warn("DataTarget plugin with id {0} already exists, skipping", instance.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Error loading plugin {0}: {1}", ex, dllFile, ex.Message);
            }
        }
        
        Logger.Instance.Info("Plugin loading completed. Loaded {0} DataSources, {1} Transformers, {2} DataTargets", 
            _dataSources.Count, _transformers.Count, _dataTargets.Count);
    }

    private T CreatePluginInstance<T>(Type type)
    {
        if (_serviceProvider != null)
        {
            try
            {
                return (T)ActivatorUtilities.CreateInstance(_serviceProvider, type);
            }
            catch (Exception ex)
            {
                Logger.Instance.Warn("Error creating plugin instance with DI: {0}, falling back to parameterless constructor", ex.Message);
            }
        }
        try
        {
            return (T)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Failed to create plugin instance: {0}", ex, ex.Message);
            throw new DataMigration.Contracts.PluginException($"Failed to create plugin instance: {ex.Message}");
        }
    }

    public IDataSource GetDataSource(string id)
    {
        if (_dataSources.TryGetValue(id, out var dataSource))
        {
            return dataSource;
        }
        throw new KeyNotFoundException($"DataSource with id '{id}' not found.");
    }

    public ITransformer GetTransformer(string id)
    {
        if (_transformers.TryGetValue(id, out var transformer))
        {
            return transformer;
        }
        throw new KeyNotFoundException($"Transformer with id '{id}' not found.");
    }

    public IDataTarget GetTarget(string id)
    {
        if (_dataTargets.TryGetValue(id, out var target))
        {
            return target;
        }
        throw new KeyNotFoundException($"DataTarget with id '{id}' not found.");
    }

    public IEnumerable<PluginInfo> ListAllComponents()
    {
        var components = new List<PluginInfo>();

        foreach (var dataSource in _dataSources.Values)
        {
            components.Add(new PluginInfo
            {
                Id = dataSource.Id,
                Name = dataSource.Name,
                Version = dataSource.Version,
                Type = "DataSource"
            });
        }

        foreach (var transformer in _transformers.Values)
        {
            components.Add(new PluginInfo
            {
                Id = transformer.Id,
                Name = transformer.Name,
                Version = transformer.Version,
                Type = "Transformer"
            });
        }

        foreach (var target in _dataTargets.Values)
        {
            components.Add(new PluginInfo
            {
                Id = target.Id,
                Name = target.Name,
                Version = target.Version,
                Type = "DataTarget"
            });
        }

        return components;
    }

    public IEnumerable<PluginInfo> GetAvailableDataSources()
    {
        return _dataSources.Values.Select(ds => new PluginInfo
        {
            Id = ds.Id,
            Name = ds.Name,
            Version = ds.Version,
            Type = "DataSource"
        });
    }

    public IEnumerable<PluginInfo> GetAvailableTargets()
    {
        return _dataTargets.Values.Select(dt => new PluginInfo
        {
            Id = dt.Id,
            Name = dt.Name,
            Version = dt.Version,
            Type = "DataTarget"
        });
    }

    public IEnumerable<PluginInfo> GetAvailableTransformers()
    {
        return _transformers.Values.Select(t => new PluginInfo
        {
            Id = t.Id,
            Name = t.Name,
            Version = t.Version,
            Type = "Transformer"
        });
    }
}
