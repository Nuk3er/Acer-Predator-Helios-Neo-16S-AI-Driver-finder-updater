using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HeliosToolkit.App.Views.Controls;

/// <summary>
/// Minimal line chart (no chart library): one accent series, an optional gray
/// secondary series, an optional best-point marker. Large series are
/// min/max-downsampled per pixel column before rendering.
/// </summary>
public partial class SweepChart
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<Point>), typeof(SweepChart),
        new PropertyMetadata(null, (d, _) => ((SweepChart)d).Redraw()));

    public static readonly DependencyProperty SecondaryPointsProperty = DependencyProperty.Register(
        nameof(SecondaryPoints), typeof(IReadOnlyList<Point>), typeof(SweepChart),
        new PropertyMetadata(null, (d, _) => ((SweepChart)d).Redraw()));

    public static readonly DependencyProperty BestPointProperty = DependencyProperty.Register(
        nameof(BestPoint), typeof(Point?), typeof(SweepChart),
        new PropertyMetadata(null, (d, _) => ((SweepChart)d).Redraw()));

    public static readonly DependencyProperty XLabelFormatProperty = DependencyProperty.Register(
        nameof(XLabelFormat), typeof(string), typeof(SweepChart), new PropertyMetadata("0.0000"));

    public static readonly DependencyProperty YLabelFormatProperty = DependencyProperty.Register(
        nameof(YLabelFormat), typeof(string), typeof(SweepChart), new PropertyMetadata("0.000"));

    public IReadOnlyList<Point>? Points
    {
        get => (IReadOnlyList<Point>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public IReadOnlyList<Point>? SecondaryPoints
    {
        get => (IReadOnlyList<Point>?)GetValue(SecondaryPointsProperty);
        set => SetValue(SecondaryPointsProperty, value);
    }

    public Point? BestPoint
    {
        get => (Point?)GetValue(BestPointProperty);
        set => SetValue(BestPointProperty, value);
    }

    public string XLabelFormat
    {
        get => (string)GetValue(XLabelFormatProperty);
        set => SetValue(XLabelFormatProperty, value);
    }

    public string YLabelFormat
    {
        get => (string)GetValue(YLabelFormatProperty);
        set => SetValue(YLabelFormatProperty, value);
    }

    private const double MarginLeft = 52, MarginRight = 14, MarginTop = 12, MarginBottom = 26;

    public SweepChart()
    {
        InitializeComponent();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        PlotCanvas.Children.Clear();

        double width = PlotCanvas.ActualWidth;
        double height = PlotCanvas.ActualHeight;
        IReadOnlyList<Point>? primary = Points;
        if (width < 60 || height < 60 || primary is null || primary.Count == 0)
        {
            return;
        }

        IEnumerable<Point> all = primary;
        if (SecondaryPoints is { Count: > 0 } secondary)
        {
            all = all.Concat(secondary);
        }

        double minX = all.Min(p => p.X), maxX = all.Max(p => p.X);
        double minY = all.Min(p => p.Y), maxY = all.Max(p => p.Y);
        if (maxX - minX < 1e-12)
        {
            maxX = minX + 1;
        }

        double padY = Math.Max((maxY - minY) * 0.08, 1e-9);
        minY -= padY;
        maxY += padY;

        double plotW = width - MarginLeft - MarginRight;
        double plotH = height - MarginTop - MarginBottom;

        double ToX(double x) => MarginLeft + (x - minX) / (maxX - minX) * plotW;
        double ToY(double y) => MarginTop + (1 - (y - minY) / (maxY - minY)) * plotH;

        var gridBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        var labelBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));

        // Horizontal gridlines + Y labels
        for (int i = 0; i <= 4; i++)
        {
            double yValue = minY + (maxY - minY) * i / 4.0;
            double y = ToY(yValue);
            PlotCanvas.Children.Add(new Line
            {
                X1 = MarginLeft, X2 = width - MarginRight, Y1 = y, Y2 = y,
                Stroke = gridBrush, StrokeThickness = 1,
            });
            var label = new TextBlock
            {
                Text = yValue.ToString(YLabelFormat),
                FontSize = 10,
                Foreground = labelBrush,
            };
            Canvas.SetLeft(label, 2);
            Canvas.SetTop(label, y - 7);
            PlotCanvas.Children.Add(label);
        }

        // X labels: min / mid / max
        foreach (double xValue in new[] { minX, (minX + maxX) / 2, maxX })
        {
            var label = new TextBlock
            {
                Text = xValue.ToString(XLabelFormat),
                FontSize = 10,
                Foreground = labelBrush,
            };
            double x = ToX(xValue);
            Canvas.SetLeft(label, Math.Min(x - 16, width - 56));
            Canvas.SetTop(label, height - MarginBottom + 6);
            PlotCanvas.Children.Add(label);
        }

        if (SecondaryPoints is { Count: > 0 } sec)
        {
            PlotCanvas.Children.Add(BuildPolyline(
                Downsample(sec, (int)plotW), ToX, ToY,
                new SolidColorBrush(Color.FromArgb(140, 160, 160, 160)), 1.5));
        }

        Brush accent = Application.Current.TryFindResource("PredatorAccentBrush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xD1));
        PlotCanvas.Children.Add(BuildPolyline(Downsample(primary, (int)plotW), ToX, ToY, accent, 2));

        if (BestPoint is { } best)
        {
            var dot = new Ellipse { Width = 9, Height = 9, Fill = accent };
            Canvas.SetLeft(dot, ToX(best.X) - 4.5);
            Canvas.SetTop(dot, ToY(best.Y) - 4.5);
            PlotCanvas.Children.Add(dot);

            var label = new TextBlock
            {
                Text = best.X.ToString(XLabelFormat),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = accent,
            };
            Canvas.SetLeft(label, Math.Min(ToX(best.X) + 7, width - 60));
            Canvas.SetTop(label, Math.Max(ToY(best.Y) - 18, 0));
            PlotCanvas.Children.Add(label);
        }
    }

    private static Polyline BuildPolyline(
        IReadOnlyList<Point> data, Func<double, double> toX, Func<double, double> toY,
        Brush stroke, double thickness)
    {
        var polyline = new Polyline
        {
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
        };
        foreach (Point p in data)
        {
            polyline.Points.Add(new Point(toX(p.X), toY(p.Y)));
        }

        return polyline;
    }

    /// <summary>Min/max per pixel column: preserves spikes while capping render cost.</summary>
    private static IReadOnlyList<Point> Downsample(IReadOnlyList<Point> data, int pixelWidth)
    {
        if (pixelWidth < 4 || data.Count <= pixelWidth * 2)
        {
            return data;
        }

        var result = new List<Point>(pixelWidth * 2);
        int bucketSize = (int)Math.Ceiling(data.Count / (double)pixelWidth);
        for (int start = 0; start < data.Count; start += bucketSize)
        {
            int end = Math.Min(start + bucketSize, data.Count);
            Point minP = data[start], maxP = data[start];
            for (int i = start + 1; i < end; i++)
            {
                if (data[i].Y < minP.Y)
                {
                    minP = data[i];
                }

                if (data[i].Y > maxP.Y)
                {
                    maxP = data[i];
                }
            }

            if (minP.X <= maxP.X)
            {
                result.Add(minP);
                if (maxP != minP)
                {
                    result.Add(maxP);
                }
            }
            else
            {
                result.Add(maxP);
                result.Add(minP);
            }
        }

        return result;
    }
}
