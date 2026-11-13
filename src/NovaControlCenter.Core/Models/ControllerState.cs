namespace NovaControlCenter.Core.Models;

/// <summary>Botões do gamepad. Os valores seguem o layout de bits do XInput por conveniência.</summary>
[Flags]
public enum GamepadButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    Guide = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

/// <summary>Analógico com valores brutos (-32768..32767) e normalizados (-1..1).</summary>
public readonly record struct StickState(short RawX, short RawY)
{
    public double X => Normalize(RawX);
    public double Y => Normalize(RawY);

    /// <summary>Distância do centro, limitada a 1.</summary>
    public double Magnitude => Math.Min(1.0, Math.Sqrt((X * X) + (Y * Y)));

    private static double Normalize(short raw) => raw < 0 ? raw / 32768.0 : raw / 32767.0;
}

/// <summary>Leitura instantânea das entradas do controle.</summary>
public sealed record ControllerState(
    uint PacketNumber,
    GamepadButtons Buttons,
    byte LeftTriggerRaw,
    byte RightTriggerRaw,
    StickState LeftStick,
    StickState RightStick)
{
    public static readonly ControllerState Neutral = new(0, GamepadButtons.None, 0, 0, default, default);

    public double LeftTrigger => LeftTriggerRaw / 255.0;
    public double RightTrigger => RightTriggerRaw / 255.0;

    public bool IsPressed(GamepadButtons button) => (Buttons & button) == button && button != GamepadButtons.None;
}
