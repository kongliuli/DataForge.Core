using DataMigration.Contracts;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;

namespace DataMigration.Plugin.SqlServerSource;

public class SqlServerDataSource : IDataSource
{
    public string Id => "Standard.SqlServerSource";
    public string Name => "SQL Server Database Source";
    public Version Version => new(1, 0, 0);

    private string _connectionString = "";
    private string _query = "";

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        // 可在此注入日志、配置中心等依赖
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(
        SourceConfig config,
        [EnumeratorCancellation] CancellationToken ct)
    {
        _connectionString = config["ConnectionString"];
        _query = config.TryGetValue("Query", out var query) ? query : "SELECT * FROM [Table]";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(_query, conn);
        await conn.OpenAsync(ct);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var record = new DataRecord();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                record[reader.GetName(i)] = reader.GetValue(i);
            }
            yield return record;
        }
    }

    public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken ct) => Task.CompletedTask;
}
