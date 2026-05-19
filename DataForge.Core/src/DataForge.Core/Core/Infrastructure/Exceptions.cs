using System;

namespace DataForge.Core.Core.Infrastructure;

public class DataForgeException : Exception
{
    public string ErrorCode { get; }
    public ErrorSeverity Severity { get; }

    public DataForgeException(string message, string errorCode = "GENERIC", ErrorSeverity severity = ErrorSeverity.Error)
        : base(message)
    {
        ErrorCode = errorCode;
        Severity = severity;
    }
}

public class DataSourceException : DataForgeException
{
    public string SourceName { get; }

    public DataSourceException(string message, string sourceName, string errorCode = "SOURCE_ERROR")
        : base(message, errorCode)
    {
        SourceName = sourceName;
    }
}

public class DataTargetException : DataForgeException
{
    public string TargetName { get; }

    public DataTargetException(string message, string targetName, string errorCode = "TARGET_ERROR")
        : base(message, errorCode)
    {
        TargetName = targetName;
    }
}

public class TransformException : DataForgeException
{
    public Type InputType { get; }
    public Type OutputType { get; }

    public TransformException(string message, Type inputType, Type outputType)
        : base(message, "TRANSFORM_ERROR")
    {
        InputType = inputType;
        OutputType = outputType;
    }
}

public enum ErrorSeverity
{
    Warning,
    Error,
    Critical
}
