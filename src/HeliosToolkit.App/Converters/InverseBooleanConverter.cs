using System.Globalization;
using System.Windows.Data;

namespace HeliosToolkit.App.Converters;

/// <summary>Negates a boolean. Used to disable a button while a task is busy.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}
