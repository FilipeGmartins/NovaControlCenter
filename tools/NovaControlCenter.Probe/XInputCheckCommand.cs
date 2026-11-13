using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.XInput;

/// <summary>
/// Simula um jogo XInput: vibra um slot (como um jogo faria) e registra quais botões/eixos chegam nele.
/// Uso: xinput-check &lt;slot&gt; [--seconds 20]
/// </summary>
internal static class XInputCheckCommand
{
    public static int Run(string[] args, CancellationToken ct)
    {
        var slot = args.Length > 1 && int.TryParse(args[1], out var s) ? s : 0;
        var seconds = args.Contains("--seconds") ? int.Parse(args[Array.IndexOf(args, "--seconds") + 1], System.Globalization.CultureInfo.InvariantCulture) : 20;
        var reader = new XInputReader();
        if (reader.Read(slot) is null)
        {
            Console.WriteLine($"Slot {slot} sem controle.");
            return 2;
        }

        Console.WriteLine($"Slot {slot}: pedindo vibração como um jogo faria (1,5 s)...");
        var rumble = new XInputRumble();
        Console.WriteLine($"  XInputSetState: {(rumble.TrySetVibration(slot, 0.8, 0.8) ? "OK" : "FALHOU")}");
        Thread.Sleep(1500);
        rumble.TrySetVibration(slot, 0, 0);

        Console.WriteLine($"\nRegistrando entradas por {seconds} s: aperte botões e mova analógicos/gatilhos.");
        var seen = GamepadButtons.None;
        short lxMin = 0, lxMax = 0, lyMin = 0, lyMax = 0;
        byte lt = 0, rt = 0;
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end && !ct.IsCancellationRequested)
        {
            if (reader.Read(slot) is { } st)
            {
                seen |= st.Buttons;
                lxMin = Math.Min(lxMin, st.LeftStick.RawX); lxMax = Math.Max(lxMax, st.LeftStick.RawX);
                lyMin = Math.Min(lyMin, st.LeftStick.RawY); lyMax = Math.Max(lyMax, st.LeftStick.RawY);
                lt = Math.Max(lt, st.LeftTriggerRaw); rt = Math.Max(rt, st.RightTriggerRaw);
            }
            Thread.Sleep(8);
        }
        var all = Enum.GetValues<GamepadButtons>().Where(b => b != GamepadButtons.None).ToArray();
        Console.WriteLine($"\nBotões recebidos no slot {slot}: {all.Count(b => (seen & b) == b)} de {all.Length}");
        Console.WriteLine($"  vistos:  {string.Join(" ", all.Where(b => (seen & b) == b))}");
        Console.WriteLine($"  faltam:  {string.Join(" ", all.Where(b => (seen & b) != b))}");
        Console.WriteLine($"Analógico esquerdo X [{lxMin} .. {lxMax}]  Y [{lyMin} .. {lyMax}]   Gatilhos máx L2={lt} R2={rt}");
        return 0;
    }
}
