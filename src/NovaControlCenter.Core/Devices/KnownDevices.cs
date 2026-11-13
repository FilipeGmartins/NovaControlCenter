namespace NovaControlCenter.Core.Devices;

/// <summary>Grau de confiança de uma identificação de hardware.</summary>
public enum Confidence
{
    /// <summary>Observado no histórico PnP, mas ainda não validado com o controle em mãos.</summary>
    Hypothesis,

    /// <summary>Validado em teste com o controle real (registrado em HARDWARE_DIAGNOSTICS.md).</summary>
    Confirmed,
}

public sealed record KnownDevice(UsbDeviceId Id, string Description, Confidence Confidence);

/// <summary>
/// Dispositivos conhecidos. Só entra aqui o que foi observado de fato.
/// Fonte: docs/HARDWARE_DIAGNOSTICS.md.
/// </summary>
public static class KnownDevices
{
    public const ushort GameSirVendorId = 0x3537;

    /// <summary>PID do receptor com o controle conectado (composto: XInput + HID vendor).</summary>
    public static readonly UsbDeviceId NovaLiteConnected = new(GameSirVendorId, 0x1040);

    /// <summary>PID do receptor sem controle (somente HID vendor).</summary>
    public static readonly UsbDeviceId NovaLiteReceiverIdle = new(GameSirVendorId, 0x2106);

    /// <summary>VID/PID do Xbox 360 com fio da Microsoft, também usado por pads virtuais (ViGEmBus).</summary>
    public static readonly UsbDeviceId Xbox360Wired = new(0x045E, 0x028E);

    public static readonly IReadOnlyList<KnownDevice> All =
    [
        new(new UsbDeviceId(GameSirVendorId, 0x1040),
            "Receptor GameSir composto (XInput + HID vendor) — candidato T4 Nova Lite",
            Confidence.Hypothesis),
        new(new UsbDeviceId(GameSirVendorId, 0x2106),
            "Receptor GameSir (Zikway) sem controle conectado — somente HID vendor",
            Confidence.Confirmed),
    ];

    public static bool IsGameSir(UsbDeviceId id) => id.VendorId == GameSirVendorId;

    public static KnownDevice? Find(UsbDeviceId id) => All.FirstOrDefault(d => d.Id == id);
}
