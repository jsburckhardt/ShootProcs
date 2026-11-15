using System.Windows;
using System.Windows.Media;

namespace ShootProcs.App;

public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable<double>), typeof(Sparkline), new FrameworkPropertyMetadata(
            Array.Empty<double>(), FrameworkPropertyMetadataOptions.AffectsRender));
    public IEnumerable<double> Values { get => (IEnumerable<double>)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Brush Stroke { get; set; } = new SolidColorBrush(Color.FromRgb(107, 244, 197));
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var values = Values.ToArray();
        if (values.Length < 2 || ActualWidth <= 0 || ActualHeight <= 0) return;
        var max = Math.Max(10, values.Max());
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new(0, ActualHeight - values[0] / max * (ActualHeight - 3)), false, false);
            for (var i = 1; i < values.Length; i++)
                context.LineTo(new(i * ActualWidth / (values.Length - 1),
                    ActualHeight - values[i] / max * (ActualHeight - 3)), true, false);
        }
        dc.DrawGeometry(null, new Pen(Stroke, 1.5), geometry);
    }
}
