using NovaControlCenter.App.Presentation;
using NovaControlCenter.App.ViewModels;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.Ds4;
using NSubstitute;

namespace NovaControlCenter.Tests.Ds4;

public class Ds4PresenterTests
{
    private static Ds4Snapshot Snap(Ds4LinkState link, Ds4Battery? battery = null, bool extended = false) =>
        new(link, true, null, battery, extended, 0);

    [Theory]
    [InlineData(Ds4LinkState.Connected, StatusKind.Ok)]
    [InlineData(Ds4LinkState.Off, StatusKind.Warning)]
    [InlineData(Ds4LinkState.NotFound, StatusKind.Error)]
    public void Link_MapsToStatusKind(Ds4LinkState link, StatusKind kind) =>
        Assert.Equal(kind, Ds4Presenter.Link(Snap(link)).Kind);

    [Fact]
    public void Battery_IsApproximate()
    {
        var (title, detail) = Ds4Presenter.Battery(Snap(Ds4LinkState.Connected, new Ds4Battery(4, false)), advanced: true);
        Assert.Equal("≈ 40%", title);
        Assert.Contains("10%", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Battery_WithoutAdvancedOverBluetooth_ExplainsHowToEnable()
    {
        var (title, detail) = Ds4Presenter.Battery(Snap(Ds4LinkState.Connected), advanced: false);
        Assert.Equal("Indisponível", title);
        Assert.Contains("Recursos avançados", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Battery_Full_And_Charging()
    {
        Assert.Equal("Carregada", Ds4Presenter.Battery(Snap(Ds4LinkState.Connected, new Ds4Battery(11, true)), true).Title);
        Assert.Contains("Carregando", Ds4Presenter.Battery(Snap(Ds4LinkState.Connected, new Ds4Battery(5, true)), true).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Battery_NotConnected_ShowsDash() =>
        Assert.Equal(Presenter.Dash, Ds4Presenter.Battery(Snap(Ds4LinkState.Off), true).Title);
}

public class Ds4ViewModelTests
{
    private readonly IDs4Controller _ds4 = Substitute.For<IDs4Controller>();

    public Ds4ViewModelTests() =>
        _ds4.Current.Returns(new Ds4Snapshot(Ds4LinkState.Connected, true,
            new Ds4Input(ControllerState.Neutral with { PacketNumber = 1 }, false, null, false), null, false, 0));

    [Fact]
    public void ToggleAdvanced_IsForwardedToController()
    {
        var vm = new Ds4ViewModel(_ds4);
        vm.Advanced = true;
        _ds4.Received().AdvancedEnabled = true;
    }

    [Fact]
    public void CanSend_RequiresAdvancedAndLink()
    {
        var vm = new Ds4ViewModel(_ds4);
        Assert.False(vm.CanSend);
        vm.Advanced = true;
        Assert.True(vm.CanSend);
    }

    [Fact]
    public void Preset_SetsColorAndSendsWithSelectedVariant()
    {
        var vm = new Ds4ViewModel(_ds4) { Advanced = true };
        vm.SelectedVariant = vm.Variants[2];

        vm.PresetCommand.Execute("FF8000");

        Assert.Equal(255, vm.LightR);
        Assert.Equal(128, vm.LightG);
        _ds4.Received().SetLight(new Ds4Color(255, 128, 0), Ds4OutputVariant.Ds4WindowsControl);
    }
}
