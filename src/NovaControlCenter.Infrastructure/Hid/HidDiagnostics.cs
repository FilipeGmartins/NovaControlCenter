using System.Globalization;
using HidSharp;
using NovaControlCenter.Core.Devices;

namespace NovaControlCenter.Infrastructure.Hid;

public sealed record HidReportInfo(byte ReportId, int Length);

public sealed record HidInterfaceReport(
    string DevicePath,
    UsbDeviceId DeviceId,
    string ReleaseNumber,
    int? InterfaceNumber,
    int? CollectionNumber,
    bool IsXInputGameInterface,
    string? Manufacturer,
    string? ProductName,
    string? SerialNumber,
    IReadOnlyList<string> TopLevelUsages,
    int MaxInputReportLength,
    int MaxOutputReportLength,
    int MaxFeatureReportLength,
    IReadOnlyList<HidReportInfo> InputReports,
    IReadOnlyList<HidReportInfo> OutputReports,
    IReadOnlyList<HidReportInfo> FeatureReports,
    string? RawReportDescriptorHex,
    IReadOnlyList<string> Errors);

/// <summary>
/// Inspeção de interfaces HID. Somente leitura de metadados expostos pelo Windows:
/// nenhum relatório é enviado ao dispositivo.
/// </summary>
public static class HidDiagnostics
{
    public static IReadOnlyList<HidInterfaceReport> Scan(ushort? vendorId)
    {
        var devices = vendorId is null
            ? DeviceList.Local.GetHidDevices()
            : DeviceList.Local.GetHidDevices(vendorId.Value);

        return devices
            .Select(Describe)
            .OrderBy(r => r.DeviceId.ProductId)
            .ThenBy(r => r.InterfaceNumber)
            .ThenBy(r => r.CollectionNumber)
            .ToList();
    }

    public static HidInterfaceReport Describe(HidDevice device)
    {
        var errors = new List<string>();
        string? Try(Func<string> read, string what)
        {
            try { return read(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
            {
                errors.Add($"{what}: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        var usages = new List<string>();
        var input = new List<HidReportInfo>();
        var output = new List<HidReportInfo>();
        var feature = new List<HidReportInfo>();
        string? rawHex = null;

        try
        {
            rawHex = Convert.ToHexString(device.GetRawReportDescriptor());
            var descriptor = device.GetReportDescriptor();
            foreach (var item in descriptor.DeviceItems)
            {
                foreach (var usage in item.Usages.GetAllValues())
                    usages.Add(HidUsageText.Describe(usage));
            }
            input.AddRange(descriptor.InputReports.Select(r => new HidReportInfo(r.ReportID, r.Length)));
            output.AddRange(descriptor.OutputReports.Select(r => new HidReportInfo(r.ReportID, r.Length)));
            feature.AddRange(descriptor.FeatureReports.Select(r => new HidReportInfo(r.ReportID, r.Length)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            errors.Add($"Report descriptor: {ex.GetType().Name}: {ex.Message}");
        }

        int SafeLength(Func<int> read, string what)
        {
            try { return read(); }
            catch (Exception ex) when (ex is IOException or NotSupportedException)
            {
                errors.Add($"{what}: {ex.Message}");
                return -1;
            }
        }

        return new HidInterfaceReport(
            DevicePath: device.DevicePath,
            DeviceId: new UsbDeviceId((ushort)device.VendorID, (ushort)device.ProductID),
            ReleaseNumber: device.ReleaseNumberBcd.ToString("X4", CultureInfo.InvariantCulture),
            InterfaceNumber: HidPathParser.InterfaceNumber(device.DevicePath),
            CollectionNumber: HidPathParser.CollectionNumber(device.DevicePath),
            IsXInputGameInterface: HidPathParser.IsXInputGameInterface(device.DevicePath),
            Manufacturer: Try(device.GetManufacturer, "Manufacturer"),
            ProductName: Try(device.GetProductName, "ProductName"),
            SerialNumber: Try(device.GetSerialNumber, "SerialNumber"),
            TopLevelUsages: usages,
            MaxInputReportLength: SafeLength(device.GetMaxInputReportLength, "MaxInputReportLength"),
            MaxOutputReportLength: SafeLength(device.GetMaxOutputReportLength, "MaxOutputReportLength"),
            MaxFeatureReportLength: SafeLength(device.GetMaxFeatureReportLength, "MaxFeatureReportLength"),
            InputReports: input,
            OutputReports: output,
            FeatureReports: feature,
            RawReportDescriptorHex: rawHex,
            Errors: errors);
    }

    public static HidDevice? FindByPath(string devicePath) =>
        DeviceList.Local.GetHidDevices()
            .FirstOrDefault(d => string.Equals(d.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));
}
