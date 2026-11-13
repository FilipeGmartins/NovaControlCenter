using HidSharp;
using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Interfaces;

namespace NovaControlCenter.Infrastructure.DeviceDetection;

/// <summary>
/// <see cref="IDevicePresence"/> sobre a lista HID do Windows (HidSharp).
/// Apenas enumera; não abre nenhum dispositivo.
/// </summary>
public sealed class HidDevicePresence : IDevicePresence, IDisposable
{
    private readonly DeviceList _list = DeviceList.Local;

    public HidDevicePresence() => _list.Changed += OnChanged;

    public event EventHandler? DevicesChanged;

    public IReadOnlySet<UsbDeviceId> GetPresentDevices(ushort vendorId) =>
        _list.GetHidDevices(vendorId)
            .Select(d => new UsbDeviceId((ushort)d.VendorID, (ushort)d.ProductID))
            .ToHashSet();

    public void Dispose() => _list.Changed -= OnChanged;

    private void OnChanged(object? sender, DeviceListChangedEventArgs e) => DevicesChanged?.Invoke(this, EventArgs.Empty);
}
