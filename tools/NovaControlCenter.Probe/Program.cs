using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;
using NovaControlCenter.Infrastructure.DeviceDetection;
using NovaControlCenter.Infrastructure.GamingInput;
using NovaControlCenter.Infrastructure.Hid;
using NovaControlCenter.Infrastructure.XInput;

// nova-probe — ferramenta de diagnóstico da Sprint 01.
// SOMENTE LEITURA: nunca envia relatórios de saída ou feature ao dispositivo.

Console.OutputEncoding = Encoding.UTF8;
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "scan";
var outputDir = Path.Combine(Environment.CurrentDirectory, "probe-reports");
Directory.CreateDirectory(outputDir);
var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

try
{
    return command switch
    {
        "scan" => await Commands.ScanAsync(args.Contains("--all"), outputDir, stamp, cts.Token),
        "listen" => Commands.Listen(args, outputDir, stamp, cts.Token),
        "battery-log" => await Commands.BatteryLogAsync(args, outputDir, stamp, cts.Token),
        "watch" => Commands.Watch(cts.Token),
        "monitor" => await Commands.MonitorAsync(args, outputDir, stamp, cts.Token),
        "ds4" => Ds4Command.Run(args, cts.Token),
        "xinput-check" => XInputCheckCommand.Run(args, cts.Token),
        "input-test" => await InputTestCommand.RunAsync(Commands.IntOption(args, "--seconds", 90), args.Contains("--rumble"), outputDir, stamp, cts.Token),
        _ => Commands.Help(),
    };
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nInterrompido pelo usuário.");
    return 130;
}

internal static class Commands
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static int Help()
    {
        Console.WriteLine("""
            nova-probe — diagnóstico do GameSir T4 Nova Lite (somente leitura)

              scan [--all]                  HID (VID 0x3537, ou todos com --all) + XInput + Windows.Gaming.Input
              listen <n|caminho|vendor> [--seconds 60]
                                            Escuta passiva dos relatórios de entrada de uma interface HID
                                            (n = número do 'scan'; vendor = segue 1040/2106). CSV + estatística.
              battery-log [--minutes 30] [--interval 10]
                                            Registra periodicamente bateria via XInput e WinRT em CSV.
              watch                         Mostra o estado XInput em tempo real (Ctrl+C para sair).
              monitor [--seconds 120]       Valida o ConnectionMonitor (Sprint 02): estados de conexão + entradas.
              ds4 [validate | light RRGGBB]  DualShock 4: leitura básica, modo estendido (bateria) e teste de luz.
              input-test [--seconds 90] [--rumble]
                                            Valida a Sprint 03: bateria, vibração e cobertura de botões/eixos.

            Resultados em .\probe-reports\
            """);
        return 0;
    }

    public static async Task<int> ScanAsync(bool all, string outputDir, string stamp, CancellationToken ct)
    {
        Section($"HID — {(all ? "todos os dispositivos" : $"VID 0x{KnownDevices.GameSirVendorId:X4} (GameSir)")}");
        var hid = HidDiagnostics.Scan(all ? null : KnownDevices.GameSirVendorId);
        if (hid.Count == 0)
            Console.WriteLine("  Nenhuma interface encontrada. O receptor está conectado?");
        for (var i = 0; i < hid.Count; i++)
            PrintHid(i, hid[i]);

        Section("XInput (xinput1_4.dll)");
        var slots = XInputDiagnostics.ScanSlots();
        foreach (var s in slots) PrintSlot(s);

        Section("Windows.Gaming.Input (WinRT)");
        var winrt = await GamingInputDiagnostics.ScanAsync(TimeSpan.FromSeconds(1.5), ct);
        if (winrt.Count == 0) Console.WriteLine("  Nenhum controlador enumerado.");
        foreach (var c in winrt) PrintWinRt(c);

        var file = Path.Combine(outputDir, $"scan-{stamp}.json");
        await File.WriteAllTextAsync(file,
            JsonSerializer.Serialize(new { Timestamp = DateTimeOffset.Now, Hid = hid, XInput = slots, GamingInput = winrt }, Json), ct);
        Console.WriteLine($"\nRelatório salvo em {file}");
        return 0;
    }

    public static int Listen(string[] args, string outputDir, string stamp, CancellationToken ct)
    {
        if (args.Length < 2) return Help();
        var seconds = IntOption(args, "--seconds", 60);
        var target = args[1];
        var follow = string.Equals(target, "vendor", StringComparison.OrdinalIgnoreCase);

        // "vendor": segue a interface vendor-defined da GameSir, seja qual for o PID atual
        // (1040 = controle conectado, 2106 = receptor sem controle), sobrevivendo a liga/desliga.
        Func<HidSharp.HidDevice?> resolve = follow ? FindVendorInterface : () => ResolveFixed(target);

        var file = Path.Combine(outputDir, $"listen-{(follow ? "vendor" : "fixed")}-{stamp}.csv");
        using var writer = new StreamWriter(file) { AutoFlush = true };
        writer.WriteLine(HidPassiveListener.CsvHeader);

        Console.WriteLine($"Escuta passiva por {seconds}s (somente leitura){(follow ? ", seguindo a interface vendor GameSir" : "")}. Ctrl+C para parar.\n");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var until = TimeSpan.FromSeconds(seconds);
        var stats = new ReportByteStatistics();
        int total = 0, timeouts = 0;
        string? lastTag = null;

        while (clock.Elapsed < until && !ct.IsCancellationRequested)
        {
            var device = resolve();
            var tag = device is null ? "nenhum" : $"{device.VendorID:X4}:{device.ProductID:X4}";
            if (tag != lastTag)
            {
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{clock.ElapsedMilliseconds},attach,{tag},0,"));
                Console.WriteLine($"[{clock.Elapsed:mm\\:ss}] Interface ativa: {tag}{(device is null ? "" : $"  {device.DevicePath}")}");
                lastTag = tag;
            }
            if (device is null)
            {
                if (!follow) { Console.WriteLine("Interface não encontrada. Rode 'scan' para ver os números."); return 2; }
                Thread.Sleep(500);
                continue;
            }

            var result = HidPassiveListener.Listen(device, clock, until, stats, writer,
                (count, report) =>
                {
                    if (count % 25 == 1)
                        Console.WriteLine($"  #{count,6} [{report.Length,3}] {Convert.ToHexString(report.Span)}");
                }, ct);
            total += result.ReportsReceived;
            timeouts += result.Timeouts;
            if (result.Error is not null)
            {
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{clock.ElapsedMilliseconds},detach,{tag},0,"));
                Console.WriteLine($"[{clock.Elapsed:mm\\:ss}] {result.Error}");
                if (!follow) break;
                lastTag = null;
                Thread.Sleep(500);
            }
        }

        Console.WriteLine($"\nRelatórios: {total}  Timeouts (1s sem dados): {timeouts}");
        if (total > 0)
        {
            Console.WriteLine("\nByte | min max distintos primeiro último");
            foreach (var b in stats.Summarize())
            {
                var hint = b.DistinctValues == 1 ? "constante" : b.DistinctValues <= 4 ? "← muda pouco (candidato a estado/bateria)" : "";
                Console.WriteLine($"{b.Offset,4} | {b.Min,3:X2} {b.Max,3:X2} {b.DistinctValues,9} {b.First,8:X2} {b.Last,6:X2}  {hint}");
            }
        }
        Console.WriteLine($"\nCSV salvo em {file}");
        return 0;
    }

    private static HidSharp.HidDevice? ResolveFixed(string target)
    {
        if (!int.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            return HidDiagnostics.FindByPath(target);
        var list = HidSharp.DeviceList.Local.GetHidDevices(KnownDevices.GameSirVendorId)
            .Select(d => (Device: d, Report: HidDiagnostics.Describe(d)))
            .OrderBy(x => x.Report.DeviceId.ProductId).ThenBy(x => x.Report.InterfaceNumber).ThenBy(x => x.Report.CollectionNumber)
            .ToList();
        return index >= 0 && index < list.Count ? list[index].Device : null;
    }

    private static HidSharp.HidDevice? FindVendorInterface() =>
        HidSharp.DeviceList.Local.GetHidDevices(KnownDevices.GameSirVendorId)
            .FirstOrDefault(d => HidDiagnostics.Describe(d).TopLevelUsages.Any(u => u.Contains("Vendor-defined", StringComparison.Ordinal)));

    public static async Task<int> BatteryLogAsync(string[] args, string outputDir, string stamp, CancellationToken ct)
    {
        var minutes = IntOption(args, "--minutes", 30);
        var interval = Math.Max(1, IntOption(args, "--interval", 10));
        var file = Path.Combine(outputDir, $"battery-{stamp}.csv");
        await using var writer = new StreamWriter(file) { AutoFlush = true };
        await writer.WriteLineAsync("timestamp,source,id,connected,battery_type,battery_level,winrt_status,remaining_mwh,full_mwh,rate_mw");

        Console.WriteLine($"Registrando bateria a cada {interval}s por {minutes} min → {file}");
        var end = DateTime.Now.AddMinutes(minutes);
        while (DateTime.Now < end)
        {
            ct.ThrowIfCancellationRequested();
            var now = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            foreach (var s in XInputDiagnostics.ScanSlots().Where(s => s.Connected))
            {
                var line = $"{now},xinput,slot{s.Slot}/{s.DeviceId},true,{s.BatteryType},{s.BatteryLevel},,,,";
                await writer.WriteLineAsync(line);
                Console.WriteLine($"  {line}");
            }
            foreach (var c in await GamingInputDiagnostics.ScanAsync(TimeSpan.FromMilliseconds(300), ct))
            {
                var line = $"{now},winrt,{c.DeviceId},true,,,{c.BatteryStatus},{c.RemainingCapacityMilliwattHours},{c.FullChargeCapacityMilliwattHours},{c.ChargeRateMilliwatts}";
                await writer.WriteLineAsync(line);
                Console.WriteLine($"  {line}");
            }
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);
        }
        return 0;
    }

    public static async Task<int> MonitorAsync(string[] args, string outputDir, string stamp, CancellationToken ct)
    {
        var seconds = IntOption(args, "--seconds", 120);
        var file = Path.Combine(outputDir, $"monitor-{stamp}.csv");
        await using var csv = new StreamWriter(file) { AutoFlush = true };
        await csv.WriteLineAsync("elapsed_ms,state,slot,slot_match,connected_since");

        using var presence = new HidDevicePresence();
        var reader = new XInputReader();
        await using var monitor = new ConnectionMonitor(presence, reader, TimeProvider.System, NullLogger<ConnectionMonitor>.Instance);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var gate = new object();

        monitor.SnapshotChanged += (_, s) =>
        {
            lock (gate)
            {
                var line = string.Create(CultureInfo.InvariantCulture,
                    $"{clock.ElapsedMilliseconds},{s.State},{s.Controller?.XInputSlot},{s.Controller?.SlotMatch},{s.ConnectedSince:O}");
                csv.WriteLine(line);
                Console.WriteLine($"\n[{clock.Elapsed:mm\\:ss\\.f}] {Describe(s)}");
            }
        };

        Console.WriteLine($"Monitorando por {seconds}s. Ligue/desligue o controle, remova/recoloque o receptor. Ctrl+C para sair.");
        monitor.Start();
        var end = TimeSpan.FromSeconds(seconds);
        while (clock.Elapsed < end && !ct.IsCancellationRequested)
        {
            var snap = monitor.Current;
            if (snap.Controller?.XInputSlot is int slot && reader.Read(slot) is { } st)
            {
                lock (gate)
                {
                    Console.Write(string.Create(CultureInfo.InvariantCulture,
                        $"\r  btn=[{st.Buttons,-40}] LT={st.LeftTrigger:0.00} RT={st.RightTrigger:0.00} L=({st.LeftStick.X,5:0.00},{st.LeftStick.Y,5:0.00}) R=({st.RightStick.X,5:0.00},{st.RightStick.Y,5:0.00})   "));
                }
            }
            await Task.Delay(50, CancellationToken.None);
        }
        Console.WriteLine($"\n\nCSV salvo em {file}");
        return 0;
    }

    private static string Describe(ConnectionSnapshot s) => s.State switch
    {
        ConnectionState.Connected =>
            $"CONECTADO  {s.Controller!.DisplayName} [{s.Controller.DeviceId}] via {s.Controller.ConnectionType}, " +
            $"slot XInput {s.Controller.XInputSlot?.ToString(CultureInfo.InvariantCulture) ?? "?"} ({s.Controller.SlotMatch}), modelo: {s.Controller.ModelConfidence}",
        ConnectionState.ControllerOff => "RECEPTOR PRESENTE, CONTROLE DESLIGADO (3537:2106)",
        _ => "RECEPTOR NÃO ENCONTRADO",
    };

    public static int Watch(CancellationToken ct)
    {
        Console.WriteLine("Estado XInput em tempo real — Ctrl+C para sair.\n");
        var top = Console.CursorTop;
        while (!ct.IsCancellationRequested)
        {
            Console.SetCursorPosition(0, top);
            foreach (var s in XInputDiagnostics.ScanSlots())
            {
                var text = s.Connected
                    ? $"Slot {s.Slot} [{s.DeviceId}] pkt={s.PacketNumber,8} btn=0x{s.Buttons:X4} LT={s.LeftTrigger,3} RT={s.RightTrigger,3} " +
                      $"L=({s.ThumbLX,6},{s.ThumbLY,6}) R=({s.ThumbRX,6},{s.ThumbRY,6})"
                    : $"Slot {s.Slot} desconectado";
                Console.WriteLine(text.PadRight(110));
            }
            Thread.Sleep(16);
        }
        return 0;
    }

    private static void PrintHid(int index, HidInterfaceReport r)
    {
        var known = KnownDevices.Find(r.DeviceId);
        Console.WriteLine($"\n  [{index}] {r.DeviceId}  MI={Fmt(r.InterfaceNumber)} COL={Fmt(r.CollectionNumber)}{(r.IsXInputGameInterface ? " IG (XInput)" : "")}  rev {r.ReleaseNumber}");
        if (known is not null) Console.WriteLine($"      Conhecido: {known.Description} ({known.Confidence})");
        Console.WriteLine($"      Fabricante: {r.Manufacturer ?? "?"}  Produto: {r.ProductName ?? "?"}  Serial: {r.SerialNumber ?? "?"}");
        Console.WriteLine($"      Usages: {string.Join(" | ", r.TopLevelUsages)}");
        Console.WriteLine($"      Tamanho máx. relatório  in={r.MaxInputReportLength} out={r.MaxOutputReportLength} feature={r.MaxFeatureReportLength}");
        Console.WriteLine($"      Report IDs  in=[{Ids(r.InputReports)}] out=[{Ids(r.OutputReports)}] feature=[{Ids(r.FeatureReports)}]");
        Console.WriteLine($"      Caminho: {r.DevicePath}");
        foreach (var e in r.Errors) Console.WriteLine($"      ! {e}");
    }

    private static void PrintSlot(XInputSlotReport s)
    {
        if (!s.Connected) { Console.WriteLine($"  Slot {s.Slot}: desconectado"); return; }
        var origin = s.DeviceId is { } id
            ? KnownDevices.IsGameSir(id) ? "GameSir" : id == KnownDevices.Xbox360Wired ? "Xbox 360 / possivelmente virtual (ViGEm)" : "outro"
            : "VID/PID desconhecido";
        Console.WriteLine($"  Slot {s.Slot}: conectado  {s.DeviceId?.ToString() ?? s.DeviceIdError}  ({origin})");
        Console.WriteLine($"      SubType={s.SubType} Flags=0x{s.Flags:X4}  Bateria: tipo={s.BatteryType} nível={s.BatteryLevel}");
    }

    private static void PrintWinRt(GamingInputControllerReport c)
    {
        Console.WriteLine($"  {c.DisplayName} [{c.DeviceId}] sem fio={c.IsWireless} botões={c.ButtonCount} eixos={c.AxisCount} gamepad={c.IsAlsoGamepad}");
        Console.WriteLine(c.BatteryReportAvailable
            ? $"      BatteryReport: status={c.BatteryStatus} restante={c.RemainingCapacityMilliwattHours?.ToString(CultureInfo.InvariantCulture) ?? "null"} mWh " +
              $"total={c.FullChargeCapacityMilliwattHours?.ToString(CultureInfo.InvariantCulture) ?? "null"} mWh taxa={c.ChargeRateMilliwatts?.ToString(CultureInfo.InvariantCulture) ?? "null"} mW"
            : $"      BatteryReport: indisponível{(c.Error is null ? "" : $" ({c.Error})")}");
    }

    private static void Section(string title) => Console.WriteLine($"\n=== {title} ===");
    private static string Fmt(int? n) => n?.ToString("D2", CultureInfo.InvariantCulture) ?? "--";
    private static string Ids(IReadOnlyList<HidReportInfo> reports) =>
        string.Join(",", reports.Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.ReportId}:{r.Length}B")));

    internal static int IntOption(string[] args, string name, int fallback)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
}
