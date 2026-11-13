using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Infrastructure.Virtual;

/// <summary>
/// Controle Xbox 360 virtual via ViGEmBus (ADR-24). O app não instala drivers: se o ViGEmBus não existir,
/// <see cref="TryCreate"/> devolve null com o motivo.
/// </summary>
public sealed class VirtualXboxPad : IDisposable
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _pad;
    private ControllerState? _last;

    private VirtualXboxPad(ViGEmClient client, IXbox360Controller pad)
    {
        _client = client;
        _pad = pad;
        _pad.AutoSubmitReport = false;
        _pad.FeedbackReceived += (_, e) => RumbleRequested?.Invoke(this, (e.LargeMotor, e.SmallMotor));
    }

    /// <summary>Vibração pedida pelo jogo: (motor grande/esquerdo, motor pequeno/direito).</summary>
    public event EventHandler<(byte Large, byte Small)>? RumbleRequested;

    /// <summary>Slot XInput atribuído pelo Windows (0–3), ou null se ainda não informado.</summary>
    public int? UserIndex
    {
        get
        {
            try { return _pad.UserIndex; }
            catch (Exception ex) when (ex is Nefarius.ViGEm.Client.Targets.Xbox360.Exceptions.Xbox360UserIndexNotReportedException or VigemTargetNotPluggedInException)
            {
                return null;
            }
        }
    }

    public static VirtualXboxPad? TryCreate(out string? error)
    {
        ViGEmClient? client = null;
        try
        {
            client = new ViGEmClient();
            var pad = client.CreateXbox360Controller();
            pad.Connect();
            error = null;
            return new VirtualXboxPad(client, pad);
        }
        catch (VigemBusNotFoundException)
        {
            client?.Dispose();
            error = "ViGEmBus não encontrado. Instale o driver ViGEmBus para usar o modo Xbox.";
            return null;
        }
        catch (Exception ex) when (ex is VigemBusVersionMismatchException or VigemBusAccessFailedException or VigemNoFreeSlotException or VigemAllocFailedException)
        {
            client?.Dispose();
            error = $"Não foi possível criar o controle Xbox virtual ({ex.GetType().Name}).";
            return null;
        }
    }

    /// <summary>Envia o estado ao controle virtual (só quando muda, para não gerar tráfego desnecessário).</summary>
    public void Update(ControllerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_last is { } prev && SameInputs(prev, state)) return;
        _last = state;
        var report = Map(state);
        _pad.SetButtonsFull(report.Buttons);
        _pad.LeftTrigger = report.LeftTrigger;
        _pad.RightTrigger = report.RightTrigger;
        _pad.LeftThumbX = report.LeftThumbX;
        _pad.LeftThumbY = report.LeftThumbY;
        _pad.RightThumbX = report.RightThumbX;
        _pad.RightThumbY = report.RightThumbY;
        _pad.SubmitReport();
    }

    public void Dispose()
    {
        try { _pad.Disconnect(); }
        catch (VigemTargetNotPluggedInException) { /* já desconectado */ }
        _client.Dispose();
    }

    /// <summary>Relatório no formato XInput. GamepadButtons já usa o layout de bits do XInput.</summary>
    internal readonly record struct XboxReport(ushort Buttons, byte LeftTrigger, byte RightTrigger,
        short LeftThumbX, short LeftThumbY, short RightThumbX, short RightThumbY);

    internal static XboxReport Map(ControllerState s) => new(
        (ushort)s.Buttons, s.LeftTriggerRaw, s.RightTriggerRaw,
        s.LeftStick.RawX, s.LeftStick.RawY, s.RightStick.RawX, s.RightStick.RawY);

    internal static bool SameInputs(ControllerState a, ControllerState b) =>
        a.Buttons == b.Buttons && a.LeftTriggerRaw == b.LeftTriggerRaw && a.RightTriggerRaw == b.RightTriggerRaw &&
        a.LeftStick == b.LeftStick && a.RightStick == b.RightStick;
}
