using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Services;

/// <summary>
/// Cadeia de responsabilidade de <see cref="IBatteryProvider"/> (ARCHITECTURE §4).
/// Sem fonte real, devolve <see cref="BatteryStatus.Unavailable"/> com o motivo — nunca um valor estimado.
/// </summary>
public sealed class BatteryService(IEnumerable<IBatteryProvider> providers)
{
    public const string NotConnectedReason = "Controle não conectado.";

    public const string NoSourceReason =
        "O receptor 2.4 GHz não informa o nível de bateria ao Windows. " +
        "Use o indicador luminoso do controle como referência.";

    private readonly IReadOnlyList<IBatteryProvider> _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));

    public BatteryStatus GetStatus(ConnectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State != ConnectionState.Connected || snapshot.Controller is null)
            return new BatteryStatus.Unavailable(NotConnectedReason);

        foreach (var provider in _providers)
        {
            if (provider.TryGetStatus(snapshot.Controller) is { } status)
                return status;
        }
        return new BatteryStatus.Unavailable(NoSourceReason);
    }
}
