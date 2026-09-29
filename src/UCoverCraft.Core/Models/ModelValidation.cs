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
}
