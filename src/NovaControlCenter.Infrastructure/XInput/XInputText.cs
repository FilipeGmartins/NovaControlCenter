namespace NovaControlCenter.Infrastructure.XInput;

/// <summary>Tradução dos códigos XInput para texto legível no diagnóstico.</summary>
internal static class XInputText
{
    public static string BatteryType(byte type) => type switch
    {
        0x00 => "DISCONNECTED",
        0x01 => "WIRED (sem informação de bateria)",
        0x02 => "ALKALINE",
        0x03 => "NIMH",
        0xFF => "UNKNOWN",
        _ => $"código desconhecido 0x{type:X2}",
    };

    /// <summary>XInput só informa 4 níveis — nunca uma porcentagem.</summary>
    public static string BatteryLevel(byte type, byte level)
    {
        // Para WIRED/DISCONNECTED o nível não tem significado.
        if (type is 0x00 or 0x01) return "n/a";
        return level switch
        {
            0x00 => "EMPTY",
            0x01 => "LOW",
            0x02 => "MEDIUM",
            0x03 => "FULL",
            _ => $"código desconhecido 0x{level:X2}",
        };
    }

    public static string SubType(byte subType) => subType switch
    {
        0x00 => "UNKNOWN",
        0x01 => "GAMEPAD",
        0x02 => "WHEEL",
        0x03 => "ARCADE_STICK",
        0x04 => "FLIGHT_STICK",
        0x05 => "DANCE_PAD",
        0x06 => "GUITAR",
        0x07 => "GUITAR_ALTERNATE",
        0x08 => "DRUM_KIT",
        0x0B => "GUITAR_BASS",
        0x13 => "ARCADE_PAD",
        _ => $"0x{subType:X2}",
    };
}
