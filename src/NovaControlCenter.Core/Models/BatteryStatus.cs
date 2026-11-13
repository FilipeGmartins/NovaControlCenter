namespace NovaControlCenter.Core.Models;

/// <summary>Níveis discretos — é o que fontes como XInput informam. Não é porcentagem.</summary>
public enum BatteryLevel
{
    Empty,
    Low,
    Medium,
    Full,
}

/// <summary>
/// Estado da bateria. Tipo fechado: só existe porcentagem quando uma fonte real a fornece (ADR-07).
/// </summary>
public abstract record BatteryStatus
{
    private BatteryStatus() { }

    /// <summary>Nenhuma fonte forneceu dado real. <see cref="Reason"/> é exibido ao usuário.</summary>
    public sealed record Unavailable(string Reason) : BatteryStatus;

    /// <summary>Nível discreto informado pelo dispositivo.</summary>
    public sealed record Level(BatteryLevel Value, string Source) : BatteryStatus;

    /// <summary>Porcentagem real informada pelo dispositivo (0–100).</summary>
    public sealed record Percent : BatteryStatus
    {
        public Percent(int value, string source)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
            Value = value;
            Source = source;
        }

        public int Value { get; }
        public string Source { get; }
    }
}
