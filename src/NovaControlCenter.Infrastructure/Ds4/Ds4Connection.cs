using System.Globalization;
using HidSharp;
using Windows.Devices.Bluetooth;

namespace NovaControlCenter.Infrastructure.Ds4;

/// <summary>
/// Conexão HID com um DualShock 4. Por padrão só lê. Escritas acontecem apenas em
/// <see cref="EnableExtendedMode"/> e <see cref="SendOutput"/>, chamados por ação explícita do usuário (ADR-23).
/// </summary>
public sealed class Ds4Connection : IDisposable
{
    public const ushort SonyVendorId = 0x054C;
    public static readonly ushort[] Ds4ProductIds = [0x05C4, 0x09CC];

    private const string BluetoothHidServiceGuid = "00001124-0000-1000-8000-00805f9b34fb";

    private readonly HidDevice _device;
    private readonly HidStream _stream;
    private readonly byte[] _readBuffer;
    private uint _packet;

    private Ds4Connection(HidDevice device, HidStream stream)
    {
        _device = device;
        _stream = stream;
        _stream.ReadTimeout = 1000;
        _readBuffer = new byte[Math.Max(device.GetMaxInputReportLength(), 64)];
        IsBluetooth = IsBluetoothPath(device.DevicePath);
    }

    public bool IsBluetooth { get; }

    public string DevicePath => _device.DevicePath;

    public ushort ProductId => (ushort)_device.ProductID;

    /// <summary>Indica se o modo estendido foi solicitado nesta sessão.</summary>
    public bool ExtendedRequested { get; private set; }

    /// <summary>DS4 presentes, com USB antes de Bluetooth (o cabo dá acesso direto, sem a pilha Bluetooth).</summary>
    public static IReadOnlyList<HidDevice> FindDevices() =>
        DeviceList.Local.GetHidDevices(SonyVendorId)
            .Where(d => Ds4ProductIds.Contains((ushort)d.ProductID))
            .OrderBy(d => IsBluetoothPath(d.DevicePath))
            .ToList();

    public static bool AnyUsbPresent() => FindDevices().Any(d => !IsBluetoothPath(d.DevicePath));

    public static Ds4Connection? TryOpen(HidDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return device.TryOpen(out HidStream stream) ? new Ds4Connection(device, stream) : null;
    }

    internal static bool IsBluetoothPath(string path) =>
        path.Contains(BluetoothHidServiceGuid, StringComparison.OrdinalIgnoreCase);

    /// <summary>Lê o próximo relatório. Null em timeout ou relatório não reconhecido. IOException = desconectado.</summary>
    public Ds4Input? Read()
    {
        int read;
        try
        {
            read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
        }
        catch (TimeoutException)
        {
            return null;
        }
        return read <= 0 ? null : Ds4Protocol.ParseInput(_readBuffer.AsSpan(0, read), IsBluetooth, ++_packet);
    }

    /// <summary>
    /// Bluetooth: lê o feature report de calibração (0x05), que faz o DS4 passar a enviar o relatório 0x11
    /// completo, com bateria, até ser desligado. Por USB o relatório já é completo.
    /// </summary>
    public bool EnableExtendedMode()
    {
        ExtendedRequested = true;
        if (!IsBluetooth) return true;
        var buffer = new byte[Math.Max(_device.GetMaxFeatureReportLength(), 64)];
        buffer[0] = Ds4Protocol.FeatureCalibrationBluetooth;
        try
        {
            _stream.GetFeature(buffer);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Caminho usado para enviar o relatório de saída (em validação: HARDWARE_DIAGNOSTICS §3.10.5).</summary>
    public enum OutputPath
    {
        /// <summary>HidD_SetOutputReport (canal de controle), 78 bytes.</summary>
        Control,

        /// <summary>WriteFile (canal de interrupção), buffer completado até o tamanho máximo do dispositivo.</summary>
        Interrupt,
    }

    /// <summary>Envia luz e/ou vibração. Por Bluetooth também ativa o modo estendido.</summary>
    public bool SendOutput(Ds4Color? light, byte rumbleLeft = 0, byte rumbleRight = 0, bool includeRumble = false, OutputPath path = OutputPath.Control, byte hwControl = 0xC0, bool fullLength = false, byte crcSeed = 0xA2, byte? validFlags = null)
    {
        if (!IsBluetooth) return WriteInterrupt(Ds4Protocol.BuildUsbOutput(light, rumbleLeft, rumbleRight, includeRumble, validFlags));
        ExtendedRequested = true;
        var length = fullLength ? Math.Max(_device.GetMaxOutputReportLength(), Ds4Protocol.BluetoothOutputLength) : Ds4Protocol.BluetoothOutputLength;
        var report = Ds4Protocol.BuildBluetoothOutput(light, rumbleLeft, rumbleRight, includeRumble, hwControl, length, crcSeed, validFlags);
        if (path == OutputPath.Control) return HidControlChannel.SetOutputReport(_device.DevicePath, report);
        return WriteInterrupt(report);
    }

    private bool WriteInterrupt(byte[] report)
    {
        // O Windows exige o buffer com o tamanho máximo de saída do dispositivo; o restante fica zerado.
        var buffer = new byte[Math.Max(_device.GetMaxOutputReportLength(), report.Length)];
        report.CopyTo(buffer, 0);
        try
        {
            _stream.Write(buffer);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            // Timeout = controle desligado/fora de alcance (o nó HID continua "presente" no Windows).
            return false;
        }
    }

    /// <summary>
    /// Por Bluetooth, o Windows mantém o dispositivo HID listado mesmo com o controle desligado
    /// (HARDWARE_DIAGNOSTICS §3.10.4). O estado real do link vem do WinRT, pelo endereço (= serial HID).
    /// USB: sempre conectado enquanto presente.
    /// </summary>
    public async Task<bool> IsLinkConnectedAsync()
    {
        if (!IsBluetooth) return true;
        var address = TryParseAddress(SafeSerial(_device));
        if (address is null) return true; // sem endereço: não dá para afirmar o contrário
        using var bt = await BluetoothDevice.FromBluetoothAddressAsync(address.Value);
        return bt?.ConnectionStatus == BluetoothConnectionStatus.Connected;
    }

    internal static ulong? TryParseAddress(string? serial)
    {
        var hex = serial?.Replace(":", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return hex is { Length: 12 } && ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? SafeSerial(HidDevice device)
    {
        try { return device.GetSerialNumber(); }
        catch (IOException) { return null; }
    }

    public void Dispose() => _stream.Dispose();
}
