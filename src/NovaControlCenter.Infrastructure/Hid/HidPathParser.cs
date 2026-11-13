using System.Globalization;
using System.Text.RegularExpressions;

namespace NovaControlCenter.Infrastructure.Hid;

/// <summary>Extrai metadados do caminho de dispositivo do Windows (ex.: \\?\hid#vid_3537&amp;pid_1040&amp;mi_02#...).</summary>
internal static partial class HidPathParser
{
    [GeneratedRegex(@"[&#]mi_([0-9a-f]{2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InterfaceRegex();

    [GeneratedRegex(@"[&#]col([0-9a-f]{2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CollectionRegex();

    [GeneratedRegex(@"[&#]ig_([0-9a-f]{2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex XInputGameInterfaceRegex();

    /// <summary>Número da interface USB (MI_xx), ou null em dispositivos não compostos.</summary>
    public static int? InterfaceNumber(string path) => ParseHex(InterfaceRegex(), path);

    /// <summary>Número da coleção HID de topo (COLxx), quando a interface expõe várias.</summary>
    public static int? CollectionNumber(string path) => ParseHex(CollectionRegex(), path);

    /// <summary>"IG_xx" marca a interface HID que o Windows associa a um dispositivo XInput.</summary>
    public static bool IsXInputGameInterface(string path) => XInputGameInterfaceRegex().IsMatch(path);

    private static int? ParseHex(Regex regex, string path)
    {
        var match = regex.Match(path);
        return match.Success
            ? int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : null;
    }
}
