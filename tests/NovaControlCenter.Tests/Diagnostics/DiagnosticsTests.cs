using NovaControlCenter.Core.Devices;
using NovaControlCenter.Infrastructure.Hid;
using NovaControlCenter.Infrastructure.XInput;

namespace NovaControlCenter.Tests.Diagnostics;

public class UsbDeviceIdTests
{
    [Fact]
    public void ToString_UsesUppercaseHexWithColon() =>
        Assert.Equal("3537:1040", new UsbDeviceId(0x3537, 0x1040).ToString());

    [Fact]
    public void KnownDevices_RecognizesGameSirVendor()
    {
        Assert.True(KnownDevices.IsGameSir(new UsbDeviceId(0x3537, 0xFFFF)));
        Assert.False(KnownDevices.IsGameSir(KnownDevices.Xbox360Wired));
    }

    [Fact]
    public void KnownDevices_ReceiverWithoutControllerIsConfirmed() =>
        Assert.Equal(Confidence.Confirmed, KnownDevices.Find(new UsbDeviceId(0x3537, 0x2106))?.Confidence);

    [Fact]
    public void KnownDevices_ControllerModelStillAwaitsUserConfirmation() =>
        Assert.Equal(Confidence.Hypothesis, KnownDevices.Find(new UsbDeviceId(0x3537, 0x1040))?.Confidence);

    [Fact]
    public void KnownDevices_FindReturnsNullForUnknown() =>
        Assert.Null(KnownDevices.Find(new UsbDeviceId(0x1234, 0x5678)));
}

public class HidPathParserTests
{
    private const string VendorPath = @"\\?\hid#vid_3537&pid_1040&mi_02#7&e8d8f38&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string CollectionPath = @"\\?\hid#vid_3537&pid_1040&mi_01&col02#7&1df45251&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string XInputPath = @"\\?\hid#vid_3537&pid_1040&ig_00#8&d0f6ded&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string SimplePath = @"\\?\hid#vid_3537&pid_2106#6&231ee1cc&1&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    [Fact]
    public void InterfaceNumber_ParsesMi() => Assert.Equal(2, HidPathParser.InterfaceNumber(VendorPath));

    [Fact]
    public void InterfaceNumber_NullForNonComposite() => Assert.Null(HidPathParser.InterfaceNumber(SimplePath));

    [Fact]
    public void CollectionNumber_ParsesCol() => Assert.Equal(2, HidPathParser.CollectionNumber(CollectionPath));

    [Fact]
    public void IsXInputGameInterface_DetectsIg()
    {
        Assert.True(HidPathParser.IsXInputGameInterface(XInputPath));
        Assert.False(HidPathParser.IsXInputGameInterface(VendorPath));
    }
}

public class ReportByteStatisticsTests
{
    [Fact]
    public void Summarize_TracksMinMaxDistinctFirstLast()
    {
        var stats = new ReportByteStatistics();
        stats.Add([0x01, 0x64]);
        stats.Add([0x01, 0x10]);
        stats.Add([0x01, 0x80]);

        var summary = stats.Summarize();

        Assert.Equal(3, stats.ReportCount);
        Assert.Equal(new ByteOffsetSummary(0, 0x01, 0x01, 1, 0x01, 0x01), summary[0]);
        Assert.Equal(new ByteOffsetSummary(1, 0x10, 0x80, 3, 0x64, 0x80), summary[1]);
    }

    [Fact]
    public void Summarize_HandlesReportsOfDifferentLengths()
    {
        var stats = new ReportByteStatistics();
        stats.Add([0xAA]);
        stats.Add([0xAA, 0xBB]);

        var summary = stats.Summarize();

        Assert.Equal(2, summary.Count);
        Assert.Equal(1, summary[1].DistinctValues);
    }
}

public class XInputTextTests
{
    [Theory]
    [InlineData(0x00, 0x03, "n/a")]  // desconectado: nível não significa nada
    [InlineData(0x01, 0x03, "n/a")]  // com fio: nunca reportar FULL fictício
    [InlineData(0x03, 0x01, "LOW")]
    [InlineData(0x02, 0x03, "FULL")]
    public void BatteryLevel_IgnoresLevelWhenWiredOrDisconnected(byte type, byte level, string expected) =>
        Assert.Equal(expected, XInputText.BatteryLevel(type, level));

    [Fact]
    public void BatteryType_WiredExplainsNoBatteryInfo() =>
        Assert.Contains("sem informação", XInputText.BatteryType(0x01), StringComparison.Ordinal);
}
