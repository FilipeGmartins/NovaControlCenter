using NovaControlCenter.App.Presentation;
using NovaControlCenter.App.ViewModels;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.Audio;
using NovaControlCenter.Infrastructure.Ds4;
using NSubstitute;

namespace NovaControlCenter.Tests.Ds4;

/// <summary>Áudio simulado: permite testar a tela sem dispositivos reais.</summary>
internal sealed class FakeAudio : IControllerAudio
{
    private readonly ControllerAudioEndpoints _endpoints;

    private FakeAudio(ControllerAudioEndpoints endpoints) => _endpoints = endpoints;

    public static FakeAudio None => new(new ControllerAudioEndpoints(null, null));

    public static FakeAudio WithController() => new(new ControllerAudioEndpoints(
        new AudioEndpoint("out", "Alto-falantes (Wireless Controller)"),
        new AudioEndpoint("in", "Microfone (Wireless Controller)")));

    public int TonesPlayed { get; private set; }
    public FakeMeter? LastMeter { get; private set; }

    public ControllerAudioEndpoints Find(string? nameHint = null) => _endpoints;

    public Task PlayToneAsync(AudioEndpoint speaker, double frequency = 440, double seconds = 1, float volume = 0.3f)
    {
        TonesPlayed++;
        return Task.CompletedTask;
    }

    public IMicrophoneMeter StartMeter(AudioEndpoint microphone) => LastMeter = new FakeMeter();

    internal sealed class FakeMeter : IMicrophoneMeter
    {
        public double Level { get; set; } = 0.42;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}

public class ControllerAudioTests
{
    private static readonly AudioEndpoint Realtek = new("1", "Alto-falantes (Realtek(R) Audio)");
    private static readonly AudioEndpoint Ds4 = new("2", "Alto-falantes (Wireless Controller)");

    [Fact]
    public void Match_FindsControllerByNameHints() =>
        Assert.Equal(Ds4, ControllerAudio.Match([Realtek, Ds4], ControllerAudio.ControllerNameHints));

    [Fact]
    public void Match_IsCaseInsensitive_AndReturnsNullWhenAbsent()
    {
        Assert.Equal(Ds4, ControllerAudio.Match([new("x", "foo"), Ds4], ["wireless controller"]));
        Assert.Null(ControllerAudio.Match([Realtek], ControllerAudio.ControllerNameHints));
    }

    [Fact]
    public void Peak_Float32()
    {
        var samples = new[] { 0.1f, -0.75f, 0.5f };
        var bytes = samples.SelectMany(BitConverter.GetBytes).ToArray();
        Assert.Equal(0.75, ControllerAudio.Peak(bytes, 32), precision: 5);
    }

    [Fact]
    public void Peak_Pcm16_AndClamp()
    {
        var bytes = new short[] { 100, short.MinValue, 2000 }.SelectMany(BitConverter.GetBytes).ToArray();
        Assert.Equal(1.0, ControllerAudio.Peak(bytes, 16), precision: 5);
        Assert.Equal(0, ControllerAudio.Peak([], 16));
    }

    [Fact]
    public void Presenter_ExplainsMissingAudio_UsbVsBluetooth()
    {
        var none = new ControllerAudioEndpoints(null, null);
        Assert.Contains("aparece só como controle", Ds4Presenter.Audio(none, bluetooth: false).Text, StringComparison.Ordinal);
        Assert.Contains("Bluetooth", Ds4Presenter.Audio(none, bluetooth: true).Text, StringComparison.Ordinal);
        Assert.Equal(StatusKind.Ok, Ds4Presenter.Audio(new ControllerAudioEndpoints(Ds4, null), false).Kind);
    }
}

public class AudioViewModelTests
{
    private static IDs4Controller Connected()
    {
        var ds4 = Substitute.For<IDs4Controller>();
        ds4.Current.Returns(new Ds4Snapshot(Ds4LinkState.Connected, false,
            new Ds4Input(ControllerState.Neutral with { PacketNumber = 1 }, false, null, true), null, true, 0));
        return ds4;
    }

    [Fact]
    public async Task WithControllerAudio_ToneAndMicWork()
    {
        var audio = FakeAudio.WithController();
        var vm = new Ds4ViewModel(Connected(), null, audio);
        await vm.CheckSpeakerCommand.ExecuteAsync(null);

        Assert.True(vm.HasSpeaker);
        Assert.True(vm.HasMicrophone);

        await vm.PlayToneCommand.ExecuteAsync(null);
        Assert.Equal(1, audio.TonesPlayed);
        Assert.False(vm.TonePlaying);

        vm.ToggleMicrophoneCommand.Execute(null);
        Assert.True(vm.MicActive);
        vm.ToggleMicrophoneCommand.Execute(null);
        Assert.False(vm.MicActive);
        Assert.True(audio.LastMeter!.Disposed);
    }

    [Fact]
    public async Task WithoutControllerAudio_ButtonsStayDisabledAndNothingPlays()
    {
        var audio = FakeAudio.None;
        var vm = new Ds4ViewModel(Connected(), null, audio);
        await vm.CheckSpeakerCommand.ExecuteAsync(null);

        Assert.False(vm.HasSpeaker);
        Assert.False(vm.HasMicrophone);

        await vm.PlayToneCommand.ExecuteAsync(null);
        vm.ToggleMicrophoneCommand.Execute(null);
        Assert.Equal(0, audio.TonesPlayed);
        Assert.False(vm.MicActive);
    }
}
