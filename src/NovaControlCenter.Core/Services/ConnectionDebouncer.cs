using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Services;

/// <summary>
/// Suaviza a re-enumeração USB do receptor: ao trocar entre 0x1040 e 0x2106 há ~1 s sem nenhuma interface.
/// Transições para <see cref="ConnectionState.ReceiverNotFound"/> só valem após persistirem por <see cref="Hold"/>;
/// as demais transições são imediatas.
/// </summary>
public sealed class ConnectionDebouncer(TimeProvider time, TimeSpan hold)
{
    public static readonly TimeSpan DefaultHold = TimeSpan.FromSeconds(2);

    private ResolvedConnection _stable = new(ConnectionState.ReceiverNotFound, null);
    private DateTimeOffset? _missingSince;

    public TimeSpan Hold { get; } = hold;

    public ResolvedConnection Current => _stable;

    /// <summary>Alimenta uma leitura bruta e devolve o estado estável.</summary>
    public ResolvedConnection Update(ResolvedConnection raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (raw.State != ConnectionState.ReceiverNotFound)
        {
            _missingSince = null;
            _stable = raw;
            return _stable;
        }

        if (_stable.State == ConnectionState.ReceiverNotFound) return _stable;

        var now = time.GetUtcNow();
        _missingSince ??= now;
        if (now - _missingSince.Value >= Hold)
        {
            _stable = raw;
            _missingSince = null;
        }
        return _stable;
    }
}
