using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NovaControlCenter.Infrastructure.Ds4;

/// <summary>
/// Envio de relatório de saída pelo canal de controle HID (<c>HidD_SetOutputReport</c>, SET_REPORT).
/// Por Bluetooth o DS4 aceita o relatório 0x11 por este caminho (é o usado pelo DS4Windows);
/// o HidSharp só oferece WriteFile (canal de interrupção).
/// </summary>
internal static partial class HidControlChannel
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;

    public static bool SetOutputReport(string devicePath, byte[] report)
    {
        using var handle = CreateFile(devicePath, GenericRead | GenericWrite, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid) return false;
        return HidD_SetOutputReport(handle, report, report.Length);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(string fileName, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HidD_SetOutputReport(SafeFileHandle device, byte[] buffer, int length);
}
