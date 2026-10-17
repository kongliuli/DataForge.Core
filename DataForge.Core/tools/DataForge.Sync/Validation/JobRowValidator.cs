using System.Globalization;
using System.Text.RegularExpressions;
using DataForge.Core.Core.Validation;
using DataForge.Sync.Models;

namespace DataForge.Sync.Validation;

public sealed class JobRowValidator : IValidator<JobRow>
{
    private readonly IReadOnlyList<YamlValidationRule> _rules;

    public JobRowValidator(IReadOnlyList<YamlValidationRule> rules) => _rules = rules;

    public ValidationResult Validate(JobRow item)
    {
        var errors = new List<ValidationError>();

        foreach (var rule in _rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Field))
                continue;

            var value = item.Get(rule.Field);

            if (rule.Required == true && string.IsNullOrWhiteSpace(value))
            {
                errors.Add(ValidationError.Required(rule.Field));
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (rule.Min is { } min && TryParseDecimal(value, out var numMin) && numMin < min)
            {
                errors.Add(ValidationError.Custom(rule.Field, $"{rule.Field} must be >= {min}"));
            }

            if (rule.Max is { } max && TryParseDecimal(value, out var numMax) && numMax > max)
            {
                errors.Add(ValidationError.Custom(rule.Field, $"{rule.Field} must be <= {max}"));
            }

            if (!string.IsNullOrWhiteSpace(rule.Pattern))
            {
                if (!Regex.IsMatch(value, rule.Pattern, RegexOptions.CultureInvariant))
                    errors.Add(ValidationError.InvalidFormat(rule.Field));
            }
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }

    public Task<ValidationResult> ValidateAsync(JobRow item, CancellationToken cancellationToken = default) =>
        Task.FromResult(Validate(item));

    private static bool TryParseDecimal(string value, out decimal number) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
}
