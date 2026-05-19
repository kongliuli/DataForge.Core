using DataForge.Core.Core.Pipeline;

namespace DataForge.Core.MySql;

public static class MySqlPipelineExtensions
{
    public static IDataPipeline<T> FromMySql<T>(string connectionString, string tableName) where T : new()
    {
        var source = new MySqlSource<T>(connectionString, tableName);
        return new DataPipeline<T>(source.ReadAsync());
    }
}