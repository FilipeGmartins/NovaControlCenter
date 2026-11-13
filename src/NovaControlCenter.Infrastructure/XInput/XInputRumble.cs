using NovaControlCenter.Core.Interfaces;

namespace NovaControlCenter.Infrastructure.XInput;

/// <summary>Vibração via XInputSetState (API pública e documentada da Microsoft).</summary>
public sealed class XInputRumble : IRumbleService
{
    public bool TrySetVibration(int slot, double leftMotor, double rightMotor)
    {
        if (slot is < 0 or >= XInputNative.MaxControllers) return false;
        var vibration = new XInputNative.XInputVibration
        {
            LeftMotorSpeed = ToSpeed(leftMotor),
            RightMotorSpeed = ToSpeed(rightMotor),
        };
        return XInputNative.SetState((uint)slot, ref vibration) == XInputNative.ErrorSuccess;
    }

    /// <summary>Converte 0..1 em 0..65535, limitando valores fora da faixa (inclusive NaN → 0).</summary>
    internal static ushort ToSpeed(double intensity) =>
        double.IsNaN(intensity) ? (ushort)0 : (ushort)Math.Round(Math.Clamp(intensity, 0.0, 1.0) * ushort.MaxValue);
}
