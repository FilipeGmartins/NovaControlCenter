using System.Globalization;
using NovaControlCenter.Infrastructure.Audio;

/// <summary>
/// Valida o mecanismo de áudio do app em qualquer dispositivo.
///   audio-test                 procura o áudio do controle (Wireless Controller / DUALSHOCK)
///   audio-test --name Realtek  usa outro dispositivo (ex.: o do PC) para validar tom e medidor
/// </summary>
internal static class AudioTestCommand
{
    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var nameIndex = Array.IndexOf(args, "--name");
        var hint = nameIndex >= 0 && nameIndex + 1 < args.Length ? args[nameIndex + 1] : null;
        var audio = new ControllerAudio();
        var found = audio.Find(hint);

        Console.WriteLine($"Procurando: {(hint is null ? "áudio do controle (Wireless Controller / DUALSHOCK)" : $"\"{hint}\"")}");
        Console.WriteLine($"  Saída:   {found.Speaker?.Name ?? "não encontrada"}");
        Console.WriteLine($"  Entrada: {found.Microphone?.Name ?? "não encontrada"}");

        if (found.Speaker is { } speaker && !args.Contains("--no-tone"))
        {
            Console.WriteLine("\nTocando tom de 440 Hz por 0,4 s (volume 15%)...");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await audio.PlayToneAsync(speaker, 440, 0.4, 0.15f);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  Reprodução concluída em {clock.ElapsedMilliseconds} ms"));
        }

        if (found.Microphone is { } mic)
        {
            Console.WriteLine("\nMedindo o microfone por 4 s (fale ou bata palmas)...");
            using var meter = audio.StartMeter(mic);
            var max = 0.0;
            for (var i = 0; i < 20 && !ct.IsCancellationRequested; i++)
            {
                await Task.Delay(200, ct);
                max = Math.Max(max, meter.Level);
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {meter.Level,5:0.000} {new string('█', (int)(meter.Level * 40))}"));
            }
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  Pico máximo: {max:0.000}"));
        }
        return found.Any ? 0 : 2;
    }
}
