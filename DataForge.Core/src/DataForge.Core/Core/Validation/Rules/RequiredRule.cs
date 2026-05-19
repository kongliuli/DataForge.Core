namespace DataForge.Core.Core.Validation.Rules;

internal class RequiredRule<T> : IValidator<T>
{
    private readonly Func<T, object?> _propertySelector;
    private readonly string _propertyName;

    public RequiredRule(Func<T, object?> propertySelector, string propertyName)
    {
        _propertySelector = propertySelector;
        _propertyName = propertyName;
    }

    public Task<ValidationResult> ValidateAsync(T item, CancellationToken cancellationToken = default)
    {
        var value = _propertySelector(item);
        if (value == null || 
            (value is string str && string.IsNullOrWhiteSpace(str)) ||
            (value is IEnumerable<object> enumerable && !enumerable.Any()))
        {
            return Task.FromResult(ValidationResult.Failure(new[]
            {
                ValidationError.Required(_propertyName)
            }));
        }
        return Task.FromResult(ValidationResult.Success());
    }
}