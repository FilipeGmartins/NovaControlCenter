using System.Text;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.Ds4;

namespace NovaControlCenter.Tests.Ds4;

public class Crc32Tests
{
    [Fact]
    public void Compute_MatchesStandardCheckValue() =>
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));

    [Fact]
    public void ComputeWithSeed_EqualsCrcOfConcatenation()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        Assert.Equal(Crc32.Compute([0xA2, .. data]), Crc32.Compute(0xA2, data));
    }
}

public class Ds4InputTests
{
    /// <summary>Relatório no layout USB (índice 0 = report ID), com sticks centrados e d-pad solto.</summary>
    private static byte[] UsbLayout(int length = 64)
    {
        var r = new byte[length];
        r[0] = 0x01;
        r[1] = r[2] = r[3] = r[4] = 128;
        r[5] = 0x08; // hat solto
        return r;
    }

    /// <summary>Converte o layout USB para o 0x11 Bluetooth (2 bytes de cabeçalho a mais).</summary>
    private static byte[] ToBluetoothFull(byte[] usb)
    {
        var bt = new byte[78];
        bt[0] = 0x11;
        bt[1] = 0xC0;
        usb.AsSpan(1).CopyTo(bt.AsSpan(3));
        return bt;
    }

    [Fact]
    public void BluetoothBasic_HasInputsButNoBattery()
    {
        var r = UsbLayout(10);
        r[5] = 0x08 | 0x20; // xis
        var input = Ds4Protocol.ParseInput(r, bluetooth: true, 1)!;

        Assert.True(input.State.IsPressed(GamepadButtons.A));
        Assert.Null(input.Battery);
        Assert.False(input.Extended);
    }

    [Fact]
    public void BluetoothFull_ReadsBatteryAtShiftedOffset()
    {
        var usb = UsbLayout();
        usb[30] = 0x08; // 80%, sem cabo
        var input = Ds4Protocol.ParseInput(ToBluetoothFull(usb), bluetooth: true, 1)!;

        Assert.True(input.Extended);
        Assert.Equal(80, input.Battery!.Value.Percent);
        Assert.False(input.Battery.Value.CablePlugged);
    }

    [Fact]
    public void Usb_ReadsBatteryDirectly()
    {
        var usb = UsbLayout();
        usb[30] = 0x15; // cabo + nível 5
        var battery = Ds4Protocol.ParseInput(usb, bluetooth: false, 1)!.Battery!.Value;

        Assert.Equal(50, battery.Percent);
        Assert.True(battery.IsCharging);
        Assert.False(battery.IsFull);
    }

    [Fact]
    public void Battery_Level11WithCable_IsFull()
    {
        var b = new Ds4Battery(11, true);
        Assert.True(b.IsFull);
        Assert.False(b.IsCharging);
        Assert.Equal(100, b.Percent);
    }

    [Theory]
    [InlineData(0, GamepadButtons.DPadUp)]
    [InlineData(1, GamepadButtons.DPadUp | GamepadButtons.DPadRight)]
    [InlineData(4, GamepadButtons.DPadDown)]
    [InlineData(6, GamepadButtons.DPadLeft)]
    [InlineData(8, GamepadButtons.None)]
    public void Hat_MapsToDPad(int hat, GamepadButtons expected)
    {
        var r = UsbLayout(10);
        r[5] = (byte)hat;
        Assert.Equal(expected, Ds4Protocol.ParseInput(r, true, 1)!.State.Buttons);
    }

    [Fact]
    public void Buttons_MapToXboxEquivalents()
    {
        var r = UsbLayout(10);
        r[5] = 0x08 | 0x10 | 0x40 | 0x80; // quadrado, círculo, triângulo
        r[6] = 0x01 | 0x02 | 0x10 | 0x20 | 0x40 | 0x80;
        r[7] = 0x01 | 0x02; // PS + clique do touchpad
        var input = Ds4Protocol.ParseInput(r, true, 1)!;

        var expected = GamepadButtons.X | GamepadButtons.B | GamepadButtons.Y | GamepadButtons.LeftShoulder |
                       GamepadButtons.RightShoulder | GamepadButtons.Back | GamepadButtons.Start |
                       GamepadButtons.LeftThumb | GamepadButtons.RightThumb | GamepadButtons.Guide;
        Assert.Equal(expected, input.State.Buttons);
        Assert.True(input.TouchpadClick);
    }

    [Fact]
    public void Sticks_YIsInvertedAndCenterIsZero()
    {
        var r = UsbLayout(10);
        r[2] = 0;   // esquerdo totalmente para cima no DS4
        r[3] = 255; // direito totalmente para a direita
        var s = Ds4Protocol.ParseInput(r, true, 1)!.State;

        Assert.Equal(0, s.RightStick.RawY);
        Assert.True(s.LeftStick.Y > 0.99);
        Assert.True(s.RightStick.X > 0.99);
        Assert.Equal(0, Ds4Protocol.Axis(128));
    }

    [Fact]
    public void Triggers_AreRaw()
    {
        var r = UsbLayout(10);
        r[8] = 200;
        r[9] = 255;
        var s = Ds4Protocol.ParseInput(r, true, 1)!.State;
        Assert.Equal(200, s.LeftTriggerRaw);
        Assert.Equal(255, s.RightTriggerRaw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("050102")]
    [InlineData("010102")]
    public void UnknownOrShortReports_ReturnNull(string hex) =>
        Assert.Null(Ds4Protocol.ParseInput(Convert.FromHexString(hex), true, 1));
}

public class Ds4OutputTests
{
    [Fact]
    public void LightReport_HasHeaderFlagsColorAndValidCrc()
    {
        var r = Ds4Protocol.BuildBluetoothOutput(new Ds4Color(0x12, 0x34, 0x56));

        Assert.Equal(78, r.Length);
        Assert.Equal(0x11, r[0]);
        Assert.Equal(0xC0, r[1]);
        Assert.Equal(0x02, r[3]);                    // só a luz
        Assert.Equal(0x12, r[8]);
        Assert.Equal(0x34, r[9]);
        Assert.Equal(0x56, r[10]);
        Assert.Equal(0, r[6]);
        Assert.Equal(0, r[7]);

        var crc = Crc32.Compute(0xA2, r.AsSpan(0, 74));
        Assert.Equal(crc, BitConverter.ToUInt32(r, 74));
    }

    [Fact]
    public void RumbleReport_SetsMotorFlagAndBytes()
    {
        var r = Ds4Protocol.BuildBluetoothOutput(null, rumbleLeft: 200, rumbleRight: 50, includeRumble: true);
        Assert.Equal(0x01, r[3]);
        Assert.Equal(50, r[6]);   // leve
        Assert.Equal(200, r[7]);  // pesado
    }
}

public class Ds4ConnectionTests
{
    [Theory]
    [InlineData("a1b2c3d4e5f6", 0xA1B2C3D4E5F6UL)]
    [InlineData("A1:B2:C3:D4:E5:F6", 0xA1B2C3D4E5F6UL)]
    public void TryParseAddress_AcceptsHidSerialFormats(string serial, ulong expected) =>
        Assert.Equal(expected, NovaControlCenter.Infrastructure.Ds4.Ds4Connection.TryParseAddress(serial));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xyz")]
    public void TryParseAddress_RejectsInvalid(string? serial) =>
        Assert.Null(NovaControlCenter.Infrastructure.Ds4.Ds4Connection.TryParseAddress(serial));

    [Fact]
    public void IsBluetoothPath_DetectsHidOverBluetooth()
    {
        Assert.True(NovaControlCenter.Infrastructure.Ds4.Ds4Connection.IsBluetoothPath(
            @"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&09cc#8&e8e7a17&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"));
        Assert.False(NovaControlCenter.Infrastructure.Ds4.Ds4Connection.IsBluetoothPath(@"\\?\hid#vid_054c&pid_09cc#1"));
    }
}

public class Ds4UsbOutputTests
{
    [Fact]
    public void UsbReport_HasIdFlagsMotorsAndColor()
    {
        var r = Ds4Protocol.BuildUsbOutput(new Ds4Color(1, 2, 3), rumbleLeft: 200, rumbleRight: 50, includeRumble: true);
        Assert.Equal(32, r.Length);
        Assert.Equal(0x05, r[0]);
        Assert.Equal(0x03, r[1]);
        Assert.Equal(50, r[4]);
        Assert.Equal(200, r[5]);
        Assert.Equal(1, r[6]);
        Assert.Equal(2, r[7]);
        Assert.Equal(3, r[8]);
    }

    [Fact]
    public void UsbReport_LightOnly_DoesNotTouchMotors()
    {
        var r = Ds4Protocol.BuildUsbOutput(new Ds4Color(9, 9, 9));
        Assert.Equal(0x02, r[1]);
        Assert.Equal(0, r[4]);
        Assert.Equal(0, r[5]);
    }
}

public class Ds4CompatibilityTests
{
    /// <summary>Controle do usuário só aplica a cor com a flag de motor junto (HARDWARE_DIAGNOSTICS §3.10.6).</summary>
    [Fact]
    public void LightWithIdleMotors_SetsBothFlagsAndKeepsMotorsOff()
    {
        var usb = Ds4Protocol.BuildUsbOutput(new Ds4Color(10, 20, 30), 0, 0, includeRumble: true);
        Assert.Equal(0x03, usb[1]);
        Assert.Equal(0, usb[4]);
        Assert.Equal(0, usb[5]);

        var bt = Ds4Protocol.BuildBluetoothOutput(new Ds4Color(10, 20, 30), 0, 0, includeRumble: true);
        Assert.Equal(0x03, bt[3]);
        Assert.Equal(0, bt[6]);
        Assert.Equal(0, bt[7]);
    }
}

public class Ds4SensorTests
{
    /// <summary>Relatório USB real capturado do controle do usuário (repouso, cabo conectado).</summary>
    private const string RealUsbReport =
        "018080808008000000000000001400CAFFD7FF3C01783EBD08000000000018000000008000000080000000008000000080000000008000000080000000008000";

    [Fact]
    public void RealReport_DecodesMotionAndNoTouch()
    {
        var input = Ds4Protocol.ParseInput(Convert.FromHexString(RealUsbReport), bluetooth: false, 1)!;
        var m = input.Motion!.Value;

        Assert.Equal(20, m.GyroX);
        Assert.Equal(-54, m.GyroY);
        Assert.Equal(-41, m.GyroZ);
        Assert.Equal(316, m.AccelX);
        Assert.Equal(15992, m.AccelY);
        Assert.Equal(2237, m.AccelZ);
        Assert.False(input.Touch!.Value.First.Active);
        Assert.False(input.Touch.Value.Second.Active);
        Assert.Equal(80, input.Battery!.Value.Percent);
    }

    [Fact]
    public void TouchPoint_Decodes12BitCoordinates()
    {
        // ativo (bit7=0), id 5, X = 0x77F (1919), Y = 0x3AD (941)
        var p = Ds4Protocol.TouchPoint([0x05, 0x7F, 0xD7, 0x3A]);
        Assert.True(p.Active);
        Assert.Equal(5, p.Id);
        Assert.Equal(1919, p.X);
        Assert.Equal(941, p.Y);
    }

    [Fact]
    public void Motion_FlatControllerHasNearZeroTilt()
    {
        var flat = new Ds4Motion(0, 0, 0, 0, 8192, 0);
        Assert.Equal(0, flat.PitchDegrees, precision: 3);
        Assert.Equal(0, flat.RollDegrees, precision: 3);

        var tilted = new Ds4Motion(0, 0, 0, 8192, 8192, 0);
        Assert.Equal(45, tilted.RollDegrees, precision: 3);
    }

    [Fact]
    public void BluetoothBasic_HasNoSensors() =>
        Assert.Null(Ds4Protocol.ParseInput(Convert.FromHexString("01808080800800000000"), bluetooth: true, 1)!.Motion);
}

public class Ds4ServiceLifetimeTests
{
    [Fact]
    public void Dispose_TwiceDoesNotThrow()
    {
        var service = new Ds4Service(Microsoft.Extensions.Logging.Abstractions.NullLogger<Ds4Service>.Instance);
        service.Dispose();
        var second = Record.Exception(service.Dispose);
        Assert.Null(second);
    }
}
