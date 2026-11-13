using System.Globalization;

namespace NovaControlCenter.Core.Devices;

/// <summary>Identificador USB (Vendor ID + Product ID).</summary>
public readonly record struct UsbDeviceId(ushort VendorId, ushort ProductId)
{
    /// <summary>Formato canônico "VVVV:PPPP" em hexadecimal maiúsculo, ex.: "3537:1040".</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{VendorId:X4}:{ProductId:X4}");
}
