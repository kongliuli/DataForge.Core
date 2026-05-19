using System;
using System.Collections.Generic;
using System.Linq;

namespace DataForge.Core.Core.Validation;

public class ValidationRuleBuilder<T, TProperty> : IValidationRule<T>
{
    private readonly string _propertyName;
    private readonly Func<T, TProperty> _propertyAccessor;
    private readonly List<Func<T, TProperty, ValidationError?>> _conditions = [];
    private string? _customMessage;
    private ValidationSeverity _severity = ValidationSeverity.Error;

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

    public ValidationRuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate)
    {
        _conditions.Add((instance, value) =>
        {
            if (value == null) return null;
            if (!predicate(value))
                return CreateError($"{_propertyName} does not meet the required condition", ValidationErrorCode.Custom);
            return null;
        });
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> When(Func<T, bool> condition)
    {
        var lastCondition = _conditions.LastOrDefault();
        if (lastCondition != null)
        {
            _conditions.Remove(lastCondition);
            _conditions.Add((instance, value) =>
            {
                if (!condition(instance)) return null;
                return lastCondition(instance, value);
            });
        }
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> MinLength(int minLength)
    {
        _conditions.Add((instance, value) =>
        {
            if (value is string str && str.Length < minLength)
                return CreateError($"{_propertyName} must be at least {minLength} characters", ValidationErrorCode.Length);
            return null;
        });
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> GreaterThanOrEqualTo(TProperty threshold)
    {
        _conditions.Add((instance, value) =>
        {
            if (value is IComparable<TProperty> comparable)
            {
                if (comparable.CompareTo(threshold) < 0)
                    return CreateError($"{_propertyName} must be greater than or equal to {threshold}", ValidationErrorCode.Range);
            }
            else if (value is IComparable comparable2)
            {
                if (comparable2.CompareTo(threshold) < 0)
                    return CreateError($"{_propertyName} must be greater than or equal to {threshold}", ValidationErrorCode.Range);
            }
            return null;
        });
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> LessThan(TProperty threshold)
    {
        _conditions.Add((instance, value) =>
        {
            if (value is IComparable<TProperty> comparable)
            {
                if (comparable.CompareTo(threshold) >= 0)
                    return CreateError($"{_propertyName} must be less than {threshold}", ValidationErrorCode.Range);
            }
            else if (value is IComparable comparable2)
            {
                if (comparable2.CompareTo(threshold) >= 0)
                    return CreateError($"{_propertyName} must be less than {threshold}", ValidationErrorCode.Range);
            }
            return null;
        });
        return this;
    }

    public ValidationRuleBuilder<T, TProperty> WithSeverity(ValidationSeverity severity)
    {
        _severity = severity;
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
            ErrorCode = errorCode,
            Severity = _severity
        };
    }
}
