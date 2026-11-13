using NovaControlCenter.App.Presentation;
using NovaControlCenter.App.ViewModels;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.Ds4;
using NovaControlCenter.Infrastructure.Virtual;
using NSubstitute;

namespace NovaControlCenter.Tests.Ds4;

public class VirtualXboxPadMappingTests
{
    [Fact]
    public void Map_CopiesButtonsTriggersAndSticks()
    {
        var state = new ControllerState(1, GamepadButtons.A | GamepadButtons.DPadUp | GamepadButtons.Guide, 10, 250,
            new StickState(-32768, 32767), new StickState(100, -100));

        var r = VirtualXboxPad.Map(state);

        Assert.Equal((ushort)(0x1000 | 0x0001 | 0x0400), r.Buttons);
        Assert.Equal(10, r.LeftTrigger);
        Assert.Equal(250, r.RightTrigger);
        Assert.Equal(-32768, r.LeftThumbX);
        Assert.Equal(32767, r.LeftThumbY);
        Assert.Equal(100, r.RightThumbX);
        Assert.Equal(-100, r.RightThumbY);
    }

    [Fact]
    public void SameInputs_IgnoresPacketNumber()
    {
        var a = ControllerState.Neutral with { PacketNumber = 1 };
        var b = ControllerState.Neutral with { PacketNumber = 2 };
        Assert.True(VirtualXboxPad.SameInputs(a, b));
        Assert.False(VirtualXboxPad.SameInputs(a, a with { Buttons = GamepadButtons.B }));
    }
}

public class RecognitionPresenterTests
{
    private static Ds4Snapshot Snap(Ds4RecognitionMode mode, Ds4LinkState link = Ds4LinkState.Connected,
        bool active = false, int? slot = null, string? error = null) =>
        new(link, false, null, null, false, 0, mode, slot, active, error);

    [Fact]
    public void PlayStation_NoVirtualPad() =>
        Assert.Contains("PlayStation", Ds4Presenter.Recognition(Snap(Ds4RecognitionMode.PlayStation)).Text, StringComparison.Ordinal);

    [Fact]
    public void Xbox_Active_ShowsOneBasedSlot()
    {
        var (kind, text) = Ds4Presenter.Recognition(Snap(Ds4RecognitionMode.Xbox, active: true, slot: 1));
        Assert.Equal(StatusKind.Ok, kind);
        Assert.Contains("2 de 4", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Xbox_Error_IsShown()
    {
        var (kind, text) = Ds4Presenter.Recognition(Snap(Ds4RecognitionMode.Xbox, error: "ViGEmBus não encontrado."));
        Assert.Equal(StatusKind.Error, kind);
        Assert.Equal("ViGEmBus não encontrado.", text);
    }

    [Fact]
    public void Xbox_WithoutLink_Waits() =>
        Assert.Equal(StatusKind.Warning, Ds4Presenter.Recognition(Snap(Ds4RecognitionMode.Xbox, Ds4LinkState.Off)).Kind);
}

public class RecognitionViewModelTests
{
    [Fact]
    public void SelectingXbox_IsForwardedToService()
    {
        var ds4 = Substitute.For<IDs4Controller>();
        ds4.Current.Returns(new Ds4Snapshot(Ds4LinkState.Off, false, null, null, false, 0));
        ds4.Mode.Returns(Ds4RecognitionMode.PlayStation);
        var vm = new Ds4ViewModel(ds4);

        vm.SelectedMode = vm.Modes[1];

        ds4.Received().Mode = Ds4RecognitionMode.Xbox;
    }
}
