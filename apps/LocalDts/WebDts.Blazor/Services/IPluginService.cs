using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataMigration.Contracts;

namespace WebDts.Blazor.Services
{
    public interface IPluginService
    {
        Task<IEnumerable<IPlugin>> GetAllPluginsAsync();
        Task<IEnumerable<IDataSource>> GetDataSourcesAsync();
        Task<IEnumerable<IDataTarget>> GetDataTargetsAsync();
        Task<IEnumerable<ITransformer>> GetTransformersAsync();
        Task<IPlugin> GetPluginAsync(string pluginName);
        Task<bool> LoadPluginAsync(string pluginPath);
        Task<bool> UnloadPluginAsync(string pluginName);
        Task<bool> TestConnectionAsync(string pluginName, Dictionary<string, string> connectionString);
        Task SavePluginConfigAsync(string pluginName, Dictionary<string, string> config);
        Task<Dictionary<string, string>> GetPluginConfigAsync(string pluginName);
    }
}