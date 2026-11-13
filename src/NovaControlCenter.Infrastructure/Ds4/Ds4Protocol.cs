using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Infrastructure.Ds4;

/// <summary>Bateria do DS4: nível bruto 0–11 e se há cabo. Resolução real de 10%.</summary>
public readonly record struct Ds4Battery(int RawLevel, bool CablePlugged)
{
    /// <summary>Porcentagem aproximada (múltiplos de 10).</summary>
    public int Percent => Math.Clamp(RawLevel * 10, 0, 100);

    public bool IsCharging => CablePlugged && RawLevel <= 10;

    public bool IsFull => CablePlugged && RawLevel >= 11;
}

/// <summary>Um ponto de toque do touchpad (X 0–1919, Y 0–941).</summary>
public readonly record struct Ds4TouchPoint(bool Active, int Id, int X, int Y);

/// <summary>Até dois dedos no touchpad.</summary>
public readonly record struct Ds4Touch(Ds4TouchPoint First, Ds4TouchPoint Second)
{
    public const int Width = 1920;
    public const int Height = 942;
}

/// <summary>
/// Sensores de movimento em valores brutos (sem calibração de fábrica).
/// Giroscópio = velocidade angular; acelerômetro = gravidade + aceleração.
/// </summary>
public readonly record struct Ds4Motion(short GyroX, short GyroY, short GyroZ, short AccelX, short AccelY, short AccelZ)
{
    /// <summary>Inclinação frente/trás em graus, a partir da gravidade (independe da escala do sensor).</summary>
    public double PitchDegrees => Math.Atan2(AccelZ, AccelY) * 180 / Math.PI;

    /// <summary>Inclinação lateral em graus, a partir da gravidade.</summary>
    public double RollDegrees => Math.Atan2(AccelX, AccelY) * 180 / Math.PI;
}

/// <summary>Leitura decodificada de um relatório de entrada do DS4.</summary>
public sealed record Ds4Input(ControllerState State, bool TouchpadClick, Ds4Battery? Battery, bool Extended,
    Ds4Touch? Touch = null, Ds4Motion? Motion = null);

/// <summary>Cor RGB da lightbar.</summary>
public readonly record struct Ds4Color(byte R, byte G, byte B)
{
    public static readonly Ds4Color Off = new(0, 0, 0);
}

/// <summary>
/// Protocolo do DualShock 4 a partir de fontes públicas: driver Linux <c>hid-playstation</c> e SDL
/// (<c>SDL_hidapi_ps4.c</c>). Ver docs/HARDWARE_DIAGNOSTICS.md §3.10.2.
/// </summary>
public static class Ds4Protocol
{
    public const byte ReportBasic = 0x01;
    public const byte ReportBluetoothFull = 0x11;
    public const byte FeatureCalibrationBluetooth = 0x05;
    public const int BluetoothOutputLength = 78;

    private const byte OutputHwControl = 0xC0;    // HID + CRC32
    private const byte FlagMotor = 0x01;
    private const byte FlagLight = 0x02;
    private const byte CrcSeedOutput = 0xA2;
    private const int BatteryOffsetUsb = 30;       // offset no layout USB (índice 0 = report ID)
    private const int GyroOffsetUsb = 13;          // 3 x int16 LE
    private const int AccelOffsetUsb = 19;         // 3 x int16 LE
    private const int TouchOffsetUsb = 35;         // 1º ponto do 1º pacote de toque (após contagem e timestamp)

    /// <summary>
    /// Decodifica um relatório de entrada. <paramref name="bluetooth"/> diferencia o 0x01 básico (BT, sem bateria)
    /// do 0x01 completo (USB, com bateria). Retorna null para relatórios não reconhecidos.
    /// </summary>
    public static Ds4Input? ParseInput(ReadOnlySpan<byte> report, bool bluetooth, uint packetNumber)
    {
        if (report.IsEmpty) return null;

        int shift;           // deslocamento em relação ao layout USB
        bool hasBattery;
        bool extended;
        switch (report[0])
        {
            case ReportBasic when report.Length >= 10:
                shift = 0;
                extended = !bluetooth;
                hasBattery = !bluetooth && report.Length > BatteryOffsetUsb;
                break;
            case ReportBluetoothFull when report.Length > BatteryOffsetUsb + 2:
                shift = 2;
                extended = true;
                hasBattery = true;
                break;
            default:
                return null;
        }

        // Visão no layout USB: d[k] = byte k do relatório USB equivalente.
        var d = report[shift..];

        var b0 = d[5];
        var b1 = d[6];
        var b2 = d[7];
        var buttons = Hat(b0 & 0x0F);
        if ((b0 & 0x10) != 0) buttons |= GamepadButtons.X;  // quadrado
        if ((b0 & 0x20) != 0) buttons |= GamepadButtons.A;  // xis
        if ((b0 & 0x40) != 0) buttons |= GamepadButtons.B;  // círculo
        if ((b0 & 0x80) != 0) buttons |= GamepadButtons.Y;  // triângulo
        if ((b1 & 0x01) != 0) buttons |= GamepadButtons.LeftShoulder;
        if ((b1 & 0x02) != 0) buttons |= GamepadButtons.RightShoulder;
        if ((b1 & 0x10) != 0) buttons |= GamepadButtons.Back;   // Share
        if ((b1 & 0x20) != 0) buttons |= GamepadButtons.Start;  // Options
        if ((b1 & 0x40) != 0) buttons |= GamepadButtons.LeftThumb;
        if ((b1 & 0x80) != 0) buttons |= GamepadButtons.RightThumb;
        if ((b2 & 0x01) != 0) buttons |= GamepadButtons.Guide;  // PS

        var state = new ControllerState(
            packetNumber,
            buttons,
            d[8],
            d[9],
            new StickState(Axis(d[1]), InvertedAxis(d[2])),
            new StickState(Axis(d[3]), InvertedAxis(d[4])));

        Ds4Battery? battery = null;
        if (hasBattery)
        {
            var raw = d[BatteryOffsetUsb];
            battery = new Ds4Battery(raw & 0x0F, (raw & 0x10) != 0);
        }

        Ds4Touch? touch = null;
        Ds4Motion? motion = null;
        if (hasBattery && d.Length >= TouchOffsetUsb + 8)
        {
            motion = new Ds4Motion(
                Int16(d, GyroOffsetUsb), Int16(d, GyroOffsetUsb + 2), Int16(d, GyroOffsetUsb + 4),
                Int16(d, AccelOffsetUsb), Int16(d, AccelOffsetUsb + 2), Int16(d, AccelOffsetUsb + 4));
            touch = new Ds4Touch(TouchPoint(d.Slice(TouchOffsetUsb, 4)), TouchPoint(d.Slice(TouchOffsetUsb + 4, 4)));
        }

        return new Ds4Input(state, (b2 & 0x02) != 0, battery, extended, touch, motion);
    }

    private static short Int16(ReadOnlySpan<byte> d, int offset) => (short)(d[offset] | (d[offset + 1] << 8));

    /// <summary>Byte 0: bit 7 = 1 significa SEM toque; bits 0–6 = id. X/Y de 12 bits nos 3 bytes seguintes.</summary>
    internal static Ds4TouchPoint TouchPoint(ReadOnlySpan<byte> p) => new(
        (p[0] & 0x80) == 0,
        p[0] & 0x7F,
        p[1] | ((p[2] & 0x0F) << 8),
        (p[2] >> 4) | (p[3] << 4));

    /// <summary>Monta o relatório de saída 0x11 (Bluetooth) com luz e vibração, incluindo o CRC-32.</summary>
    /// <param name="hwControl">0xC0 (kernel Linux) ou 0xC4 (SDL: também define o intervalo de 4 ms).</param>
    /// <param name="totalLength">Tamanho total enviado. No Windows o descritor do DS4 declara 547 bytes para o 0x11 (77 úteis + preenchimento).</param>
    /// <param name="crcSeed">0xA2 = DATA|Output (interrupção); 0x52 = SET_REPORT|Output (controle).</param>
    public static byte[] BuildBluetoothOutput(Ds4Color? light, byte rumbleLeft = 0, byte rumbleRight = 0, bool includeRumble = false,
        byte hwControl = OutputHwControl, int totalLength = BluetoothOutputLength, byte crcSeed = CrcSeedOutput, byte? validFlags = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(totalLength, BluetoothOutputLength);
        var report = new byte[totalLength];
        report[0] = ReportBluetoothFull;
        report[1] = hwControl;
        report[2] = 0x00;
        byte flags = 0;
        if (includeRumble)
        {
            flags |= FlagMotor;
            report[6] = rumbleRight;   // motor leve
            report[7] = rumbleLeft;    // motor pesado
        }
        if (light is { } c)
        {
            flags |= FlagLight;
            report[8] = c.R;
            report[9] = c.G;
            report[10] = c.B;
        }
        report[3] = validFlags ?? flags;

        // CRC-32 nos 4 últimos bytes do relatório, calculado sobre a semente + todo o resto.
        var end = totalLength - 4;
        var crc = Crc32.Compute(crcSeed, report.AsSpan(0, end));
        report[end] = (byte)crc;
        report[end + 1] = (byte)(crc >> 8);
        report[end + 2] = (byte)(crc >> 16);
        report[end + 3] = (byte)(crc >> 24);
        return report;
    }

    public const byte ReportUsbOutput = 0x05;
    public const int UsbOutputLength = 32;

    /// <summary>Relatório de saída 0x05 (USB): mesmo bloco comum do Bluetooth, sem cabeçalho extra e sem CRC.</summary>
    public static byte[] BuildUsbOutput(Ds4Color? light, byte rumbleLeft = 0, byte rumbleRight = 0, bool includeRumble = false, byte? validFlags = null)
    {
        var report = new byte[UsbOutputLength];
        report[0] = ReportUsbOutput;
        byte flags = 0;
        if (includeRumble)
        {
            flags |= FlagMotor;
            report[4] = rumbleRight;
            report[5] = rumbleLeft;
        }
        if (light is { } c)
        {
            flags |= FlagLight;
            report[6] = c.R;
            report[7] = c.G;
            report[8] = c.B;
        }
        report[1] = validFlags ?? flags;
        return report;
    }

    /// <summary>Converte 0..255 (centro 128) para a faixa do XInput.</summary>
    internal static short Axis(byte value) => (short)Math.Clamp((value - 128) * 258, short.MinValue, short.MaxValue);

    /// <summary>No DS4, Y cresce para baixo; no XInput, para cima.</summary>
    internal static short InvertedAxis(byte value) => (short)Math.Clamp((128 - value) * 258, short.MinValue, short.MaxValue);

    private static GamepadButtons Hat(int hat) => hat switch
    {
        0 => GamepadButtons.DPadUp,
        1 => GamepadButtons.DPadUp | GamepadButtons.DPadRight,
        2 => GamepadButtons.DPadRight,
        3 => GamepadButtons.DPadDown | GamepadButtons.DPadRight,
        4 => GamepadButtons.DPadDown,
        5 => GamepadButtons.DPadDown | GamepadButtons.DPadLeft,
        6 => GamepadButtons.DPadLeft,
        7 => GamepadButtons.DPadUp | GamepadButtons.DPadLeft,
        _ => GamepadButtons.None,
    };
}

/// <summary>CRC-32 padrão (IEEE 802.3, polinômio refletido 0xEDB88320), o mesmo do zlib.</summary>
public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data) => ~Update(0xFFFFFFFF, data);

    /// <summary>CRC de <paramref name="seed"/> seguido de <paramref name="data"/> (formato exigido pelo DS4 via Bluetooth).</summary>
    public static uint Compute(byte seed, ReadOnlySpan<byte> data) => ~Update(Update(0xFFFFFFFF, [seed]), data);

    private static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
