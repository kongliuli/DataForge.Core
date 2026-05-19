using DataForge.Core.Core.Validation;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.FluentValidation;

public class FluentValidationAdapter<T> : IValidator<T>
{
    private readonly global::FluentValidation.IValidator<T> _innerValidator;

    public FluentValidationAdapter(global::FluentValidation.IValidator<T> innerValidator)
    {
        _innerValidator = innerValidator;
    }

    public async Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default)
    {
        var fluentResult = await _innerValidator.ValidateAsync(instance, cancellationToken);

        if (fluentResult.IsValid)
        {
            return ValidationResult.Success();
        }

        var errors = fluentResult.Errors.Select(e => new ValidationError
        {
            PropertyName = e.PropertyName,
            Message = e.ErrorMessage,
            ErrorCode = ValidationErrorCode.Custom
        }).ToList();

        return ValidationResult.Failure(errors);
    }

    public ValidationResult Validate(T instance)
    {
        var fluentResult = _innerValidator.Validate(instance);

        if (fluentResult.IsValid)
        {
            return ValidationResult.Success();
        }

        var errors = fluentResult.Errors.Select(e => new ValidationError
        {
            PropertyName = e.PropertyName,
            Message = e.ErrorMessage,
            ErrorCode = ValidationErrorCode.Custom
        }).ToList();

        return ValidationResult.Failure(errors);
    }
}
