using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HeliosToolkit.Core.Lab;

namespace HeliosToolkit.App.Converters;

/// <summary>DpcVerdict → badge brush (green/yellow/orange/red) reusing the risk palette.</summary>
public sealed class DpcVerdictToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value switch
        {
            DpcVerdict.Good => "RiskSafeBrush",
            DpcVerdict.Noticeable => "RiskSituationalBrush",
            DpcVerdict.Concerning => "RiskSituationalBrush",
            DpcVerdict.Bad => "RiskRiskyBrush",
            _ => "RiskInfoBrush",
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
