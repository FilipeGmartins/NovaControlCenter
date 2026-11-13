using System.Globalization;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.Ds4;

/// <summary>
/// Validação do DualShock 4 (D4-03).
///   ds4 validate : fase 1 básica (só leitura), fase 2 estendida (bateria), fase 3 teste de luz.
///   ds4 light RRGGBB : define a cor da luz.
/// </summary>
internal static class Ds4Command
{
    public static int Run(string[] args, CancellationToken ct)
    {
        var sub = args.Length > 1 ? args[1].ToLowerInvariant() : "validate";
        var devices = Ds4Connection.FindDevices();
        var device = devices.Count > 0 ? devices[0] : null;
        if (device is null)
        {
            Console.WriteLine("Nenhum DualShock 4 encontrado (054C:05C4/09CC). Ligue o controle com o botão PS.");
            return 2;
        }
        using var ds4 = Ds4Connection.TryOpen(device);
        if (ds4 is null)
        {
            Console.WriteLine("Não foi possível abrir o DS4 (outro programa pode estar com acesso exclusivo, ex.: DS4Windows).");
            return 3;
        }
        Console.WriteLine($"DS4 {ds4.ProductId:X4} via {(ds4.IsBluetooth ? "Bluetooth" : "USB")}");

        return sub switch
        {
            "light" when args.Length > 2 => SetLight(ds4, args[2]),
            "light-paths" => LightPaths(ds4),
            "rumble-paths" => RumblePaths(ds4),
            _ => Validate(ds4, ct),
        };
    }

    /// <summary>Testa cada caminho de envio com uma cor diferente, para o usuário dizer qual apareceu.</summary>
    private static int LightPaths(Ds4Connection ds4)
    {
        if (!ds4.IsLinkConnectedAsync().GetAwaiter().GetResult())
        {
            Console.WriteLine("DS4 desligado. Aperte PS e rode de novo.");
            return 4;
        }
        Console.WriteLine("Observe a luz do controle (6 s por cor):");
        (string Label, Ds4Color Color, Ds4Connection.OutputPath Path, bool Full, byte Seed)[] variants =
        [
            ("5) CIANO    — interrupção, 547 B, CRC no fim, semente 0xA2", new Ds4Color(0, 255, 255), Ds4Connection.OutputPath.Interrupt, true, 0xA2),
            ("6) MAGENTA  — controle, 547 B, CRC no fim, semente 0x52", new Ds4Color(255, 0, 255), Ds4Connection.OutputPath.Control, true, 0x52),
            ("7) BRANCO   — controle, 78 B, semente 0x52", new Ds4Color(255, 255, 255), Ds4Connection.OutputPath.Control, false, 0x52),
            ("8) LARANJA  — controle, 547 B, CRC no fim, semente 0xA2", new Ds4Color(255, 90, 0), Ds4Connection.OutputPath.Control, true, 0xA2),
        ];
        foreach (var v in variants)
        {
            var ok = ds4.SendOutput(v.Color, path: v.Path, fullLength: v.Full, crcSeed: v.Seed);
            Console.WriteLine($"  {v.Label}: {(ok ? "aceito" : "FALHOU")}");
            Thread.Sleep(6000);
        }        Console.WriteLine("Fim. Qual cor apareceu?");
        return 0;
    }

    /// <summary>Vibração + luz no mesmo relatório: se vibrar, o caminho chega ao controle.</summary>
    private static int RumblePaths(Ds4Connection ds4)
    {
        if (!ds4.IsLinkConnectedAsync().GetAwaiter().GetResult())
        {
            Console.WriteLine("DS4 desligado. Aperte PS e rode de novo.");
            return 4;
        }
        (string Label, Ds4Color Color, Ds4Connection.OutputPath Path, byte Hw, byte? Flags)[] variants =
        [
            ("A) controle,    78 B, 0xC0 (Linux)", new Ds4Color(255, 0, 0), Ds4Connection.OutputPath.Control, 0xC0, null),
            ("B) interrupção, 78 B, 0xC0 (Linux)", new Ds4Color(0, 255, 0), Ds4Connection.OutputPath.Interrupt, 0xC0, null),
            ("C) controle,    78 B, 0x84 + flags 0xF7 (DS4Windows)", new Ds4Color(0, 0, 255), Ds4Connection.OutputPath.Control, 0x84, (byte)0xF7),
            ("D) interrupção, 78 B, 0x84 + flags 0xF7 (DS4Windows)", new Ds4Color(255, 200, 0), Ds4Connection.OutputPath.Interrupt, 0x84, (byte)0xF7),
        ];
        Console.WriteLine("Segure o controle. Cada variante vibra por 2 s (e tenta mudar a luz), com 3 s de pausa:");
        foreach (var v in variants)
        {
            var ok = ds4.SendOutput(v.Color, 200, 200, includeRumble: true, v.Path, v.Hw, validFlags: v.Flags);
            Console.WriteLine($"  {v.Label}: {(ok ? "aceito" : "FALHOU")}");
            Thread.Sleep(2000);
            ds4.SendOutput(v.Color, 0, 0, includeRumble: true, v.Path, v.Hw, validFlags: v.Flags);
            Thread.Sleep(3000);
        }
        Console.WriteLine("Fim. Em quais letras o controle vibrou?");
        return 0;
    }

    private static int SetLight(Ds4Connection ds4, string hex)
    {
        var rgb = Convert.FromHexString(hex.TrimStart('#'));
        var ok = ds4.SendOutput(new Ds4Color(rgb[0], rgb[1], rgb[2]));
        Console.WriteLine(ok ? $"Luz definida para #{hex.TrimStart('#').ToUpperInvariant()}" : "Falha ao enviar o relatório de saída");
        return ok ? 0 : 1;
    }

    private static int Validate(Ds4Connection ds4, CancellationToken ct)
    {
        Console.WriteLine("Aguardando o link Bluetooth do DS4 (aperte o botão PS) — até 3 min...");
        var until = DateTime.UtcNow.AddMinutes(3);
        while (!ds4.IsLinkConnectedAsync().GetAwaiter().GetResult())
        {
            if (DateTime.UtcNow > until || ct.IsCancellationRequested)
            {
                Console.WriteLine("DS4 não conectou. Nada foi enviado ao controle.");
                return 4;
            }
            Thread.Sleep(1000);
        }
        Console.WriteLine("Link Bluetooth ativo. Começando em 3 s...");
        Thread.Sleep(3000);

        // ---- Fase 1: modo básico, nada é enviado ----
        Console.WriteLine("\nFASE 1 (15 s, somente leitura): aperte botões, mova os analógicos e os gatilhos.");
        var basic = Sample(ds4, TimeSpan.FromSeconds(15), ct);
        Print("Fase 1", basic);
        if (basic.Reports == 0)
        {
            Console.WriteLine("  Nenhum relatório recebido em modo básico. Prosseguindo mesmo assim para a fase 2.");
        }

        // ---- Fase 2: modo estendido ----
        Console.WriteLine("\nFASE 2 (10 s): ativando o modo estendido (feature 0x05) para ler a bateria...");
        Console.WriteLine($"  Feature 0x05: {(ds4.EnableExtendedMode() ? "OK" : "FALHOU")}");
        var extended = Sample(ds4, TimeSpan.FromSeconds(10), ct);
        Print("Fase 2", extended);

        // ---- Fase 3: luz ----
        Console.WriteLine("\nFASE 3: teste de luz — observe a barra de luz do controle.");
        (string Name, Ds4Color Color)[] steps =
        [
            ("vermelho", new Ds4Color(255, 0, 0)),
            ("verde", new Ds4Color(0, 255, 0)),
            ("azul", new Ds4Color(0, 0, 255)),
            ("violeta (Nova)", new Ds4Color(0x9B, 0x6B, 0xFF)),
        ];
        foreach (var (name, color) in steps)
        {
            var ok = ds4.SendOutput(color);
            Console.WriteLine($"  {name,-16} {(ok ? "enviado" : "FALHOU")}");
            Thread.Sleep(1500);
        }
        Console.WriteLine("  A luz fica violeta até o controle ser desligado (ou outra cor ser definida).");
        return 0;
    }

    private sealed record SampleResult(int Reports, int Basic, int Extended, GamepadButtons Buttons, Ds4Battery? Battery, bool Touchpad, ControllerState? Last);

    private static SampleResult Sample(Ds4Connection ds4, TimeSpan duration, CancellationToken ct)
    {
        int reports = 0, basic = 0, extended = 0;
        var seen = GamepadButtons.None;
        Ds4Battery? battery = null;
        var touch = false;
        ControllerState? last = null;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed < duration && !ct.IsCancellationRequested)
        {
            Ds4Input? input;
            try { input = ds4.Read(); }
            catch (IOException) { Console.WriteLine("  DS4 desconectado."); break; }
            if (input is null) continue;

            reports++;
            if (input.Extended) extended++; else basic++;
            seen |= input.State.Buttons;
            touch |= input.TouchpadClick;
            battery = input.Battery ?? battery;
            last = input.State;
            if (reports % 40 == 1)
            {
                var s = input.State;
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"  [{(input.Extended ? "0x11" : "0x01")}] btn={s.Buttons,-30} L=({s.LeftStick.X,5:0.00},{s.LeftStick.Y,5:0.00}) R=({s.RightStick.X,5:0.00},{s.RightStick.Y,5:0.00}) L2={s.LeftTriggerRaw,3} R2={s.RightTriggerRaw,3} bat={Describe(input.Battery)}"));
            }
        }
        return new SampleResult(reports, basic, extended, seen, battery, touch, last);
    }

    private static void Print(string phase, SampleResult r)
    {
        Console.WriteLine($"  {phase}: {r.Reports} relatórios (básicos {r.Basic}, estendidos {r.Extended})");
        Console.WriteLine($"  Botões vistos: {r.Buttons}{(r.Touchpad ? " + clique do touchpad" : "")}");
        Console.WriteLine($"  Bateria: {Describe(r.Battery)}");
    }

    private static string Describe(Ds4Battery? b) => b is not { } v
        ? "n/d"
        : v.IsFull ? "cheia (cabo)" : $"~{v.Percent}%{(v.IsCharging ? " carregando" : "")} (nível bruto {v.RawLevel})";
}
