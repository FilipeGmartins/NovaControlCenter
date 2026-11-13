using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Infrastructure.XInput;

/// <summary><see cref="IInputReader"/> sobre xinput1_4.dll. Somente leitura.</summary>
public sealed class XInputReader : IInputReader
{
    // Ordinais não documentados: detectados uma vez; se ausentes, caímos para as funções públicas.
    private bool _stateExAvailable = true;
    private bool _capabilitiesExAvailable = true;

    public IReadOnlyList<XInputSlot> GetConnectedSlots()
    {
        var slots = new List<XInputSlot>(XInputNative.MaxControllers);
        for (uint i = 0; i < XInputNative.MaxControllers; i++)
        {
            if (XInputNative.GetState(i, out _) != XInputNative.ErrorSuccess) continue;
            slots.Add(new XInputSlot((int)i, TryGetDeviceId(i)));
        }
        return slots;
    }

    public ControllerState? Read(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, XInputNative.MaxControllers);

        if (ReadRaw((uint)slot, out var raw) != XInputNative.ErrorSuccess) return null;
        var pad = raw.Gamepad;
        return new ControllerState(
            raw.PacketNumber,
            (GamepadButtons)pad.Buttons,
            pad.LeftTrigger,
            pad.RightTrigger,
            new StickState(pad.ThumbLX, pad.ThumbLY),
            new StickState(pad.ThumbRX, pad.ThumbRY));
    }

    private uint ReadRaw(uint slot, out XInputNative.XInputState state)
    {
        if (_stateExAvailable)
        {
            try { return XInputNative.GetStateEx(slot, out state); }
            catch (EntryPointNotFoundException) { _stateExAvailable = false; }
        }
        return XInputNative.GetState(slot, out state);
    }

    private UsbDeviceId? TryGetDeviceId(uint slot)
    {
        if (!_capabilitiesExAvailable) return null;
        try
        {
            return XInputNative.GetCapabilitiesEx(1, slot, 0, out var caps) == XInputNative.ErrorSuccess
                ? new UsbDeviceId(caps.VendorId, caps.ProductId)
                : null;
        }
        catch (EntryPointNotFoundException)
        {
            _capabilitiesExAvailable = false;
            return null;
        }
    }
}
