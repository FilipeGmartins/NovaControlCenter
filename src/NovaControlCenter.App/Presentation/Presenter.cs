using System.Globalization;
using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.App.Presentation;

public enum StatusKind
{
    Ok,
    Warning,
    Error,
}

public sealed record StatusText(StatusKind Kind, string Title, string Description);

/// <summary>Converte modelos do Core em textos de interface. Puro e testável.</summary>
public static class Presenter
{
    public const string Dash = "—";

    public static StatusText Connection(ConnectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.State switch
        {
            ConnectionState.Connected => new StatusText(StatusKind.Ok, "Controle conectado",
                "Pronto para uso. Entradas e vibração disponíveis em Monitoramento."),
            ConnectionState.ControllerOff => new StatusText(StatusKind.Warning, "Controle desligado",
                "O receptor está conectado, mas o controle está desligado ou fora de alcance. Ligue o controle para continuar."),
            _ => new StatusText(StatusKind.Error, "Receptor não encontrado",
                "Conecte o receptor USB 2.4 GHz do GameSir a uma porta USB do computador."),
        };
    }

    public static string Receiver(ConnectionSnapshot snapshot) => snapshot.State switch
    {
        ConnectionState.Connected => $"{KnownDevices.NovaLiteConnected} — controle conectado",
        ConnectionState.ControllerOff => $"{KnownDevices.NovaLiteReceiverIdle} — aguardando o controle",
        _ => "Não detectado",
    };

    public static string Slot(ControllerInfo? controller)
    {
        if (controller?.XInputSlot is not int slot) return "Não identificado";
        var how = controller.SlotMatch switch
        {
            SlotMatch.Exact => "identificação exata",
            SlotMatch.Approximate => "identificação aproximada",
            _ => "",
        };
        return string.Create(CultureInfo.InvariantCulture, $"{slot + 1} de 4 ({how})");
    }

    public static string Model(ControllerInfo? controller) => controller?.ModelConfidence switch
    {
        Confidence.Confirmed => "Modelo confirmado",
        Confidence.Hypothesis => $"Modelo presumido pelo ID do receptor ({controller.DeviceId})",
        _ => Dash,
    };

    public static (string Title, string Detail) Battery(BatteryStatus status) => status switch
    {
        BatteryStatus.Unavailable u => ("Não informada", u.Reason),
        BatteryStatus.Level l => (l.Value switch
        {
            BatteryLevel.Empty => "Vazia",
            BatteryLevel.Low => "Baixa",
            BatteryLevel.Medium => "Média",
            _ => "Cheia",
        }, $"Nível informado por {l.Source} (4 níveis, sem porcentagem)."),
        BatteryStatus.Percent p => (string.Create(CultureInfo.InvariantCulture, $"{p.Value}%"), $"Informado por {p.Source}."),
        _ => (Dash, string.Empty),
    };

    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        return span.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours} h {span.Minutes:00} min")
            : span.TotalMinutes >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{span.Minutes} min {span.Seconds:00} s")
                : string.Create(CultureInfo.InvariantCulture, $"{span.Seconds} s");
    }
}
