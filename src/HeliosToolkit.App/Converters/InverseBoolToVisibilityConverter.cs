using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HeliosToolkit.App.Converters;

/// <summary>true ⇒ Collapsed, false ⇒ Visible. The inverse of the built-in converter.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}
