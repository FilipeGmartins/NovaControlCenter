using NovaControlCenter.App.Presentation;
using NovaControlCenter.App.ViewModels;
using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;

namespace NovaControlCenter.Tests.Presentation;

public class PresenterTests
{
    private static ControllerInfo Controller(int? slot = 0, SlotMatch match = SlotMatch.Exact) =>
        new(KnownDevices.NovaLiteConnected, "GameSir T4 Nova Lite", "Receptor USB 2.4 GHz", slot, match, Confidence.Hypothesis);

    [Theory]
    [InlineData(ConnectionState.Connected, StatusKind.Ok, "Controle conectado")]
    [InlineData(ConnectionState.ControllerOff, StatusKind.Warning, "Controle desligado")]
    [InlineData(ConnectionState.ReceiverNotFound, StatusKind.Error, "Receptor não encontrado")]
    public void Connection_MapsEachStateToClearMessage(ConnectionState state, StatusKind kind, string title)
    {
        var snapshot = new ConnectionSnapshot(state, state == ConnectionState.Connected ? Controller() : null, null);
        var status = Presenter.Connection(snapshot);
        Assert.Equal(kind, status.Kind);
        Assert.Equal(title, status.Title);
        Assert.False(string.IsNullOrWhiteSpace(status.Description));
    }

    [Fact]
    public void Slot_IsOneBasedAndExplainsMatch()
    {
        Assert.Equal("1 de 4 (identificação exata)", Presenter.Slot(Controller(0)));
        Assert.Equal("3 de 4 (identificação aproximada)", Presenter.Slot(Controller(2, SlotMatch.Approximate)));
        Assert.Equal("Não identificado", Presenter.Slot(Controller(null, SlotMatch.None)));
    }

    [Fact]
    public void Model_HypothesisIsDisclosed() =>
        Assert.Contains("presumido", Presenter.Model(Controller()), StringComparison.Ordinal);

    [Fact]
    public void Battery_UnavailableShowsReason_NoNumber()
    {
        var (title, detail) = Presenter.Battery(new BatteryStatus.Unavailable(BatteryService.NoSourceReason));
        Assert.Equal("Não informada", title);
        Assert.Equal(BatteryService.NoSourceReason, detail);
        Assert.DoesNotContain("%", title, StringComparison.Ordinal);
    }

    [Fact]
    public void Battery_LevelIsNotShownAsPercent()
    {
        var (title, detail) = Presenter.Battery(new BatteryStatus.Level(BatteryLevel.Low, "XInput"));
        Assert.Equal("Baixa", title);
        Assert.Contains("sem porcentagem", detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5, "5 s")]
    [InlineData(65, "1 min 05 s")]
    [InlineData(3725, "1 h 02 min")]
    [InlineData(-10, "0 s")]
    public void Duration_IsHumanReadable(int seconds, string expected) =>
        Assert.Equal(expected, Presenter.Duration(TimeSpan.FromSeconds(seconds)));
}

public class ButtonStatesTests
{
    [Fact]
    public void Indexer_ReflectsPressedButtons()
    {
        var states = new ButtonStates();
        states.Set(GamepadButtons.A | GamepadButtons.DPadUp);
        Assert.True(states["A"]);
        Assert.True(states["DPadUp"]);
        Assert.False(states["B"]);
        Assert.False(states["NaoExiste"]);
        Assert.Equal(2, states.Count);
    }

    [Fact]
    public void Set_NotifiesOnlyOnChange()
    {
        var states = new ButtonStates();
        var notifications = 0;
        states.PropertyChanged += (_, e) => { if (e.PropertyName == "Item[]") notifications++; };

        states.Set(GamepadButtons.A);
        states.Set(GamepadButtons.A);
        states.Add(GamepadButtons.A);

        Assert.Equal(1, notifications);
    }
}
