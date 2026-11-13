using System.Windows;
using System.Windows.Controls;

namespace NovaControlCenter.App.Views;

/// <summary>
/// Silhueta do controle com indicadores ao vivo. Usa o DataContext herdado (ViewModel com Buttons, LeftX… LeftTrigger).
/// <see cref="IsPlayStation"/> troca os rótulos para o padrão PlayStation; os bindings continuam os mesmos.
/// </summary>
public partial class ControllerVisual : UserControl
{
    public static readonly DependencyProperty IsPlayStationProperty = DependencyProperty.Register(
        nameof(IsPlayStation), typeof(bool), typeof(ControllerVisual),
        new PropertyMetadata(false, (d, _) => ((ControllerVisual)d).ApplyLabels()));

    public ControllerVisual()
    {
        InitializeComponent();
        ApplyLabels();
    }

    public bool IsPlayStation
    {
        get => (bool)GetValue(IsPlayStationProperty);
        set => SetValue(IsPlayStationProperty, value);
    }

    private void ApplyLabels()
    {
        var ps = IsPlayStation;
        FaceA.Label = ps ? "✕" : "A";
        FaceB.Label = ps ? "○" : "B";
        FaceX.Label = ps ? "□" : "X";
        FaceY.Label = ps ? "△" : "Y";
        BtnLB.Label = ps ? "L1" : "LB";
        BtnRB.Label = ps ? "R1" : "RB";
        BtnSelect.Label = ps ? "SHR" : "SEL";
        BtnSelect.ToolTip = ps ? "Share" : "Select / Back";
        BtnStart.Label = ps ? "OPT" : "STA";
        BtnStart.ToolTip = ps ? "Options" : "Start";
    }
}
