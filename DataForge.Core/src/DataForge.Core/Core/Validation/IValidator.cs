using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Validation;

public interface IValidator<in T>
{
    ValidationResult Validate(T item);
    Task<ValidationResult> ValidateAsync(T item, CancellationToken cancellationToken = default);
}

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    
    public List<ValidationError> Errors { get; } = [];

    public static ValidationResult Success() => new();
    
    public static ValidationResult Failure(IEnumerable<ValidationError> errors) => new() { Errors = errors.ToList() };
}

public class ValidationError
{
    public string PropertyName { get; set; } = string.Empty;
    
    public string Message { get; set; } = string.Empty;
    
    public ValidationErrorCode ErrorCode { get; set; }

    public static ValidationError Required(string propertyName) => new()
    {
        PropertyName = propertyName,
        Message = $"{propertyName} is required",
        ErrorCode = ValidationErrorCode.Required
    };

    public static ValidationError InvalidFormat(string propertyName) => new()
    {
        PropertyName = propertyName,
        Message = $"{propertyName} has invalid format",
        ErrorCode = ValidationErrorCode.InvalidFormat
    };

    public static ValidationError Custom(string propertyName, string message) => new()
    {
        PropertyName = propertyName,
        Message = message,
        ErrorCode = ValidationErrorCode.Custom
    };
}

public enum ValidationErrorCode
{
    Required,
    InvalidFormat,
    Range,
    Length,
    Custom
}

public class ValidationException : Exception
{
    public List<ValidationError> Errors { get; }

    public ValidationException(List<ValidationError> errors)
        : base($"Validation failed with {errors.Count} errors")
    {
        Errors = errors;
    }
}