using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;
using NSubstitute;

namespace NovaControlCenter.Tests.Connection;

internal static class Ids
{
    public static readonly UsbDeviceId Connected = KnownDevices.NovaLiteConnected;
    public static readonly UsbDeviceId Idle = KnownDevices.NovaLiteReceiverIdle;
    public static readonly UsbDeviceId Virtual360 = KnownDevices.Xbox360Wired;

    public static IReadOnlySet<UsbDeviceId> Set(params UsbDeviceId[] ids) => ids.ToHashSet();
}

public class ConnectionResolverTests
{
    [Fact]
    public void NoDevices_IsReceiverNotFound() =>
        Assert.Equal(ConnectionState.ReceiverNotFound, ConnectionResolver.Resolve(Ids.Set(), []).State);

    [Fact]
    public void IdlePid_IsControllerOff()
    {
        var result = ConnectionResolver.Resolve(Ids.Set(Ids.Idle), []);
        Assert.Equal(ConnectionState.ControllerOff, result.State);
        Assert.Null(result.Controller);
    }

    [Fact]
    public void ConnectedPid_WithMatchingSlot_IsExactMatch()
    {
        var result = ConnectionResolver.Resolve(Ids.Set(Ids.Connected), [new XInputSlot(0, Ids.Virtual360), new XInputSlot(2, Ids.Connected)]);

        Assert.Equal(ConnectionState.Connected, result.State);
        Assert.Equal(2, result.Controller!.XInputSlot);
        Assert.Equal(SlotMatch.Exact, result.Controller.SlotMatch);
        Assert.Equal("Receptor USB 2.4 GHz", result.Controller.ConnectionType);
    }

    [Fact]
    public void ConnectedPid_WithoutVidPid_PicksSingleUnknownSlotApproximately()
    {
        var result = ConnectionResolver.Resolve(Ids.Set(Ids.Connected), [new XInputSlot(0, Ids.Virtual360), new XInputSlot(1, null)]);

        Assert.Equal(1, result.Controller!.XInputSlot);
        Assert.Equal(SlotMatch.Approximate, result.Controller.SlotMatch);
    }

    [Fact]
    public void ConnectedPid_NeverPicksVirtualPad()
    {
        var result = ConnectionResolver.Resolve(Ids.Set(Ids.Connected), [new XInputSlot(0, Ids.Virtual360)]);

        Assert.Equal(ConnectionState.Connected, result.State);
        Assert.Null(result.Controller!.XInputSlot);
        Assert.Equal(SlotMatch.None, result.Controller.SlotMatch);
    }

    [Fact]
    public void ConnectedPid_AmbiguousUnknownSlots_DoesNotGuess()
    {
        var result = ConnectionResolver.Resolve(Ids.Set(Ids.Connected), [new XInputSlot(0, null), new XInputSlot(1, null)]);
        Assert.Equal(SlotMatch.None, result.Controller!.SlotMatch);
    }

    [Fact]
    public void BothPidsPresent_ConnectedWins() =>
        Assert.Equal(ConnectionState.Connected, ConnectionResolver.Resolve(Ids.Set(Ids.Idle, Ids.Connected), []).State);
}

public class ConnectionDebouncerTests
{
    private static readonly ResolvedConnection Missing = new(ConnectionState.ReceiverNotFound, null);
    private static readonly ResolvedConnection Off = new(ConnectionState.ControllerOff, null);
    private static readonly ResolvedConnection On = ConnectionResolver.Resolve(Ids.Set(Ids.Connected), []);

    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void ReEnumerationGap_DoesNotFlickerToNotFound()
    {
        var debouncer = new ConnectionDebouncer(_time, TimeSpan.FromSeconds(2));
        debouncer.Update(On);

        // Sequência real observada: 1040 → (~1 s nada) → 2106
        Assert.Equal(ConnectionState.Connected, debouncer.Update(Missing).State);
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(ConnectionState.Connected, debouncer.Update(Missing).State);
        Assert.Equal(ConnectionState.ControllerOff, debouncer.Update(Off).State);
    }

    [Fact]
    public void PersistentAbsence_BecomesNotFoundAfterHold()
    {
        var debouncer = new ConnectionDebouncer(_time, TimeSpan.FromSeconds(2));
        debouncer.Update(Off);

        debouncer.Update(Missing);
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(ConnectionState.ReceiverNotFound, debouncer.Update(Missing).State);
    }

    [Fact]
    public void AbsenceTimer_ResetsWhenDeviceReturns()
    {
        var debouncer = new ConnectionDebouncer(_time, TimeSpan.FromSeconds(2));
        debouncer.Update(On);

        debouncer.Update(Missing);
        _time.Advance(TimeSpan.FromSeconds(1.5));
        debouncer.Update(On);
        debouncer.Update(Missing);
        _time.Advance(TimeSpan.FromSeconds(1.5));

        Assert.Equal(ConnectionState.Connected, debouncer.Update(Missing).State);
    }

    [Fact]
    public void Connecting_IsImmediate()
    {
        var debouncer = new ConnectionDebouncer(_time, TimeSpan.FromSeconds(2));
        Assert.Equal(ConnectionState.Connected, debouncer.Update(On).State);
    }
}

public class ConnectionMonitorTests
{
    private readonly IDevicePresence _presence = Substitute.For<IDevicePresence>();
    private readonly IInputReader _input = Substitute.For<IInputReader>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));

    private ConnectionMonitor CreateMonitor() =>
        new(_presence, _input, _time, NullLogger<ConnectionMonitor>.Instance);

    private void Present(params UsbDeviceId[] ids) =>
        _presence.GetPresentDevices(KnownDevices.GameSirVendorId).Returns(Ids.Set(ids));

    [Fact]
    public async Task Poll_RaisesEventOnlyWhenStateChanges()
    {
        await using var monitor = CreateMonitor();
        var events = new List<ConnectionSnapshot>();
        monitor.SnapshotChanged += (_, s) => events.Add(s);
        _input.GetConnectedSlots().Returns([new XInputSlot(0, Ids.Connected)]);
        Present(Ids.Connected);

        monitor.Poll();
        monitor.Poll();

        var snapshot = Assert.Single(events);
        Assert.Equal(ConnectionState.Connected, snapshot.State);
        Assert.Equal(_time.GetUtcNow(), snapshot.ConnectedSince);
    }

    [Fact]
    public async Task ConnectedSince_IsKeptWhileConnected_AndClearedOnDisconnect()
    {
        await using var monitor = CreateMonitor();
        _input.GetConnectedSlots().Returns([new XInputSlot(0, Ids.Connected)]);
        Present(Ids.Connected);
        var since = monitor.Poll().ConnectedSince;

        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(since, monitor.Poll().ConnectedSince);

        Present(Ids.Idle);
        _input.GetConnectedSlots().Returns([]);
        var off = monitor.Poll();
        Assert.Equal(ConnectionState.ControllerOff, off.State);
        Assert.Null(off.ConnectedSince);
    }

    [Fact]
    public async Task HardwareFailure_KeepsLastState()
    {
        await using var monitor = CreateMonitor();
        _input.GetConnectedSlots().Returns([]);
        Present(Ids.Idle);
        monitor.Poll();

        _presence.GetPresentDevices(Arg.Any<ushort>()).Returns(_ => throw new IOException("falha simulada"));

        Assert.Equal(ConnectionState.ControllerOff, monitor.Poll().State);
    }
}

public class ControllerStateTests
{
    [Theory]
    [InlineData((short)32767, 1.0)]
    [InlineData((short)-32768, -1.0)]
    [InlineData((short)0, 0.0)]
    public void Stick_NormalizesFullRange(short raw, double expected) =>
        Assert.Equal(expected, new StickState(raw, 0).X, precision: 6);

    [Fact]
    public void Stick_MagnitudeIsClampedToOne() =>
        Assert.Equal(1.0, new StickState(32767, 32767).Magnitude, precision: 6);

    [Fact]
    public void IsPressed_ChecksFlags()
    {
        var state = ControllerState.Neutral with { Buttons = GamepadButtons.A | GamepadButtons.LeftShoulder };
        Assert.True(state.IsPressed(GamepadButtons.A));
        Assert.False(state.IsPressed(GamepadButtons.B));
        Assert.False(state.IsPressed(GamepadButtons.None));
    }

    [Fact]
    public void Triggers_NormalizeToZeroOne()
    {
        var state = ControllerState.Neutral with { LeftTriggerRaw = 255, RightTriggerRaw = 0 };
        Assert.Equal(1.0, state.LeftTrigger);
        Assert.Equal(0.0, state.RightTrigger);
    }
}
