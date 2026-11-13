using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Interfaces;

/// <summary>Slot XInput ocupado. <see cref="DeviceId"/> é null quando o VID/PID não pôde ser obtido.</summary>
public sealed record XInputSlot(int Index, UsbDeviceId? DeviceId);

/// <summary>Leitura de entradas do controle (implementado sobre XInput).</summary>
public interface IInputReader
{
    /// <summary>Slots atualmente ocupados.</summary>
    IReadOnlyList<XInputSlot> GetConnectedSlots();

    /// <summary>Lê o estado do slot; null se o slot não estiver conectado.</summary>
    ControllerState? Read(int slot);
}

/// <summary>Detecção de dispositivos USB/HID presentes (implementado sobre HID).</summary>
public interface IDevicePresence
{
    /// <summary>VID/PIDs distintos presentes para o fabricante informado.</summary>
    IReadOnlySet<UsbDeviceId> GetPresentDevices(ushort vendorId);

    /// <summary>Disparado quando o Windows informa mudança na lista de dispositivos (pode ser em outra thread).</summary>
    event EventHandler? DevicesChanged;
}
