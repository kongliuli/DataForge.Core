namespace DataForge.Core.Core.Models;

public enum ErrorKind
{
    Validation,
    Transform,
    IO,
    Unknown
}

public sealed class RowError
{
    public long? RowNumber { get; init; }

    public string? SourceLocation { get; init; }

    public string? PropertyName { get; init; }

    public string? RuleName { get; init; }

    public string? RawValue { get; init; }

    public string Message { get; init; } = string.Empty;

    public ErrorKind Kind { get; init; } = ErrorKind.Unknown;
}
