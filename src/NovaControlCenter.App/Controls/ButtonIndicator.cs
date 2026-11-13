using System.Windows;
using System.Windows.Controls;

namespace NovaControlCenter.App.Controls;

/// <summary>Indicador visual de um botão do controle (aceso quando pressionado).</summary>
public sealed class ButtonIndicator : Control
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ButtonIndicator), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ButtonIndicator), new PropertyMetadata(false));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ButtonIndicator), new PropertyMetadata(new CornerRadius(6)));

    static ButtonIndicator() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonIndicator), new FrameworkPropertyMetadata(typeof(ButtonIndicator)));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
}
