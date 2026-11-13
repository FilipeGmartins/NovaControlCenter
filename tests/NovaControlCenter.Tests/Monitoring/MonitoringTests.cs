using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;
using NovaControlCenter.Infrastructure.XInput;
using NSubstitute;
using NSubstitute.Extensions;

namespace NovaControlCenter.Tests.Monitoring;

public class BatteryStatusTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Percent_RejectsOutOfRange(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatteryStatus.Percent(value, "teste"));

    [Fact]
    public void Percent_AcceptsBounds()
    {
        Assert.Equal(0, new BatteryStatus.Percent(0, "t").Value);
        Assert.Equal(100, new BatteryStatus.Percent(100, "t").Value);
    }
}

public class BatteryServiceTests
{
    private static readonly ControllerInfo Controller = new(
        KnownDevices.NovaLiteConnected, "GameSir T4 Nova Lite", "Receptor USB 2.4 GHz", 0, SlotMatch.Exact, Confidence.Hypothesis);

    private static readonly ConnectionSnapshot Connected = new(ConnectionState.Connected, Controller, DateTimeOffset.UnixEpoch);

    [Fact]
    public void NotConnected_IsUnavailableWithReason()
    {
        var status = new BatteryService([]).GetStatus(ConnectionSnapshot.NotFound);
        Assert.Equal(BatteryService.NotConnectedReason, Assert.IsType<BatteryStatus.Unavailable>(status).Reason);
    }

    [Fact]
    public void NoProviderHasData_IsUnavailable_NeverEstimated()
    {
        var silent = Substitute.For<IBatteryProvider>();
        silent.TryGetStatus(Controller).Returns((BatteryStatus?)null);

        var status = new BatteryService([silent]).GetStatus(Connected);

        Assert.Equal(BatteryService.NoSourceReason, Assert.IsType<BatteryStatus.Unavailable>(status).Reason);
    }

    [Fact]
    public void FirstProviderWithData_Wins()
    {
        var silent = Substitute.For<IBatteryProvider>();
        var real = Substitute.For<IBatteryProvider>();
        var later = Substitute.For<IBatteryProvider>();
        real.TryGetStatus(Controller).Returns(new BatteryStatus.Level(BatteryLevel.Low, "real"));

        var status = new BatteryService([silent, real, later]).GetStatus(Connected);

        Assert.Equal(new BatteryStatus.Level(BatteryLevel.Low, "real"), status);
        later.DidNotReceiveWithAnyArgs().TryGetStatus(default!);
    }
}

public class XInputMappingTests
{
    [Theory]
    [InlineData(0x00)] // DISCONNECTED
    [InlineData(0x01)] // WIRED — o caso real do T4 Nova Lite
    [InlineData(0x07)] // desconhecido
    public void BatteryMap_TypesWithoutBatteryInfo_ReturnNull(byte type) =>
        Assert.Null(XInputBatteryProvider.Map(type, 0x03, "XInput"));

    [Theory]
    [InlineData(0x02, 0x00, BatteryLevel.Empty)]
    [InlineData(0x03, 0x01, BatteryLevel.Low)]
    [InlineData(0xFF, 0x02, BatteryLevel.Medium)]
    [InlineData(0x02, 0x03, BatteryLevel.Full)]
    public void BatteryMap_BatteryTypes_ReturnLevel(byte type, byte level, BatteryLevel expected) =>
        Assert.Equal(new BatteryStatus.Level(expected, "XInput"), XInputBatteryProvider.Map(type, level, "XInput"));

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(1.0, 65535)]
    [InlineData(0.5, 32768)]
    [InlineData(-3.0, 0)]
    [InlineData(7.0, 65535)]
    [InlineData(double.NaN, 0)]
    public void Rumble_ToSpeed_ClampsAndScales(double intensity, int expected) =>
        Assert.Equal(expected, XInputRumble.ToSpeed(intensity));
}

public class InputMonitorTests
{
    private readonly IInputReader _reader = Substitute.For<IInputReader>();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UnixEpoch);
    private int? _slot = 0;

    private InputMonitor Create() => new(_reader, () => _slot, _time, NullLogger<InputMonitor>.Instance);

    private static ControllerState Packet(uint n) => ControllerState.Neutral with { PacketNumber = n };

    [Fact]
    public async Task PublishesOnlyWhenPacketChanges()
    {
        await using var monitor = Create();
        var events = new List<InputSample?>();
        monitor.InputChanged += (_, s) => events.Add(s);

        _reader.Read(0).Returns(Packet(1), Packet(1), Packet(2));
        monitor.Poll();
        monitor.Poll();
        monitor.Poll();

        Assert.Equal([1u, 2u], events.Select(e => e!.State.PacketNumber));
    }

    [Fact]
    public async Task PublishesNullOnceWhenReadingStops()
    {
        await using var monitor = Create();
        var events = new List<InputSample?>();
        monitor.InputChanged += (_, s) => events.Add(s);

        _reader.Read(0).Returns(Packet(1));
        monitor.Poll();
        _slot = null;
        monitor.Poll();
        monitor.Poll();

        Assert.Equal(2, events.Count);
        Assert.Null(events[1]);
        Assert.Null(monitor.Latest);
    }

    [Fact]
    public async Task UpdatesPerSecond_CountsOnlyLastSecond()
    {
        await using var monitor = Create();
        uint n = 0;
        _reader.Read(0).Returns(_ => Packet(++n));

        for (var i = 0; i < 10; i++)
        {
            monitor.Poll();
            _time.Advance(TimeSpan.FromMilliseconds(50));
        }
        Assert.Equal(10, monitor.UpdatesPerSecond);

        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, monitor.UpdatesPerSecond);
    }

    [Fact]
    public async Task ReaderFailure_PublishesNullAndKeepsRunning()
    {
        await using var monitor = Create();
        _reader.Read(0).Returns(Packet(1));
        monitor.Poll();

        _reader.Read(0).Returns(_ => throw new InvalidOperationException("falha simulada"));
        monitor.Poll();
        Assert.Null(monitor.Latest);

        _reader.Configure().Read(0).Returns(Packet(5));
        monitor.Poll();
        Assert.Equal(5u, monitor.Latest!.State.PacketNumber);
    }
}
