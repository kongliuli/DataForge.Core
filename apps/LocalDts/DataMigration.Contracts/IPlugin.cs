namespace DataMigration.Contracts;

public interface IPlugin
{
    string Id { get; }          // 唯一标识，如 "MyCompany.SqlServerSource"
    string Name { get; }        // 显示名称
    Version Version { get; }    // 语义化版本

    Task InitializeAsync(IServiceProvider services, CancellationToken ct);
    Task ExecuteAsync(CancellationToken ct);   // 可选，用于长生命周期的组件
    Task ShutdownAsync(CancellationToken ct);
}
