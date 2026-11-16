using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;
using NovaControlCenter.Infrastructure.Ds4;

namespace NovaControlCenter.App.ViewModels;

/// <summary>
/// Configurações por software do perfil ativo, com pré-visualização ao vivo (bruto x com perfil).
/// Os sliders atualizam a prévia na hora; a gravação acontece 300 ms depois do último ajuste.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ProfileService _profiles;
    private readonly InputMonitor? _gamesir;
    private readonly IDs4Controller? _ds4;
    private readonly DispatcherTimer _commit;
    private bool _loading;

    // Valores em porcentagem/escala amigável para os sliders.
    [ObservableProperty] private double _leftDeadzone;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LeftCurveText))]
    private double _leftCurve;
    [ObservableProperty] private double _rightDeadzone;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RightCurveText))]
    private double _rightCurve;
    [ObservableProperty] private double _triggerDeadzone;
    [ObservableProperty] private double _triggerFullAt;
    [ObservableProperty] private double _vibration;
    [ObservableProperty] private string _profileName = "";

    // Pré-visualização
    [ObservableProperty] private string _sourceText = "";
    [ObservableProperty] private bool _hasSource;
    [ObservableProperty] private double _rawLX;
    [ObservableProperty] private double _rawLY;
    [ObservableProperty] private double _outLX;
    [ObservableProperty] private double _outLY;
    [ObservableProperty] private double _rawRX;
    [ObservableProperty] private double _rawRY;
    [ObservableProperty] private double _outRX;
    [ObservableProperty] private double _outRY;
    [ObservableProperty] private double _rawLT;
    [ObservableProperty] private double _outLT;
    [ObservableProperty] private double _rawRT;
    [ObservableProperty] private double _outRT;

    public SettingsViewModel(ProfileService profiles, InputMonitor? gamesir = null, IDs4Controller? ds4 = null, bool startTimers = true,
        Services.UiActivity? activity = null, BackgroundOptionsViewModel? background = null)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        Background = background;
        _gamesir = gamesir;
        _ds4 = ds4;
        _commit = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _commit.Tick += (_, _) => CommitNow();
        _profiles.Changed += (_, _) => { if (!_commit.IsEnabled) LoadActive(); };
        LoadActive();

        if (!startTimers) return;
        var frame = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        frame.Tick += (_, _) => RefreshPreview();
        frame.Start();
        activity?.Track(frame);
    }

    public string LeftCurveText => DescribeCurve(LeftCurve);

    public string RightCurveText => DescribeCurve(RightCurve);

    /// <summary>Opções de segundo plano (bandeja, iniciar com o Windows). Null nos testes.</summary>
    public BackgroundOptionsViewModel? Background { get; }

    /// <summary>Perfil montado a partir dos sliders (usado na prévia e ao gravar).</summary>
    public InputProfile Draft => _profiles.Active with
    {
        LeftStick = new StickSettings { Deadzone = LeftDeadzone / 100, Curve = LeftCurve },
        RightStick = new StickSettings { Deadzone = RightDeadzone / 100, Curve = RightCurve },
        Triggers = new TriggerSettings { Deadzone = TriggerDeadzone / 100, FullPressAt = TriggerFullAt / 100 },
        Vibration = Vibration / 100,
    };

    public static string Scope =>
        "Aplica-se aos jogos: ✅ PS4 no modo Xbox (o app gera o controle que o jogo lê). " +
        "GameSir: só pré-visualização, porque o jogo lê o controle direto. Nada é gravado no hardware.";

    /// <summary>Grava imediatamente o rascunho no perfil ativo (também chamado pelo timer).</summary>
    public void CommitNow()
    {
        _commit.Stop();
        _profiles.Update(Draft);
    }

    [RelayCommand]
    private void ResetNeutral()
    {
        _loading = true;
        LeftDeadzone = RightDeadzone = 5;
        LeftCurve = RightCurve = 1.0;
        TriggerDeadzone = 0;
        TriggerFullAt = 100;
        Vibration = 100;
        _loading = false;
        CommitNow();
    }

    partial void OnLeftDeadzoneChanged(double value) => Schedule();
    partial void OnLeftCurveChanged(double value) => Schedule();
    partial void OnRightDeadzoneChanged(double value) => Schedule();
    partial void OnRightCurveChanged(double value) => Schedule();
    partial void OnTriggerDeadzoneChanged(double value) => Schedule();
    partial void OnTriggerFullAtChanged(double value) => Schedule();
    partial void OnVibrationChanged(double value) => Schedule();

    private void Schedule()
    {
        if (_loading) return;
        _commit.Stop();
        _commit.Start();
    }

    private void LoadActive()
    {
        var p = _profiles.Active.Normalized();
        _loading = true;
        ProfileName = p.Name;
        LeftDeadzone = Math.Round(p.LeftStick.Deadzone * 100);
        LeftCurve = p.LeftStick.Curve;
        RightDeadzone = Math.Round(p.RightStick.Deadzone * 100);
        RightCurve = p.RightStick.Curve;
        TriggerDeadzone = Math.Round(p.Triggers.Deadzone * 100);
        TriggerFullAt = Math.Round(p.Triggers.FullPressAt * 100);
        Vibration = Math.Round(p.Vibration * 100);
        _loading = false;
    }

    /// <summary>Fonte da prévia: PS4 conectado, senão GameSir.</summary>
    internal void RefreshPreview()
    {
        ControllerState? state = null;
        var ds4 = _ds4?.Current;
        if (ds4 is { Link: Ds4LinkState.Connected, Input: { } input })
        {
            state = input.State;
            SourceText = "Fonte da prévia: controle PS4";
        }
        else if (_gamesir?.Latest is { } sample)
        {
            state = sample.State;
            SourceText = "Fonte da prévia: GameSir T4 Nova Lite";
        }
        else
        {
            SourceText = "Conecte um controle para ver a prévia.";
        }

        HasSource = state is not null;
        if (state is null) return;
        var output = InputProcessor.Apply(state, Draft);
        (RawLX, RawLY, OutLX, OutLY) = (state.LeftStick.X, state.LeftStick.Y, output.LeftStick.X, output.LeftStick.Y);
        (RawRX, RawRY, OutRX, OutRY) = (state.RightStick.X, state.RightStick.Y, output.RightStick.X, output.RightStick.Y);
        (RawLT, OutLT, RawRT, OutRT) = (state.LeftTrigger, output.LeftTrigger, state.RightTrigger, output.RightTrigger);
    }

    /// <summary>Texto curto da curva para o rótulo do slider.</summary>
    public static string DescribeCurve(double curve) => curve switch
    {
        < 0.9 => string.Create(CultureInfo.InvariantCulture, $"{curve:0.0} · resposta rápida"),
        <= 1.1 => string.Create(CultureInfo.InvariantCulture, $"{curve:0.0} · linear"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{curve:0.0} · precisão no centro"),
    };
}
