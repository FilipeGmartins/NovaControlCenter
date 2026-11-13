using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;

namespace NovaControlCenter.App.ViewModels;

/// <summary>Monitoramento em tempo real das entradas + teste de vibração.</summary>
public sealed partial class MonitorViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan PulseDuration = TimeSpan.FromSeconds(1.5);

    private readonly InputMonitor _input;
    private readonly ConnectionMonitor _connection;
    private readonly IRumbleService _rumble;
    private uint _lastPacket = uint.MaxValue;
    private int _rateTick;
    private CancellationTokenSource? _pulse;

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private double _leftX;
    [ObservableProperty] private double _leftY;
    [ObservableProperty] private double _rightX;
    [ObservableProperty] private double _rightY;
    [ObservableProperty] private double _leftTrigger;
    [ObservableProperty] private double _rightTrigger;
    [ObservableProperty] private string _leftStickText = "";
    [ObservableProperty] private string _rightStickText = "";
    [ObservableProperty] private string _triggersText = "";
    [ObservableProperty] private string _buttonsText = "";
    [ObservableProperty] private uint _packetNumber;
    [ObservableProperty] private int _updatesPerSecond;
    [ObservableProperty] private string _missingButtons = "";

    [ObservableProperty] private double _leftMotor = 60;
    [ObservableProperty] private double _rightMotor = 60;
    [ObservableProperty] private bool _continuousVibration;
    [ObservableProperty] private string _vibrationStatus = "Parada";

    public MonitorViewModel(InputMonitor input, ConnectionMonitor connection, IRumbleService rumble)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _rumble = rumble ?? throw new ArgumentNullException(nameof(rumble));
        UpdateMissing();

        // Atualiza a tela a ~60 fps lendo o último estado (sem inundar o Dispatcher com eventos).
        var frame = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        frame.Tick += (_, _) => Refresh();
        frame.Start();
    }

    /// <summary>Botões pressionados agora.</summary>
    public ButtonStates Buttons { get; } = new();

    /// <summary>Botões já detectados desde o último "Reiniciar teste" — para teste manual.</summary>
    public ButtonStates Tested { get; } = new();

    public int TotalButtons => ButtonStates.All.Length;

    private int? Slot => _connection.Current.Controller?.XInputSlot;

    private void Refresh()
    {
        if (++_rateTick % 15 == 0) UpdatesPerSecond = _input.UpdatesPerSecond;

        var sample = _input.Latest;
        IsActive = sample is not null;
        if (sample is null || sample.State.PacketNumber == _lastPacket) return;
        _lastPacket = sample.State.PacketNumber;

        var s = sample.State;
        PacketNumber = s.PacketNumber;
        LeftX = s.LeftStick.X;
        LeftY = s.LeftStick.Y;
        RightX = s.RightStick.X;
        RightY = s.RightStick.Y;
        LeftTrigger = s.LeftTrigger;
        RightTrigger = s.RightTrigger;
        LeftStickText = Format(s.LeftStick);
        RightStickText = Format(s.RightStick);
        TriggersText = string.Create(CultureInfo.InvariantCulture, $"L2 {s.LeftTriggerRaw,3}/255   R2 {s.RightTriggerRaw,3}/255");
        ButtonsText = s.Buttons == GamepadButtons.None
            ? "nenhum"
            : string.Create(CultureInfo.InvariantCulture, $"0x{(ushort)s.Buttons:X4}  {s.Buttons}");

        Buttons.Set(s.Buttons);
        var before = Tested.Value;
        Tested.Add(s.Buttons);
        if (Tested.Value != before) UpdateMissing();
    }

    private static string Format(StickState stick) => string.Create(CultureInfo.InvariantCulture,
        $"X {stick.RawX,6} ({stick.X,5:0.00})   Y {stick.RawY,6} ({stick.Y,5:0.00})");

    private void UpdateMissing()
    {
        var missing = ButtonStates.All.Where(b => (Tested.Value & b) != b).Select(Name).ToList();
        MissingButtons = missing.Count == 0 ? "Todos os botões foram detectados ✓" : "Ainda não detectados: " + string.Join(", ", missing);
    }

    [RelayCommand]
    private void ResetTest()
    {
        Tested.Set(GamepadButtons.None);
        UpdateMissing();
    }

    // ===== Vibração =====
    [RelayCommand] private Task PulseLeft() => PulseAsync(LeftMotor, 0, "Motor esquerdo");
    [RelayCommand] private Task PulseRight() => PulseAsync(0, RightMotor, "Motor direito");
    [RelayCommand] private Task PulseBoth() => PulseAsync(LeftMotor, RightMotor, "Ambos os motores");

    [RelayCommand]
    public void StopVibration()
    {
        _pulse?.Cancel();
        ContinuousVibration = false;
        if (Slot is int slot) _rumble.TrySetVibration(slot, 0, 0);
        VibrationStatus = "Parada";
    }

    public void Dispose()
    {
        StopVibration();
        _pulse?.Dispose();
        _pulse = null;
    }

    partial void OnLeftMotorChanged(double value) => ApplyContinuous();
    partial void OnRightMotorChanged(double value) => ApplyContinuous();

    partial void OnContinuousVibrationChanged(bool value)
    {
        if (value) ApplyContinuous();
        else if (Slot is int slot)
        {
            _rumble.TrySetVibration(slot, 0, 0);
            VibrationStatus = "Parada";
        }
    }

    private void ApplyContinuous()
    {
        if (!ContinuousVibration || Slot is not int slot) return;
        var ok = _rumble.TrySetVibration(slot, LeftMotor / 100, RightMotor / 100);
        VibrationStatus = ok
            ? string.Create(CultureInfo.InvariantCulture, $"Contínua — esquerdo {LeftMotor:0}%, direito {RightMotor:0}%")
            : "Falha ao enviar comando de vibração";
    }

    private async Task PulseAsync(double left, double right, string label)
    {
        if (Slot is not int slot)
        {
            VibrationStatus = "Controle não conectado";
            return;
        }
        ContinuousVibration = false;
        _pulse?.Cancel();
        _pulse?.Dispose();
        var cts = _pulse = new CancellationTokenSource();

        var ok = _rumble.TrySetVibration(slot, left / 100, right / 100);
        VibrationStatus = ok ? $"{label} vibrando…" : "Falha ao enviar comando de vibração";
        try
        {
            await Task.Delay(PulseDuration, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        _rumble.TrySetVibration(slot, 0, 0);
        VibrationStatus = $"{label}: pulso concluído";
    }

    private static string Name(GamepadButtons b) => b switch
    {
        GamepadButtons.DPadUp => "D-pad ↑",
        GamepadButtons.DPadDown => "D-pad ↓",
        GamepadButtons.DPadLeft => "D-pad ←",
        GamepadButtons.DPadRight => "D-pad →",
        GamepadButtons.LeftThumb => "L3",
        GamepadButtons.RightThumb => "R3",
        GamepadButtons.LeftShoulder => "LB",
        GamepadButtons.RightShoulder => "RB",
        GamepadButtons.Guide => "Home",
        GamepadButtons.Back => "Select",
        _ => b.ToString(),
    };
}
