using System;
using System.Globalization;
using System.Windows.Data;

namespace UCoverCraft.App.ViewModels;

public sealed class ExportStateToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExportState state && parameter is string targetState)
        {
            return Enum.TryParse<ExportState>(targetState, true, out var target)
                && state == target;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}