namespace DataForge.Core.Core.Validation;

public interface IValidationRule<T>
{
    string RuleName { get; }
    ValidationError? Validate(T instance);
}
