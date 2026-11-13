using NovaControlCenter.App.Presentation;
using NovaControlCenter.App.ViewModels;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.Ds4;
using NSubstitute;

namespace NovaControlCenter.Tests.Ds4;

public class Ds4SensorViewModelTests
{
    private static IDs4Controller Controller(Ds4Input input, bool bluetooth = false)
    {
        var ds4 = Substitute.For<IDs4Controller>();
        ds4.Current.Returns(new Ds4Snapshot(Ds4LinkState.Connected, bluetooth, input, null, true, 0));
        return ds4;
    }

    private static Ds4Input Input(Ds4Touch? touch = null, Ds4Motion? motion = null) =>
        new(ControllerState.Neutral with { PacketNumber = 7 }, false, null, true, touch, motion);

    [Fact]
    public void Touch_FingerIsPlacedProportionally()
    {
        var touch = new Ds4Touch(new Ds4TouchPoint(true, 1, 960, 471), new Ds4TouchPoint(false, 0, 0, 0));
        var vm = new Ds4ViewModel(Controller(Input(touch, new Ds4Motion())), audio: FakeAudio.None);

        Assert.True(vm.Finger1Active);
        Assert.False(vm.Finger2Active);
        Assert.Equal((Ds4ViewModel.PadWidth / 2) - 11, vm.Finger1Left, precision: 6);
        Assert.Equal((Ds4ViewModel.PadHeight / 2) - 11, vm.Finger1Top, precision: 6);
        Assert.True(vm.SensorsAvailable);
    }

    [Fact]
    public void Motion_ZeroTiltMakesCurrentPositionLevel()
    {
        var vm = new Ds4ViewModel(Controller(Input(motion: new Ds4Motion(0, 0, 0, 4000, 8000, 0))), audio: FakeAudio.None);
        Assert.NotEqual(0, vm.Roll, precision: 3);

        vm.ZeroTiltCommand.Execute(null);

        Assert.Equal(0, vm.Roll, precision: 6);
        Assert.Equal(0, vm.Pitch, precision: 6);
    }

    [Fact]
    public void GyroLevel_IsClampedToOne()
    {
        Assert.Equal(1.0, Ds4ViewModel.Level(short.MinValue));
        Assert.Equal(0.5, Ds4ViewModel.Level(4000), precision: 6);
    }

    [Fact]
    public async Task Speaker_NotExposed_ExplainsWhy()
    {
        var vm = new Ds4ViewModel(Controller(Input()), audio: FakeAudio.None);
        await vm.CheckSpeakerCommand.ExecuteAsync(null);

        Assert.Equal(StatusKind.Warning, vm.SpeakerKind);
        Assert.Contains("não expõe alto-falante", vm.SpeakerText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Speaker_Found_IsReported()
    {
        var vm = new Ds4ViewModel(Controller(Input()), audio: FakeAudio.WithController());
        await vm.CheckSpeakerCommand.ExecuteAsync(null);

        Assert.Equal(StatusKind.Ok, vm.SpeakerKind);
        Assert.Contains("Wireless Controller", vm.SpeakerText, StringComparison.Ordinal);
    }

    [Fact]
    public void BasicBluetoothReport_HasNoSensors()
    {
        var vm = new Ds4ViewModel(Controller(Input(), bluetooth: true), audio: FakeAudio.None);
        Assert.False(vm.SensorsAvailable);
    }
}

public class GameViewTests
{
    [Fact]
    public void XboxMode_ShowsWhatXInputReports()
    {
        var ds4 = Substitute.For<IDs4Controller>();
        ds4.Current.Returns(new Ds4Snapshot(Ds4LinkState.Connected, false,
            new Ds4Input(ControllerState.Neutral with { PacketNumber = 1 }, false, null, true), null, true, 0,
            Ds4RecognitionMode.Xbox, VirtualSlot: 2, VirtualActive: true));
        var xinput = Substitute.For<NovaControlCenter.Core.Interfaces.IInputReader>();
        xinput.Read(2).Returns(ControllerState.Neutral with { Buttons = GamepadButtons.A | GamepadButtons.Y });

        var vm = new Ds4ViewModel(ds4, xinput, FakeAudio.None);

        Assert.True(vm.GameViewActive);
        Assert.Contains("A", vm.GameViewButtons, StringComparison.Ordinal);
        Assert.Contains("Y", vm.GameViewButtons, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayStationMode_HidesGameView()
    {
        var ds4 = Substitute.For<IDs4Controller>();
        ds4.Current.Returns(new Ds4Snapshot(Ds4LinkState.Connected, false, null, null, false, 0));
        var vm = new Ds4ViewModel(ds4, Substitute.For<NovaControlCenter.Core.Interfaces.IInputReader>(), FakeAudio.None);
        Assert.False(vm.GameViewActive);
    }
}
