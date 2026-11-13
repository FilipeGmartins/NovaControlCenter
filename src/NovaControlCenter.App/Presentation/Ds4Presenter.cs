using NovaControlCenter.Infrastructure.Audio;
using NovaControlCenter.Infrastructure.Ds4;

namespace NovaControlCenter.App.Presentation;

/// <summary>Textos da página PS4. Puro e testável.</summary>
public static class Ds4Presenter
{
    public static StatusText Link(Ds4Snapshot s) => s.Link switch
    {
        Ds4LinkState.Connected => new StatusText(StatusKind.Ok, "PS4 conectado",
            $"DualShock 4 via {(s.IsBluetooth ? "Bluetooth" : "USB")}, enviando dados."),
        Ds4LinkState.Off => new StatusText(StatusKind.Warning, "PS4 desligado",
            "O controle está pareado, mas desligado ou fora de alcance. Aperte o botão PS."),
        _ => new StatusText(StatusKind.Error, "PS4 não encontrado",
            "Pareie o DualShock 4 em Configurações → Dispositivos → Bluetooth (segure Share + PS até piscar)."),
    };

    public static (string Title, string Detail) Battery(Ds4Snapshot s, bool advanced)
    {
        if (s.Link != Ds4LinkState.Connected) return (Presenter.Dash, "Controle não conectado.");
        if (s.Battery is { } b)
        {
            if (b.IsFull) return ("Carregada", "Carga completa, com cabo conectado.");
            var title = $"≈ {b.Percent}%";
            var detail = b.IsCharging
                ? "Carregando. O controle informa a carga em passos de 10%."
                : "O controle informa a carga em passos de 10%.";
            return (title, detail);
        }
        if (s.IsBluetooth && !advanced)
            return ("Indisponível", "Ligue \"Recursos avançados\" para ler a bateria pelo Bluetooth.");
        return ("Lendo…", "Aguardando o primeiro relatório completo do controle.");
    }

    public static string Mode(Ds4Snapshot s) => s.Link != Ds4LinkState.Connected
        ? Presenter.Dash
        : !s.IsBluetooth ? "USB: relatório completo (entradas + bateria)"
        : s.ExtendedActive ? "Bluetooth estendido (0x11): entradas + bateria" : "Bluetooth básico (0x01): só entradas";

    /// <summary>Texto do estado do modo de reconhecimento.</summary>
    public static (StatusKind Kind, string Text) Recognition(Ds4Snapshot s)
    {
        if (s.Mode == Ds4RecognitionMode.PlayStation)
            return (StatusKind.Ok, "Os jogos veem um controle PlayStation. Nenhum controle virtual é criado.");
        if (s.VirtualError is { } error)
            return (StatusKind.Error, error);
        if (s.Link != Ds4LinkState.Connected)
            return (StatusKind.Warning, "O Xbox virtual será criado quando o PS4 conectar.");
        if (!s.VirtualActive)
            return (StatusKind.Warning, "Criando o Xbox virtual…");
        var slot = s.VirtualSlot is int i ? $" (posição XInput {i + 1} de 4)" : "";
        return (StatusKind.Ok, $"Xbox 360 virtual ativo{slot}. Jogos XInput já recebem os comandos do PS4.");
    }

    /// <summary>Texto do cartão de áudio do controle.</summary>
    public static (StatusKind Kind, string Text) Audio(ControllerAudioEndpoints e, bool bluetooth)
    {
        if (e.Speaker is not null || e.Microphone is not null)
        {
            var parts = new List<string>();
            if (e.Speaker is { } s) parts.Add($"alto-falante \"{s.Name}\"");
            if (e.Microphone is { } m) parts.Add($"microfone \"{m.Name}\"");
            return (StatusKind.Ok, "Encontrado: " + string.Join(" e ", parts) + ".");
        }
        return (StatusKind.Warning, bluetooth
            ? "Pelo Bluetooth o áudio do DS4 usa um formato proprietário que o Windows não suporta. Conecte o cabo USB para verificar."
            : "Este controle não expõe alto-falante nem microfone ao Windows: pelo cabo ele aparece só como controle (HID). No DS4 original, surgem os dispositivos \"Wireless Controller\".");
    }
}