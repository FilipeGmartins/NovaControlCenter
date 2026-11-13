using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Services;

/// <summary>Resultado instantâneo (sem debounce) da resolução de conexão.</summary>
public sealed record ResolvedConnection(ConnectionState State, ControllerInfo? Controller);

/// <summary>
/// Regra pura que converte "dispositivos presentes + slots XInput" em estado de conexão.
/// Ver HARDWARE_DIAGNOSTICS §3.5: 0x1040 = controle conectado, 0x2106 = receptor sem controle.
/// </summary>
public static class ConnectionResolver
{
    public const string DisplayName = "GameSir T4 Nova Lite";
    public const string ConnectionType = "Receptor USB 2.4 GHz";

    public static ResolvedConnection Resolve(IReadOnlySet<UsbDeviceId> presentGameSir, IReadOnlyList<XInputSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(presentGameSir);
        ArgumentNullException.ThrowIfNull(slots);

        if (presentGameSir.Contains(KnownDevices.NovaLiteConnected))
        {
            var (slot, match) = MatchSlot(slots);
            var confidence = KnownDevices.Find(KnownDevices.NovaLiteConnected)?.Confidence ?? Confidence.Hypothesis;
            var info = new ControllerInfo(KnownDevices.NovaLiteConnected, DisplayName, ConnectionType, slot, match, confidence);
            return new ResolvedConnection(ConnectionState.Connected, info);
        }

        if (presentGameSir.Contains(KnownDevices.NovaLiteReceiverIdle))
            return new ResolvedConnection(ConnectionState.ControllerOff, null);

        return new ResolvedConnection(ConnectionState.ReceiverNotFound, null);
    }

    /// <summary>
    /// 1) Slot cujo VID/PID é o do receptor → exato.
    /// 2) Senão, um único slot sem VID/PID conhecido → aproximado.
    ///    Slots identificados como outro dispositivo (ex.: pad virtual 045E:028E) nunca são escolhidos.
    /// </summary>
    internal static (int? Slot, SlotMatch Match) MatchSlot(IReadOnlyList<XInputSlot> slots)
    {
        var exact = slots.FirstOrDefault(s => s.DeviceId == KnownDevices.NovaLiteConnected);
        if (exact is not null) return (exact.Index, SlotMatch.Exact);

        var unknown = slots.Where(s => s.DeviceId is null).ToList();
        return unknown.Count == 1 ? (unknown[0].Index, SlotMatch.Approximate) : (null, SlotMatch.None);
    }
}
