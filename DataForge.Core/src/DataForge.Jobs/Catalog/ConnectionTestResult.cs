namespace DataForge.Jobs.Catalog;

public sealed class ConnectionTestResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static ConnectionTestResult Ok() => new() { Success = true };

    public static ConnectionTestResult Fail(string error) => new() { Success = false, Error = error };
}
