namespace NovaControlCenter.Core.Models;

/// <summary>Ajustes de um analógico. 🖥️ Aplicados por software (nada é gravado no controle).</summary>
public sealed record StickSettings
{
    /// <summary>Zona morta radial, 0 a 0,5 (fração do curso).</summary>
    public double Deadzone { get; init; } = 0.05;

    /// <summary>Expoente da curva de resposta: 1 = linear; &gt;1 = mais precisão no centro; &lt;1 = mais rápido no início.</summary>
    public double Curve { get; init; } = 1.0;

    public StickSettings Normalized() => this with
    {
        Deadzone = Math.Clamp(Deadzone, 0, 0.5),
        Curve = Math.Clamp(Curve, 0.4, 3.0),
    };
}

/// <summary>Ajustes dos gatilhos. 🖥️ Aplicados por software.</summary>
public sealed record TriggerSettings
{
    /// <summary>Curso inicial ignorado, 0 a 0,5.</summary>
    public double Deadzone { get; init; }

    /// <summary>Ponto em que o gatilho já conta como 100%, 0,5 a 1 (gatilho "curto").</summary>
    public double FullPressAt { get; init; } = 1.0;

    public TriggerSettings Normalized()
    {
        var dz = Math.Clamp(Deadzone, 0, 0.5);
        return this with { Deadzone = dz, FullPressAt = Math.Clamp(FullPressAt, Math.Max(0.5, dz + 0.05), 1.0) };
    }
}

/// <summary>Perfil de ajustes por software.</summary>
public sealed record InputProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = "Personalizado";

    /// <summary>O perfil "Padrão" não pode ser excluído (sempre existe ao menos um perfil).</summary>
    public bool IsDefault { get; init; }

    public StickSettings LeftStick { get; init; } = new();

    public StickSettings RightStick { get; init; } = new();

    public TriggerSettings Triggers { get; init; } = new();

    /// <summary>Intensidade da vibração repassada ao controle, 0 a 1.</summary>
    public double Vibration { get; init; } = 1.0;

    public InputProfile Normalized() => this with
    {
        LeftStick = LeftStick.Normalized(),
        RightStick = RightStick.Normalized(),
        Triggers = Triggers.Normalized(),
        Vibration = Math.Clamp(Vibration, 0, 1),
    };

    /// <summary>Perfis iniciais sugeridos pela especificação: Padrão, FPS e Corrida.</summary>
    public static IReadOnlyList<InputProfile> BuiltIns() =>
    [
        new() { Name = "Padrão", IsDefault = true },
        new()
        {
            // Mira mais fina no centro e gatilho de disparo curto.
            Name = "FPS",
            LeftStick = new() { Deadzone = 0.06, Curve = 1.0 },
            RightStick = new() { Deadzone = 0.04, Curve = 1.6 },
            Triggers = new() { Deadzone = 0.0, FullPressAt = 0.6 },
            Vibration = 0.6,
        },
        new()
        {
            // Direção suave e gatilhos com curso completo para dosar acelerador/freio.
            Name = "Corrida",
            LeftStick = new() { Deadzone = 0.03, Curve = 1.3 },
            RightStick = new() { Deadzone = 0.08, Curve = 1.0 },
            Triggers = new() { Deadzone = 0.05, FullPressAt = 1.0 },
            Vibration = 1.0,
        },
    ];
}
