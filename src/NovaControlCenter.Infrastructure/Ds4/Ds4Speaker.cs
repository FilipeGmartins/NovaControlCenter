using Windows.Devices.Enumeration;

namespace NovaControlCenter.Infrastructure.Ds4;

/// <summary>
/// Alto-falante do DS4. No controle original, pelo cabo USB, o Windows cria uma saída de áudio
/// "Wireless Controller". Pelo Bluetooth o áudio usa um fluxo proprietário não suportado pelo Windows.
/// Ver HARDWARE_DIAGNOSTICS §3.10.7.
/// </summary>
public static class Ds4Speaker
{
    private static readonly string[] NameHints = ["Wireless Controller", "DUALSHOCK", "DualShock"];

    /// <summary>Nome da saída de áudio do controle, ou null se o Windows não expõe nenhuma.</summary>
    public static async Task<string?> FindOutputAsync()
    {
        var outputs = await DeviceInformation.FindAllAsync(DeviceClass.AudioRender);
        return outputs
            .Select(d => d.Name)
            .FirstOrDefault(name => NameHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)));
    }
}
