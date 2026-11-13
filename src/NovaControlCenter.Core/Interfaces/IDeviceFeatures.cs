using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Interfaces;

/// <summary>Fonte de bateria. Retorna null quando não tem dado REAL para o controle.</summary>
public interface IBatteryProvider
{
    string Name { get; }

    BatteryStatus? TryGetStatus(ControllerInfo controller);
}

/// <summary>Vibração via API documentada (XInputSetState). Intensidades de 0 a 1.</summary>
public interface IRumbleService
{
    bool TrySetVibration(int slot, double leftMotor, double rightMotor);
}
