using NovaControlCenter.Core.Devices;

namespace NovaControlCenter.Core.Models;

/// <summary>Estados de conexão observáveis (ver HARDWARE_DIAGNOSTICS §3.5).</summary>
public enum ConnectionState
{
    /// <summary>Nenhum receptor GameSir presente.</summary>
    ReceiverNotFound,

    /// <summary>Receptor presente como PID 0x2106: controle desligado ou fora de alcance.</summary>
    ControllerOff,

    /// <summary>Receptor presente como PID 0x1040: controle conectado.</summary>
    Connected,
}

/// <summary>Como o slot XInput foi associado ao receptor.</summary>
public enum SlotMatch
{
    /// <summary>Nenhum slot XInput associado (ex.: XInput ainda não registrou o controle).</summary>
    None,

    /// <summary>VID/PID do slot confirmado via XInputGetCapabilitiesEx.</summary>
    Exact,

    /// <summary>VID/PID indisponível; slot escolhido por eliminação.</summary>
    Approximate,
}

/// <summary>Identificação do controle conectado.</summary>
public sealed record ControllerInfo(
    UsbDeviceId DeviceId,
    string DisplayName,
    string ConnectionType,
    int? XInputSlot,
    SlotMatch SlotMatch,
    Confidence ModelConfidence);

/// <summary>Estado de conexão publicado para a interface.</summary>
public sealed record ConnectionSnapshot(
    ConnectionState State,
    ControllerInfo? Controller,
    DateTimeOffset? ConnectedSince)
{
    public static readonly ConnectionSnapshot NotFound = new(ConnectionState.ReceiverNotFound, null, null);
}
