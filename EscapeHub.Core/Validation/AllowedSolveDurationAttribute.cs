using System.ComponentModel.DataAnnotations;

namespace EscapeHub.Core.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class AllowedSolveDurationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is int minutes && minutes is 60 or 90 or 120)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(ErrorMessage ?? "Válasszon 60, 90 vagy 120 perces játékidőt.");
    }
}
