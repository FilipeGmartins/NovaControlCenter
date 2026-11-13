using System.Diagnostics;
using System.Globalization;
using HidSharp;

namespace NovaControlCenter.Infrastructure.Hid;

public sealed record PassiveListenResult(int ReportsReceived, int Timeouts, string? Error);

/// <summary>
/// Escuta PASSIVA de relatórios de entrada de uma interface HID.
/// Abre o handle e apenas lê — não chama Write, SetFeature nem GetFeature.
/// </summary>
public static class HidPassiveListener
{
    public const string CsvHeader = "elapsed_ms,event,device,length,hex";

    /// <summary>
    /// Lê até <paramref name="until"/> (medido em <paramref name="clock"/>) ou até o dispositivo sumir.
    /// Cada relatório vira uma linha CSV no formato <see cref="CsvHeader"/>.
    /// </summary>
    public static PassiveListenResult Listen(
        HidDevice device,
        Stopwatch clock,
        TimeSpan until,
        ReportByteStatistics stats,
        TextWriter csv,
        Action<int, ReadOnlyMemory<byte>>? onReport,
        CancellationToken cancellationToken)
    {
        var tag = string.Create(CultureInfo.InvariantCulture, $"{device.VendorID:X4}:{device.ProductID:X4}");
        var timeouts = 0;
        var received = 0;

        if (!device.TryOpen(out HidStream stream))
            return new PassiveListenResult(0, 0, "Não foi possível abrir a interface (pode estar em uso exclusivo pelo Windows, como teclado/mouse).");

        using (stream)
        {
            stream.ReadTimeout = 1000;
            var buffer = new byte[Math.Max(device.GetMaxInputReportLength(), 1)];

            while (clock.Elapsed < until && !cancellationToken.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = stream.Read(buffer, 0, buffer.Length);
                }
                catch (TimeoutException)
                {
                    timeouts++;
                    continue;
                }
                catch (IOException ex)
                {
                    return new PassiveListenResult(received, timeouts, $"Leitura interrompida (desconectado?): {ex.Message}");
                }

                if (read <= 0) continue;
                received++;
                var report = buffer.AsMemory(0, read);
                stats.Add(report.Span);
                csv.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{clock.ElapsedMilliseconds},report,{tag},{read},{Convert.ToHexString(report.Span)}"));
                onReport?.Invoke(stats.ReportCount, report);
            }
        }

        return new PassiveListenResult(received, timeouts, null);
    }
}
