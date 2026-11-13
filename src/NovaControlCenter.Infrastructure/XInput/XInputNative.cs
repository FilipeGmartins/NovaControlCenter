using System.Runtime.InteropServices;

namespace NovaControlCenter.Infrastructure.XInput;

/// <summary>
/// P/Invoke mínimo para xinput1_4.dll (Windows 8+). Leitura + vibração (XInputSetState, documentada).
/// </summary>
internal static partial class XInputNative
{
    private const string Dll = "xinput1_4.dll";

    public const int MaxControllers = 4;
    public const uint ErrorSuccess = 0;
    public const uint ErrorDeviceNotConnected = 1167;

    public const byte BatteryDevTypeGamepad = 0x00;

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputVibration
    {
        public ushort LeftMotorSpeed;
        public ushort RightMotorSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputCapabilities
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public XInputGamepad Gamepad;
        public XInputVibration Vibration;
    }

    /// <summary>Estrutura estendida (não documentada) retornada pelo ordinal #108.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputCapabilitiesEx
    {
        public XInputCapabilities Capabilities;
        public ushort VendorId;
        public ushort ProductId;
        public ushort ProductVersion;
        public ushort Unknown1;
        public uint Unknown2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputBatteryInformation
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [LibraryImport(Dll, EntryPoint = "XInputGetState")]
    public static partial uint GetState(uint userIndex, out XInputState state);

    /// <summary>Ordinal #100: igual a GetState, mas inclui o botão Guide (0x0400). Não documentado.</summary>
    [LibraryImport(Dll, EntryPoint = "#100")]
    public static partial uint GetStateEx(uint userIndex, out XInputState state);

    [LibraryImport(Dll, EntryPoint = "XInputGetCapabilities")]
    public static partial uint GetCapabilities(uint userIndex, uint flags, out XInputCapabilities capabilities);

    /// <summary>Ordinal #108: devolve VID/PID do dispositivo no slot. Primeiro parâmetro deve ser 1. Não documentado.</summary>
    [LibraryImport(Dll, EntryPoint = "#108")]
    public static partial uint GetCapabilitiesEx(uint reserved, uint userIndex, uint flags, out XInputCapabilitiesEx capabilities);

    /// <summary>Vibração. API pública; a única escrita permitida no dispositivo (ADR-09).</summary>
    [LibraryImport(Dll, EntryPoint = "XInputSetState")]
    public static partial uint SetState(uint userIndex, ref XInputVibration vibration);

    [LibraryImport(Dll, EntryPoint = "XInputGetBatteryInformation")]
    public static partial uint GetBatteryInformation(uint userIndex, byte devType, out XInputBatteryInformation information);
}
