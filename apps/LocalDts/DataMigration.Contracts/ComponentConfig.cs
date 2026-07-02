namespace DataMigration.Contracts;

public abstract class ComponentConfig : Dictionary<string, string>
{
    public string ComponentId { get; set; } = "";
    public string? Description { get; set; }
}

public class SourceConfig : ComponentConfig { }
public class TransformConfig : ComponentConfig { }
public class TargetConfig : ComponentConfig { }

public class DataSourceConfigCollection<T> where T : ComponentConfig, new()
{
    public List<T> Configurations { get; set; } = new List<T>();
    
    public void AddConfig(T config)
    {
        Configurations.Add(config);
    }
    
    public void RemoveConfig(string componentId)
    {
        Configurations.RemoveAll(c => c.ComponentId == componentId);
    }
    
    public T? GetConfig(string componentId)
    {
        return Configurations.FirstOrDefault(c => c.ComponentId == componentId);
    }
}
