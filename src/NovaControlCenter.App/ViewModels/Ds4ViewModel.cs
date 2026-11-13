using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaControlCenter.App.Presentation;
using NovaControlCenter.App.Services;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.Audio;
using NovaControlCenter.Infrastructure.Ds4;

namespace NovaControlCenter.App.ViewModels;

public sealed record Ds4VariantOption(Ds4OutputVariant Value, string Name, string Detail);

public sealed record Ds4ModeOption(Ds4RecognitionMode Value, string Name, string Detail);

/// <summary>Página PS4: monitoramento, bateria, luz e vibração do DualShock 4.</summary>
public sealed partial class Ds4ViewModel : ObservableObject
{
    private static readonly TimeSpan PulseDuration = TimeSpan.FromSeconds(1.5);

    /// <summary>Área do touchpad na tela (proporção real 1920 x 942).</summary>
    public const double PadWidth = 320;
    public const double PadHeight = PadWidth * Ds4Touch.Height / Ds4Touch.Width;
    private const double FingerSize = 22;

    private readonly IDs4Controller _ds4;
    private readonly IControllerAudio? _audio;
    private ControllerAudioEndpoints _endpoints = new(null, null);
    private IMicrophoneMeter? _meter;
    private readonly IInputReader? _xinput;
    private readonly ISettingsStore? _settings;
    private bool _loading = true;
    private uint _lastPacket = uint.MaxValue;
    private double _pitchZero;
    private double _rollZero;
    private Ds4Motion? _lastMotion;
    private int _tick;
    private Ds4LinkState? _lastLink;

    [ObservableProperty] private StatusKind _statusKind = StatusKind.Error;
    [ObservableProperty] private string _statusTitle = "";
    [ObservableProperty] private string _statusDescription = "";
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isLinked;
    [ObservableProperty] private bool _isBluetooth;
    [ObservableProperty] private double _leftX;
    [ObservableProperty] private double _leftY;
    [ObservableProperty] private double _rightX;
    [ObservableProperty] private double _rightY;
    [ObservableProperty] private double _leftTrigger;
    [ObservableProperty] private double _rightTrigger;
    [ObservableProperty] private bool _touchpad;
    [ObservableProperty] private string _batteryTitle = Presenter.Dash;
    [ObservableProperty] private string _batteryDetail = "";
    [ObservableProperty] private string _modeText = Presenter.Dash;
    [ObservableProperty] private int _reportsPerSecond;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private bool _advanced;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private double _lightR = 0x9B;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private double _lightG = 0x6B;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private double _lightB = 0xFF;

    [ObservableProperty] private Ds4VariantOption _selectedVariant;
    [ObservableProperty] private string _outputStatus = "Nenhum comando enviado ainda.";
    [ObservableProperty] private Ds4ModeOption _selectedMode;
    [ObservableProperty] private StatusKind _recognitionKind = StatusKind.Ok;
    [ObservableProperty] private string _recognitionText = "";

    // ===== Touchpad =====
    [ObservableProperty] private bool _sensorsAvailable;
    [ObservableProperty] private bool _finger1Active;
    [ObservableProperty] private double _finger1Left;
    [ObservableProperty] private double _finger1Top;
    [ObservableProperty] private bool _finger2Active;
    [ObservableProperty] private double _finger2Left;
    [ObservableProperty] private double _finger2Top;
    [ObservableProperty] private string _touchText = Presenter.Dash;

    // ===== Movimento =====
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PitchScale))]
    private double _pitch;
    [ObservableProperty] private double _roll;
    [ObservableProperty] private double _gyroXLevel;
    [ObservableProperty] private double _gyroYLevel;
    [ObservableProperty] private double _gyroZLevel;
    [ObservableProperty] private string _gyroText = Presenter.Dash;
    [ObservableProperty] private string _accelText = Presenter.Dash;
    [ObservableProperty] private string _tiltText = Presenter.Dash;

    // ===== Alto-falante =====
    [ObservableProperty] private StatusKind _speakerKind = StatusKind.Warning;
    [ObservableProperty] private string _speakerText = "Ainda não verificado.";
    [ObservableProperty] private bool _hasSpeaker;
    [ObservableProperty] private bool _hasMicrophone;
    [ObservableProperty] private bool _micActive;
    [ObservableProperty] private double _micLevel;
    [ObservableProperty] private bool _tonePlaying;

    // ===== O que o jogo vê (Xbox virtual lido pelo XInput) =====
    [ObservableProperty] private bool _gameViewActive;
    [ObservableProperty] private string _gameViewButtons = Presenter.Dash;
    [ObservableProperty] private string _gameViewSticks = Presenter.Dash;

    public Ds4ViewModel(IDs4Controller ds4, IInputReader? xinput = null, IControllerAudio? audio = null, ISettingsStore? settings = null)
    {
        _xinput = xinput;
        _settings = settings;
        _ds4 = ds4 ?? throw new ArgumentNullException(nameof(ds4));
        _audio = audio;
        _selectedVariant = Variants[0];
        _selectedMode = Modes.First(m => m.Value == ds4.Mode);
        _advanced = ds4.AdvancedEnabled;

        // Estado inicial vem das preferências salvas (o serviço já as recebeu na inicialização).
        if (settings?.Load().Ds4 is { } saved)
        {
            _selectedVariant = Variants.FirstOrDefault(v => v.Value == Ds4Preferences.ParseVariant(saved.OutputVariant)) ?? Variants[0];
            if (Ds4Preferences.ParseColor(saved.LightColor) is { } c)
                (_lightR, _lightG, _lightB) = (c.R, c.G, c.B);
        }
        _loading = false;

        var frame = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        frame.Tick += (_, _) => Refresh();
        frame.Start();
        Refresh();
    }

    public ButtonStates Buttons { get; } = new();

    public IReadOnlyList<Ds4VariantOption> Variants { get; } =
    [
        new(Ds4OutputVariant.LinuxControl, "A · Controle (Linux)", "HidD_SetOutputReport, 0xC0"),
        new(Ds4OutputVariant.LinuxInterrupt, "B · Interrupção (Linux)", "WriteFile, 0xC0"),
        new(Ds4OutputVariant.Ds4WindowsControl, "C · Controle (DS4Windows)", "HidD_SetOutputReport, 0x84 + 0xF7"),
        new(Ds4OutputVariant.Ds4WindowsInterrupt, "D · Interrupção (DS4Windows)", "WriteFile, 0x84 + 0xF7"),
    ];

    public IReadOnlyList<Ds4ModeOption> Modes { get; } =
    [
        new(Ds4RecognitionMode.PlayStation, "PlayStation (nativo)", "Jogos e Steam com suporte a PS4 usam o controle direto."),
        new(Ds4RecognitionMode.Xbox, "Xbox (XInput emulado)", "O app cria um Xbox 360 virtual. 🖥️ Só funciona com o app aberto."),
    ];

    public bool CanSend => Advanced && IsLinked;

    /// <summary>Achata o desenho conforme a inclinação frente/trás (cos do ângulo), mínimo de 15%.</summary>
    public double PitchScale => Math.Max(0.15, Math.Abs(Math.Cos(Pitch * Math.PI / 180)));

    partial void OnSelectedModeChanged(Ds4ModeOption value)
    {
        if (value is null) return;
        _ds4.Mode = value.Value;
        SavePreferences();
    }

    partial void OnSelectedVariantChanged(Ds4VariantOption value) => SavePreferences();

    /// <summary>Grava as escolhas da página. A cor só é gravada quando aplicada (não a cada movimento do slider).</summary>
    private void SavePreferences(Ds4Color? appliedLight = null)
    {
        if (_loading || _settings is null) return;
        var current = _settings.Load();
        var ds4 = current.Ds4 with
        {
            AdvancedEnabled = Advanced,
            RecognitionMode = SelectedMode.Value.ToString(),
            OutputVariant = SelectedVariant.Value.ToString(),
            LightColor = appliedLight is { } c ? Ds4Preferences.FormatColor(c) : current.Ds4.LightColor,
        };
        _settings.Save(current with { Ds4 = ds4 });
    }

    public SolidColorBrush Preview => new(Color.FromRgb((byte)LightR, (byte)LightG, (byte)LightB));

    partial void OnAdvancedChanged(bool value)
    {
        _ds4.AdvancedEnabled = value;
        SavePreferences();
    }

    partial void OnIsLinkedChanged(bool value) => OnPropertyChanged(nameof(CanSend));

    private void Refresh()
    {
        var s = _ds4.Current;

        if (s.Link != _lastLink)
        {
            _lastLink = s.Link;
            var status = Ds4Presenter.Link(s);
            StatusKind = status.Kind;
            StatusTitle = status.Title;
            StatusDescription = status.Description;
            IsLinked = s.Link == Ds4LinkState.Connected;
            if (IsLinked) _ = CheckSpeakerAsync();
        }

        if (++_tick % 15 == 0)
        {
            ReportsPerSecond = s.ReportsPerSecond;
            (BatteryTitle, BatteryDetail) = Ds4Presenter.Battery(s, Advanced);
            ModeText = Ds4Presenter.Mode(s);
            (RecognitionKind, RecognitionText) = Ds4Presenter.Recognition(s);
        }

        IsBluetooth = s.IsBluetooth;
        if (_meter is not null) MicLevel = _meter.Level;
        RefreshGameView(s);
        IsActive = s.Link == Ds4LinkState.Connected && s.Input is not null;
        if (s.Input is null || s.Input.State.PacketNumber == _lastPacket) return;
        _lastPacket = s.Input.State.PacketNumber;

        var st = s.Input.State;
        LeftX = st.LeftStick.X;
        LeftY = st.LeftStick.Y;
        RightX = st.RightStick.X;
        RightY = st.RightStick.Y;
        LeftTrigger = st.LeftTrigger;
        RightTrigger = st.RightTrigger;
        Touchpad = s.Input.TouchpadClick;
        Buttons.Set(st.Buttons);
        ApplySensors(s.Input);
    }

    /// <summary>Lê o Xbox virtual exatamente como um jogo XInput leria.</summary>
    private void RefreshGameView(Ds4Snapshot s)
    {
        var state = s.VirtualActive && s.VirtualSlot is int slot ? _xinput?.Read(slot) : null;
        GameViewActive = state is not null;
        if (state is null) return;
        GameViewButtons = state.Buttons == GamepadButtons.None ? "nenhum botão" : state.Buttons.ToString();
        GameViewSticks = string.Create(CultureInfo.InvariantCulture,
            $"L ({state.LeftStick.X,5:0.00}, {state.LeftStick.Y,5:0.00})  R ({state.RightStick.X,5:0.00}, {state.RightStick.Y,5:0.00})  L2 {state.LeftTriggerRaw,3}  R2 {state.RightTriggerRaw,3}");
    }

    private void ApplySensors(Ds4Input input)
    {
        SensorsAvailable = input.Motion is not null;
        if (input.Touch is { } t)
        {
            (Finger1Active, Finger1Left, Finger1Top) = Place(t.First);
            (Finger2Active, Finger2Left, Finger2Top) = Place(t.Second);
            TouchText = (t.First.Active, t.Second.Active) switch
            {
                (false, false) => "Nenhum dedo no touchpad",
                (true, false) => Point(t.First),
                (false, true) => Point(t.Second),
                _ => $"2 dedos · {Point(t.First)} · {Point(t.Second)}",
            };
        }

        if (input.Motion is { } m)
        {
            _lastMotion = m;
            Pitch = m.PitchDegrees - _pitchZero;
            Roll = m.RollDegrees - _rollZero;
            GyroXLevel = Level(m.GyroX);
            GyroYLevel = Level(m.GyroY);
            GyroZLevel = Level(m.GyroZ);
            GyroText = string.Create(CultureInfo.InvariantCulture, $"X {m.GyroX,6}   Y {m.GyroY,6}   Z {m.GyroZ,6}");
            AccelText = string.Create(CultureInfo.InvariantCulture, $"X {m.AccelX,6}   Y {m.AccelY,6}   Z {m.AccelZ,6}");
            TiltText = string.Create(CultureInfo.InvariantCulture, $"Frente/trás {Pitch,6:0}°    Lateral {Roll,6:0}°");
        }
    }

    private static (bool Active, double Left, double Top) Place(Ds4TouchPoint p) =>
        (p.Active, (p.X * PadWidth / Ds4Touch.Width) - (FingerSize / 2), (p.Y * PadHeight / Ds4Touch.Height) - (FingerSize / 2));

    private static string Point(Ds4TouchPoint p) => string.Create(CultureInfo.InvariantCulture, $"dedo {p.Id}: ({p.X}, {p.Y})");

    /// <summary>Intensidade 0–1 da rotação para as barras (valor bruto, sem calibração).</summary>
    public static double Level(short gyro) => Math.Min(1.0, Math.Abs((int)gyro) / 8000.0);

    /// <summary>Define a posição atual como "nivelado".</summary>
    [RelayCommand]
    private void ZeroTilt()
    {
        if (_lastMotion is not { } m) return;
        _pitchZero = m.PitchDegrees;
        _rollZero = m.RollDegrees;
        Pitch = 0;
        Roll = 0;
    }

    [RelayCommand]
    private async Task CheckSpeakerAsync()
    {
        if (_audio is null)
        {
            (SpeakerKind, SpeakerText) = (StatusKind.Warning, "Verificação de áudio indisponível.");
            return;
        }
        try
        {
            var audio = _audio;
            _endpoints = await Task.Run(() => audio.Find());
            HasSpeaker = _endpoints.Speaker is not null;
            HasMicrophone = _endpoints.Microphone is not null;
            (SpeakerKind, SpeakerText) = Presentation.Ds4Presenter.Audio(_endpoints, IsBluetooth);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            (SpeakerKind, SpeakerText) = (StatusKind.Error, $"Não foi possível consultar os dispositivos de áudio ({ex.GetType().Name}).");
        }
    }

    /// <summary>Tom de teste de 1 s no alto-falante do controle.</summary>
    [RelayCommand]
    private async Task PlayToneAsync()
    {
        if (_audio is null || _endpoints.Speaker is not { } speaker || TonePlaying) return;
        TonePlaying = true;
        try
        {
            await _audio.PlayToneAsync(speaker, 440, 1.0, 0.3f);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            (SpeakerKind, SpeakerText) = (StatusKind.Error, $"Falha ao tocar o som de teste ({ex.GetType().Name}).");
        }
        finally
        {
            TonePlaying = false;
        }
    }

    /// <summary>Liga/desliga o medidor do microfone do controle.</summary>
    [RelayCommand]
    private void ToggleMicrophone()
    {
        if (_meter is not null)
        {
            _meter.Dispose();
            _meter = null;
            MicActive = false;
            MicLevel = 0;
            return;
        }
        if (_audio is null || _endpoints.Microphone is not { } mic) return;
        try
        {
            _meter = _audio.StartMeter(mic);
            MicActive = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            (SpeakerKind, SpeakerText) = (StatusKind.Error, $"Falha ao abrir o microfone ({ex.GetType().Name}).");
        }
    }
    [RelayCommand]
    private void Preset(string hex)
    {
        var rgb = Convert.FromHexString(hex);
        LightR = rgb[0];
        LightG = rgb[1];
        LightB = rgb[2];
        ApplyLight();
    }

    [RelayCommand]
    private void ApplyLight()
    {
        var color = new Ds4Color((byte)LightR, (byte)LightG, (byte)LightB);
        Report("Luz", _ds4.SetLight(color, SelectedVariant.Value));
        SavePreferences(color);
    }

    [RelayCommand]
    private void LightOff()
    {
        LightR = LightG = LightB = 0;
        ApplyLight();
    }

    [RelayCommand]
    private async Task Pulse(string which)
    {
        byte left = which is "left" or "both" ? (byte)200 : (byte)0;
        byte right = which is "right" or "both" ? (byte)200 : (byte)0;
        var ok = _ds4.SetRumble(left, right, SelectedVariant.Value);
        Report("Vibração", ok);
        if (!ok) return;
        await Task.Delay(PulseDuration);
        _ds4.SetRumble(0, 0, SelectedVariant.Value);
    }

    private void Report(string what, bool ok)
    {
        var when = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        OutputStatus = !CanSend
            ? "Ligue \"Recursos avançados\" com o controle conectado para enviar comandos."
            : ok
                ? $"{when} · {what} enviada via {SelectedVariant.Name}. O Windows aceitou; confira no controle."
                : $"{when} · {what}: o envio falhou via {SelectedVariant.Name}.";
    }
}
