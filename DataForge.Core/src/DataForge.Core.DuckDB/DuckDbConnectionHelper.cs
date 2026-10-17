namespace DataForge.Core.DuckDB;

internal static class DuckDbConnectionHelper
{
    public static string BuildConnectionString(string databasePath) =>
        string.IsNullOrWhiteSpace(databasePath) || databasePath == ":memory:"
            ? "Data Source=:memory:"
            : $"Data Source={databasePath}";
}
