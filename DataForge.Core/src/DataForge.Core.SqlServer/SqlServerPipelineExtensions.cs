using DataForge.Core.Core.Pipeline;

namespace DataForge.Core.SqlServer;

public static class SqlServerPipelineExtensions
{
    public static IDataPipeline<T> FromSqlServer<T>(string connectionString, string tableName) where T : new()
    {
        var source = new SqlServerSource<T>(connectionString, tableName);
        return new DataPipeline<T>(source.ReadAsync());
    }
}