using NovaControlCenter.Core.Devices;

namespace NovaControlCenter.Infrastructure.XInput;

public sealed record XInputSlotReport(
    int Slot,
    bool Connected,
    string? SubType,
    ushort? Flags,
    UsbDeviceId? DeviceId,
    string? DeviceIdError,
    string BatteryType,
    string BatteryLevel,
    uint? PacketNumber,
    ushort? Buttons,
    byte? LeftTrigger,
    byte? RightTrigger,
    short? ThumbLX,
    short? ThumbLY,
    short? ThumbRX,
    short? ThumbRY);

/// <summary>Leitura somente-leitura dos 4 slots XInput para diagnóstico.</summary>
public static class XInputDiagnostics
{
    public static IReadOnlyList<XInputSlotReport> ScanSlots()
    {
        var reports = new List<XInputSlotReport>(XInputNative.MaxControllers);
        for (uint slot = 0; slot < XInputNative.MaxControllers; slot++)
        {
            reports.Add(ScanSlot(slot));
        }
        return reports;
    }

    public static XInputSlotReport ScanSlot(uint slot)
    {
        if (XInputNative.GetStateEx(slot, out var state) != XInputNative.ErrorSuccess)
        {
            return new XInputSlotReport((int)slot, false, null, null, null, null,
                "n/a", "n/a", null, null, null, null, null, null, null, null);
        }

        string? subType = null;
        ushort? flags = null;
        if (XInputNative.GetCapabilities(slot, 0, out var caps) == XInputNative.ErrorSuccess)
        {
            subType = XInputText.SubType(caps.SubType);
            flags = caps.Flags;
        }

        UsbDeviceId? deviceId = null;
        string? deviceIdError = null;
        try
        {
            var result = XInputNative.GetCapabilitiesEx(1, slot, 0, out var capsEx);
            if (result == XInputNative.ErrorSuccess)
                deviceId = new UsbDeviceId(capsEx.VendorId, capsEx.ProductId);
            else
                deviceIdError = $"XInputGetCapabilitiesEx retornou {result}";
        }
        catch (EntryPointNotFoundException)
        {
            deviceIdError = "Ordinal #108 indisponível nesta versão do xinput1_4.dll";
        }

        string batteryType = "erro";
        string batteryLevel = "erro";
        if (XInputNative.GetBatteryInformation(slot, XInputNative.BatteryDevTypeGamepad, out var battery) == XInputNative.ErrorSuccess)
        {
            batteryType = XInputText.BatteryType(battery.BatteryType);
            batteryLevel = XInputText.BatteryLevel(battery.BatteryType, battery.BatteryLevel);
        }

        var pad = state.Gamepad;
        return new XInputSlotReport((int)slot, true, subType, flags, deviceId, deviceIdError,
            batteryType, batteryLevel, state.PacketNumber, pad.Buttons, pad.LeftTrigger, pad.RightTrigger,
            pad.ThumbLX, pad.ThumbLY, pad.ThumbRX, pad.ThumbRY);
    }
}
