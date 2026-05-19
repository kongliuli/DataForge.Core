using System;
using System.Collections.Generic;

namespace DataForge.Core.Core.Validation;

public class ValidationRuleBuilder<T, TProperty> : IValidationRule<T>
{
    private readonly string _propertyName;
    private readonly Func<T, TProperty> _propertyAccessor;
    private readonly List<Func<T, TProperty, ValidationError?>> _conditions = [];
    private string? _customMessage;

    public string RuleName => _propertyName;

    public ValidationRuleBuilder(string propertyName, Func<T, TProperty> propertyAccessor)
    {
        _propertyName = propertyName;
        _propertyAccessor = propertyAccessor;
    }

    public ValidationRuleBuilder<T, TProperty> NotEmpty()
    {
        _conditions.Add((instance, value) =>
        {
            if (value == null)
                return CreateError($"{_propertyName} must not be empty", ValidationErrorCode.Required);

            if (value is string str && string.IsNullOrWhiteSpace(str))
                return CreateError($"{_propertyName} must not be empty", ValidationErrorCode.Required);

            if (value is System.Collections.ICollection col && col.Count == 0)
                return CreateError($"{_propertyName} must not be empty", ValidationErrorCode.Required);

            return null;
        });
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> Required()
    {
        _conditions.Add((instance, value) =>
        {
            if (value == null)
                return CreateError($"{_propertyName} is required", ValidationErrorCode.Required);

            if (value is string str && string.IsNullOrWhiteSpace(str))
                return CreateError($"{_propertyName} is required", ValidationErrorCode.Required);

            return null;
        });
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> Length(int min, int max)
    {
        _conditions.Add((instance, value) =>
        {
            if (value is string str)
            {
                if (str.Length < min || str.Length > max)
                    return CreateError($"{_propertyName} must be between {min} and {max} characters", ValidationErrorCode.Length);
            }
            return null;
        });
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> InRange(TProperty min, TProperty max)
    {
        _conditions.Add((instance, value) =>
        {
            if (value is IComparable<TProperty> comparable)
            {
                if (comparable.CompareTo(min) < 0 || comparable.CompareTo(max) > 0)
                    return CreateError($"{_propertyName} must be between {min} and {max}", ValidationErrorCode.Range);
            }
            else if (value is IComparable comparable2)
            {
                if (comparable2.CompareTo(min) < 0 || comparable2.CompareTo(max) > 0)
                    return CreateError($"{_propertyName} must be between {min} and {max}", ValidationErrorCode.Range);
            }
            return null;
        });
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> GreaterThan(TProperty threshold)
    {
        _conditions.Add((instance, value) =>
        {
            if (value is IComparable<TProperty> comparable)
            {
                if (comparable.CompareTo(threshold) <= 0)
                    return CreateError($"{_propertyName} must be greater than {threshold}", ValidationErrorCode.Range);
            }
            else if (value is IComparable comparable2)
            {
                if (comparable2.CompareTo(threshold) <= 0)
                    return CreateError($"{_propertyName} must be greater than {threshold}", ValidationErrorCode.Range);
            }
            return null;
        });
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> WithMessage(string message)
    {
        _customMessage = message;
        return this;
    }

    public ValidationError? Validate(T instance)
    {
        var value = _propertyAccessor(instance);
        foreach (var condition in _conditions)
        {
            var error = condition(instance, value);
            if (error != null)
            {
                if (_customMessage != null)
                    error.Message = _customMessage;
                return error;
            }
        }
        return null;
    }

    private ValidationError CreateError(string message, ValidationErrorCode errorCode)
    {
        return new ValidationError
        {
            PropertyName = _propertyName,
            Message = message,
            ErrorCode = errorCode
        };
    }
}
