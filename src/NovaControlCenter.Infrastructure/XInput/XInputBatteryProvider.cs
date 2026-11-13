using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Infrastructure.XInput;

/// <summary>
/// Bateria via XInputGetBatteryInformation. Para o T4 Nova Lite o receptor responde WIRED, então
/// este provider devolve null (sem dado). Mantido porque é a fonte oficial caso o dispositivo
/// passe a informar ALKALINE/NIMH/UNKNOWN.
/// </summary>
public sealed class XInputBatteryProvider : IBatteryProvider
{
    public string Name => "XInput";

    public BatteryStatus? TryGetStatus(ControllerInfo controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (controller.XInputSlot is not int slot) return null;
        if (XInputNative.GetBatteryInformation((uint)slot, XInputNative.BatteryDevTypeGamepad, out var info) != XInputNative.ErrorSuccess)
            return null;
        return Map(info.BatteryType, info.BatteryLevel, Name);
    }

    /// <summary>WIRED, DISCONNECTED e códigos desconhecidos não carregam informação de bateria.</summary>
    internal static BatteryStatus? Map(byte type, byte level, string source)
    {
        if (type is not (0x02 or 0x03 or 0xFF)) return null;
        return level switch
        {
            0x00 => new BatteryStatus.Level(BatteryLevel.Empty, source),
            0x01 => new BatteryStatus.Level(BatteryLevel.Low, source),
            0x02 => new BatteryStatus.Level(BatteryLevel.Medium, source),
            0x03 => new BatteryStatus.Level(BatteryLevel.Full, source),
            _ => null,
        };
    }
}
