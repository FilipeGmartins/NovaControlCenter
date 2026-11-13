using System.Windows;
using System.Windows.Media;

namespace NovaControlCenter.App.Controls;

/// <summary>
/// Representação 2D de um analógico: círculo de limite, cruz central, rastro do ponto e o ponto atual.
/// X e Y vão de -1 a 1 (Y positivo = para cima, como no XInput).
/// </summary>
public sealed class StickView : FrameworkElement
{
    public static readonly DependencyProperty XProperty = Register(nameof(X), 0.0);
    public static readonly DependencyProperty YProperty = Register(nameof(Y), 0.0);
    public static readonly DependencyProperty IsPressedProperty = Register(nameof(IsPressed), false);
    public static readonly DependencyProperty RingBrushProperty = Register<Brush?>(nameof(RingBrush), null);
    public static readonly DependencyProperty FillBrushProperty = Register<Brush?>(nameof(FillBrush), null);
    public static readonly DependencyProperty DotBrushProperty = Register<Brush?>(nameof(DotBrush), null);
    public static readonly DependencyProperty PressedBrushProperty = Register<Brush?>(nameof(PressedBrush), null);

    public double X { get => (double)GetValue(XProperty); set => SetValue(XProperty, value); }
    public double Y { get => (double)GetValue(YProperty); set => SetValue(YProperty, value); }
    public bool IsPressed { get => (bool)GetValue(IsPressedProperty); set => SetValue(IsPressedProperty, value); }
    public Brush? RingBrush { get => (Brush?)GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public Brush? FillBrush { get => (Brush?)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public Brush? DotBrush { get => (Brush?)GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }
    public Brush? PressedBrush { get => (Brush?)GetValue(PressedBrushProperty); set => SetValue(PressedBrushProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        const double dotRadius = 9;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size / 2) - 2;
        var travel = radius - dotRadius;

        var ringPen = new Pen(IsPressed ? PressedBrush : RingBrush, IsPressed ? 3 : 1.5);
        drawingContext.DrawEllipse(FillBrush, ringPen, center, radius, radius);

        var crossPen = new Pen(RingBrush, 1) { DashStyle = DashStyles.Dot };
        drawingContext.DrawLine(crossPen, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
        drawingContext.DrawLine(crossPen, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));

        var x = Math.Clamp(X, -1, 1);
        var y = Math.Clamp(Y, -1, 1);
        var dot = new Point(center.X + (x * travel), center.Y - (y * travel));
        drawingContext.DrawLine(new Pen(DotBrush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, center, dot);
        drawingContext.DrawEllipse(DotBrush, null, dot, dotRadius, dotRadius);
    }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(StickView),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));
}
