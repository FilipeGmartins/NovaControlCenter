using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Services;

/// <summary>
/// Aplica um perfil às entradas (puro, sem estado). 🖥️ Processamento por software: só chega aos jogos
/// quando o app alimenta um controle virtual (ex.: PS4 no modo Xbox).
/// </summary>
public static class InputProcessor
{
    public static ControllerState Apply(ControllerState state, InputProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(profile);
        var p = profile.Normalized();
        return state with
        {
            LeftStick = Stick(state.LeftStick, p.LeftStick),
            RightStick = Stick(state.RightStick, p.RightStick),
            LeftTriggerRaw = Trigger(state.LeftTriggerRaw, p.Triggers),
            RightTriggerRaw = Trigger(state.RightTriggerRaw, p.Triggers),
        };
    }

    /// <summary>
    /// Zona morta radial com reescala (sem "degrau" na borda) + curva de potência. A direção é preservada.
    /// </summary>
    public static StickState Stick(StickState stick, StickSettings s)
    {
        var x = stick.X;
        var y = stick.Y;
        var magnitude = Math.Sqrt((x * x) + (y * y));
        if (magnitude <= s.Deadzone || magnitude == 0) return default;

        var clamped = Math.Min(1.0, magnitude);
        var scaled = (clamped - s.Deadzone) / (1 - s.Deadzone);
        var curved = Math.Pow(scaled, s.Curve);
        var factor = curved / magnitude;
        return new StickState(ToAxis(x * factor), ToAxis(y * factor));
    }

    public static byte Trigger(byte raw, TriggerSettings s)
    {
        var v = raw / 255.0;
        if (v <= s.Deadzone) return 0;
        var scaled = (v - s.Deadzone) / (s.FullPressAt - s.Deadzone);
        return (byte)Math.Round(Math.Clamp(scaled, 0, 1) * 255);
    }

    /// <summary>Vibração ajustada pela intensidade do perfil.</summary>
    public static byte Vibration(byte level, InputProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return (byte)Math.Round(level * Math.Clamp(profile.Vibration, 0, 1));
    }

    private static short ToAxis(double v)
    {
        var c = Math.Clamp(v, -1, 1);
        return (short)Math.Round(c < 0 ? c * 32768 : c * 32767);
    }
}
