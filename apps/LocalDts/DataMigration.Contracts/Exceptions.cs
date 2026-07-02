namespace DataMigration.Contracts;

public class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message)
    {}
    public ConfigurationException(string message, Exception innerException) : base(message, innerException)
    {}
}

public class DataException : Exception
{
    public DataException(string message) : base(message)
    {}
    public DataException(string message, Exception innerException) : base(message, innerException)
    {}
}

public class PluginException : Exception
{
    public PluginException(string message) : base(message)
    {}
    public PluginException(string message, Exception innerException) : base(message, innerException)
    {}
}
