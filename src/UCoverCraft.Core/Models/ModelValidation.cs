using System.Globalization;

namespace UCoverCraft.Core.Models;

internal static class ModelValidation
{
    public static void RequireText(List<string> errors, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }

    public static void ValidateOptionalPositiveInteger(List<string> errors, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            errors.Add($"{fieldName} must be a positive integer.");
        }
    }
}
