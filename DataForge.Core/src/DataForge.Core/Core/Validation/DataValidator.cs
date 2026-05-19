using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Validation;

public abstract class DataValidator<T> : IValidator<T>
{
    private readonly List<IValidationRule<T>> _rules = [];

    public IReadOnlyList<IValidationRule<T>> Rules => _rules.AsReadOnly();

    protected ValidationRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
    {
        var propertyName = GetPropertyName(propertyExpression);
        var builder = new ValidationRuleBuilder<T, TProperty>(propertyName, propertyExpression.Compile());
        _rules.Add(builder);
        return builder;
    }

    protected void AddRule(IValidationRule<T> rule) => _rules.Add(rule);

    public ValidationResult Validate(T instance)
    {
        var errors = new List<ValidationError>();
        foreach (var rule in _rules)
        {
            var error = rule.Validate(instance);
            if (error != null) errors.Add(error);
        }
        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }

    public Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Validate(instance));
    }

    private static string GetPropertyName<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        return expression.Body is MemberExpression memberExpr
            ? memberExpr.Member.Name
            : expression.ToString();
    }
}
