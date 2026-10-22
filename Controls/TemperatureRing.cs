using System.Windows;
using System.Windows.Media;

namespace WatercoolerTemp.Controls;

public sealed class TemperatureRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(TemperatureRing),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IndicatorBrushProperty =
        DependencyProperty.Register(
            nameof(IndicatorBrush),
            typeof(System.Windows.Media.Brush),
            typeof(TemperatureRing),
            new FrameworkPropertyMetadata(System.Windows.Media.Brushes.LimeGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public System.Windows.Media.Brush IndicatorBrush
    {
        get => (System.Windows.Media.Brush)GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    // Brush de LED inativo congelado, em vez de alocar 72 objetos a cada render.
    private static readonly System.Windows.Media.Brush InactiveDotBrush = CreateInactiveDotBrush();

    private static System.Windows.Media.Brush CreateInactiveDotBrush()
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(38, 210, 220, 220));
        brush.Freeze();
        return brush;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        double centerX = ActualWidth / 2;
        double centerY = ActualHeight / 2;
        double radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 12);
        double dotRadius = 2.4;
        int dotCount = 72;
        int activeDots = (int)Math.Round(Math.Clamp(Progress, 0, 1) * dotCount);

        for (int index = 0; index < dotCount; index++)
        {
            double angle = -Math.PI / 2 + index * (Math.PI * 2 / dotCount);
            double x = centerX + Math.Cos(angle) * radius;
            double y = centerY + Math.Sin(angle) * radius;
            bool isActive = index < activeDots;
            System.Windows.Media.Brush brush = isActive ? IndicatorBrush : InactiveDotBrush;
            double currentRadius = isActive ? dotRadius : 1.6;

            drawingContext.DrawEllipse(brush, null, new System.Windows.Point(x, y), currentRadius, currentRadius);
        }
    }
}