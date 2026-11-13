using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;
using NovaControlCenter.Infrastructure.DeviceDetection;
using NovaControlCenter.Infrastructure.XInput;

/// <summary>
/// Validação da Sprint 03: bateria (via BatteryService), vibração e cobertura de botões/eixos via InputMonitor.
/// </summary>
internal static class InputTestCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed class AxisRange
    {
        public short Min { get; private set; } = short.MaxValue;
        public short Max { get; private set; } = short.MinValue;

        public void Add(short v)
        {
            if (v < Min) Min = v;
            if (v > Max) Max = v;
        }
    }

    public static async Task<int> RunAsync(int seconds, bool rumble, string outputDir, string stamp, CancellationToken ct)
    {
        using var presence = new HidDevicePresence();
        var reader = new XInputReader();
        await using var connection = new ConnectionMonitor(presence, reader, TimeProvider.System, NullLogger<ConnectionMonitor>.Instance);
        connection.Start();

        Console.WriteLine("Aguardando o controle conectar (até 60 s)...");
        var waitUntil = DateTime.UtcNow.AddSeconds(60);
        while (connection.Current.Controller?.XInputSlot is null && DateTime.UtcNow < waitUntil)
            await Task.Delay(200, ct);
        if (connection.Current.Controller?.XInputSlot is not int slot)
        {
            Console.WriteLine($"Controle não conectado (estado: {connection.Current.State}).");
            return 2;
        }
        Console.WriteLine($"Conectado no slot {slot} ({connection.Current.Controller.SlotMatch}).");

        var battery = new BatteryService([new XInputBatteryProvider()]).GetStatus(connection.Current);
        Console.WriteLine($"Bateria: {Describe(battery)}\n");

        var rumbleLog = new List<string>();
        if (rumble) await RumbleSequenceAsync(slot, rumbleLog, ct);

        // ---- Cobertura de entradas ----
        var seen = GamepadButtons.None;
        var lx = new AxisRange(); var ly = new AxisRange(); var rx = new AxisRange(); var ry = new AxisRange();
        byte ltMax = 0, rtMax = 0;
        ControllerState? rest = null;
        var samples = 0;
        var maxRate = 0;
        var gate = new object();

        await using var input = new InputMonitor(reader, () => connection.Current.Controller?.XInputSlot, TimeProvider.System, NullLogger<InputMonitor>.Instance);
        input.InputChanged += (_, s) =>
        {
            if (s is null) return;
            lock (gate)
            {
                var st = s.State;
                rest ??= st;
                samples++;
                seen |= st.Buttons;
                lx.Add(st.LeftStick.RawX); ly.Add(st.LeftStick.RawY);
                rx.Add(st.RightStick.RawX); ry.Add(st.RightStick.RawY);
                ltMax = Math.Max(ltMax, st.LeftTriggerRaw);
                rtMax = Math.Max(rtMax, st.RightTriggerRaw);
            }
        };
        input.Start();

        var all = Enum.GetValues<GamepadButtons>().Where(b => b != GamepadButtons.None).ToArray();
        Console.WriteLine($"""
            TESTE DE ENTRADAS — {seconds}s
              1. Aperte CADA botão uma vez (incluindo Home/Guide, Start, Back/Select, L3 e R3 = clicar os analógicos).
              2. Gire cada analógico em círculos, encostando nas bordas.
              3. Aperte L2 e R2 até o fim.

            """);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds && !ct.IsCancellationRequested)
        {
            await Task.Delay(100, CancellationToken.None);
            maxRate = Math.Max(maxRate, input.UpdatesPerSecond);
            var cur = input.Latest?.State;
            string missing;
            lock (gate) missing = string.Join(" ", all.Where(b => (seen & b) == 0));
            Console.Write($"\r  [{seconds - (int)clock.Elapsed.TotalSeconds,3}s] agora=[{cur?.Buttons,-30}] faltam: {(missing.Length == 0 ? "nenhum ✓" : missing),-90}");
        }

        Console.WriteLine("\n\n=== RESULTADO ===");
        lock (gate)
        {
            foreach (var b in all)
                Console.WriteLine($"  {((seen & b) != 0 ? "✓" : "✗")} {b}");
            Console.WriteLine($"""

                  Analógico esquerdo  X [{lx.Min,6} .. {lx.Max,6}]   Y [{ly.Min,6} .. {ly.Max,6}]
                  Analógico direito   X [{rx.Min,6} .. {rx.Max,6}]   Y [{ry.Min,6} .. {ry.Max,6}]
                  Gatilhos máx.       L2 {ltMax,3}/255   R2 {rtMax,3}/255
                  Repouso (1ª amostra) L=({rest?.LeftStick.RawX},{rest?.LeftStick.RawY}) R=({rest?.RightStick.RawX},{rest?.RightStick.RawY}) LT={rest?.LeftTriggerRaw} RT={rest?.RightTriggerRaw}
                  Amostras: {samples}   Taxa máx. observada: {maxRate} atualizações/s
                """);

            var file = Path.Combine(outputDir, $"input-test-{stamp}.json");
            File.WriteAllText(file, JsonSerializer.Serialize(new
            {
                Timestamp = DateTimeOffset.Now,
                Slot = slot,
                Battery = Describe(battery),
                Rumble = rumbleLog,
                ButtonsSeen = all.Where(b => (seen & b) != 0).Select(b => b.ToString()),
                ButtonsMissing = all.Where(b => (seen & b) == 0).Select(b => b.ToString()),
                LeftStick = new { XMin = lx.Min, XMax = lx.Max, YMin = ly.Min, YMax = ly.Max },
                RightStick = new { XMin = rx.Min, XMax = rx.Max, YMin = ry.Min, YMax = ry.Max },
                LeftTriggerMax = ltMax,
                RightTriggerMax = rtMax,
                Rest = rest,
                Samples = samples,
                MaxUpdatesPerSecond = maxRate,
            }, JsonOptions));
            Console.WriteLine($"Resumo salvo em {file}");
        }
        return 0;
    }

    private static async Task RumbleSequenceAsync(int slot, List<string> log, CancellationToken ct)
    {
        var rumble = new XInputRumble();
        (string Label, double L, double R, int Ms)[] steps =
        [
            ("Motor ESQUERDO 60%", 0.6, 0.0, 1200),
            ("pausa", 0, 0, 800),
            ("Motor DIREITO 60%", 0.0, 0.6, 1200),
            ("pausa", 0, 0, 800),
            ("AMBOS 100%", 1.0, 1.0, 600),
        ];
        Console.WriteLine("TESTE DE VIBRAÇÃO — segure o controle:");
        try
        {
            foreach (var (label, l, r, ms) in steps)
            {
                var ok = rumble.TrySetVibration(slot, l, r);
                if (label != "pausa")
                {
                    var line = string.Create(CultureInfo.InvariantCulture, $"{label}: XInputSetState {(ok ? "OK" : "FALHOU")}");
                    log.Add(line);
                    Console.WriteLine($"  {line}");
                }
                await Task.Delay(ms, ct);
            }
        }
        finally
        {
            rumble.TrySetVibration(slot, 0, 0); // nunca deixar o motor ligado
        }
        Console.WriteLine();
    }

    private static string Describe(BatteryStatus status) => status switch
    {
        BatteryStatus.Unavailable u => $"indisponível — {u.Reason}",
        BatteryStatus.Level l => $"{l.Value} (fonte: {l.Source})",
        BatteryStatus.Percent p => $"{p.Value}% (fonte: {p.Source})",
        _ => status.ToString(),
    };
}
