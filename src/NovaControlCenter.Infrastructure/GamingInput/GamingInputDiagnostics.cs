using NovaControlCenter.Core.Devices;
using Windows.Gaming.Input;

namespace NovaControlCenter.Infrastructure.GamingInput;

public sealed record GamingInputControllerReport(
    string DisplayName,
    UsbDeviceId DeviceId,
    bool IsWireless,
    int ButtonCount,
    int AxisCount,
    int SwitchCount,
    bool IsAlsoGamepad,
    bool BatteryReportAvailable,
    string? BatteryStatus,
    int? RemainingCapacityMilliwattHours,
    int? FullChargeCapacityMilliwattHours,
    int? ChargeRateMilliwatts,
    string? Error);

/// <summary>Consulta Windows.Gaming.Input (WinRT): enumeração e BatteryReport.</summary>
public static class GamingInputDiagnostics
{
    /// <summary>
    /// A lista do WinRT é preenchida de forma assíncrona após o primeiro acesso,
    /// por isso aguardamos um curto período antes de ler.
    /// </summary>
    public static async Task<IReadOnlyList<GamingInputControllerReport>> ScanAsync(TimeSpan warmUp, CancellationToken cancellationToken)
    {
        _ = RawGameController.RawGameControllers;
        await Task.Delay(warmUp, cancellationToken).ConfigureAwait(false);

        var gamepads = Gamepad.Gamepads;
        return RawGameController.RawGameControllers.Select(raw => Describe(raw, gamepads)).ToList();
    }

    private static GamingInputControllerReport Describe(RawGameController raw, IReadOnlyList<Gamepad> gamepads)
    {
        var id = new UsbDeviceId(raw.HardwareVendorId, raw.HardwareProductId);
        var isGamepad = gamepads.Any(g => RawGameController.FromGameController(g)?.NonRoamableId == raw.NonRoamableId);
        try
        {
            var report = raw.TryGetBatteryReport();
            return new GamingInputControllerReport(
                raw.DisplayName, id, raw.IsWireless, raw.ButtonCount, raw.AxisCount, raw.SwitchCount, isGamepad,
                BatteryReportAvailable: report is not null,
                BatteryStatus: report?.Status.ToString(),
                RemainingCapacityMilliwattHours: report?.RemainingCapacityInMilliwattHours,
                FullChargeCapacityMilliwattHours: report?.FullChargeCapacityInMilliwattHours,
                ChargeRateMilliwatts: report?.ChargeRateInMilliwatts,
                Error: null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return new GamingInputControllerReport(
                raw.DisplayName, id, raw.IsWireless, raw.ButtonCount, raw.AxisCount, raw.SwitchCount, isGamepad,
                false, null, null, null, null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
