using System.Globalization;

namespace NovaControlCenter.Infrastructure.Hid;

/// <summary>Descrição legível de usages HID (page &lt;&lt; 16 | usage).</summary>
internal static class HidUsageText
{
    public static string Describe(uint usage)
    {
        var page = (ushort)(usage >> 16);
        var id = (ushort)(usage & 0xFFFF);
        var hex = string.Create(CultureInfo.InvariantCulture, $"0x{page:X4}:0x{id:X4}");
        return $"{hex} {Name(page, id)}";
    }

    internal static string Name(ushort page, ushort id) => (page, id) switch
    {
        (0x01, 0x02) => "Generic Desktop / Mouse",
        (0x01, 0x04) => "Generic Desktop / Joystick",
        (0x01, 0x05) => "Generic Desktop / Gamepad",
        (0x01, 0x06) => "Generic Desktop / Keyboard",
        (0x01, 0x08) => "Generic Desktop / Multi-axis Controller",
        (0x01, 0x80) => "Generic Desktop / System Control",
        (0x0C, 0x01) => "Consumer / Consumer Control",
        (0x01, _) => "Generic Desktop",
        (0x06, _) => "Generic Device Controls",
        (0x0C, _) => "Consumer",
        (0x84, _) => "Power Device",
        (0x85, _) => "Battery System",
        (>= 0xFF00, _) => "Vendor-defined",
        _ => "?",
    };
}
