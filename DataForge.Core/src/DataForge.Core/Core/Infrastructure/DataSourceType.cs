namespace DataForge.Core.Core.Infrastructure;

public enum DataSourceType
{
    Csv,
    Json,
    Excel,
    SqlServer,
    MySql,
    Sqlite,
    Memory,
    RestApi,
    Custom
}

public enum DataTargetType
{
    Csv,
    Json,
    Excel,
    SqlServer,
    MySql,
    Sqlite,
    Console,
    Stream,
    Custom
}
