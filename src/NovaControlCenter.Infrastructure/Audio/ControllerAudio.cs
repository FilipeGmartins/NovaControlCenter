using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace NovaControlCenter.Infrastructure.Audio;

/// <summary>Um dispositivo de áudio do Windows (saída ou entrada).</summary>
public sealed record AudioEndpoint(string Id, string Name);

/// <summary>Saída (alto-falante) e entrada (microfone) do controle, se o Windows expuser.</summary>
public sealed record ControllerAudioEndpoints(AudioEndpoint? Speaker, AudioEndpoint? Microphone)
{
    public bool Any => Speaker is not null || Microphone is not null;
}

/// <summary>Contrato usado pela interface (permite testar sem dispositivos de áudio).</summary>
public interface IControllerAudio
{
    /// <summary>Procura o áudio do controle (ou de qualquer dispositivo cujo nome contenha <paramref name="nameHint"/>).</summary>
    ControllerAudioEndpoints Find(string? nameHint = null);

    /// <summary>Toca um tom senoidal curto e espera terminar.</summary>
    Task PlayToneAsync(AudioEndpoint speaker, double frequency = 440, double seconds = 1.0, float volume = 0.3f);

    /// <summary>Começa a medir o nível do microfone. Descarte o retorno para parar.</summary>
    IMicrophoneMeter StartMeter(AudioEndpoint microphone);
}

public interface IMicrophoneMeter : IDisposable
{
    /// <summary>Pico recente, de 0 a 1.</summary>
    double Level { get; }
}

/// <summary>
/// Áudio do DS4 via WASAPI (NAudio). No DS4 original, pelo cabo USB, o Windows cria "Alto-falantes (Wireless Controller)"
/// e "Microfone (Wireless Controller)". O controle do usuário não expõe áudio (HARDWARE_DIAGNOSTICS §3.10.7).
/// </summary>
public sealed class ControllerAudio : IControllerAudio
{
    internal static readonly string[] ControllerNameHints = ["Wireless Controller", "DUALSHOCK", "DualShock"];

    public ControllerAudioEndpoints Find(string? nameHint = null)
    {
        using var enumerator = new MMDeviceEnumerator();
        var hints = nameHint is null ? ControllerNameHints : [nameHint];
        return new ControllerAudioEndpoints(
            Match(List(enumerator, DataFlow.Render), hints),
            Match(List(enumerator, DataFlow.Capture), hints));
    }

    public async Task PlayToneAsync(AudioEndpoint speaker, double frequency = 440, double seconds = 1.0, float volume = 0.3f)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDevice(speaker.Id);
        var mix = device.AudioClient.MixFormat;
        var tone = new SignalGenerator(mix.SampleRate, mix.Channels)
        {
            Type = SignalGeneratorType.Sin,
            Frequency = frequency,
            Gain = Math.Clamp(volume, 0f, 1f),
        }.Take(TimeSpan.FromSeconds(seconds));

        using var output = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, latency: 100);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, e) =>
        {
            if (e.Exception is null) finished.TrySetResult();
            else finished.TrySetException(e.Exception);
        };
        output.Init(tone);
        output.Play();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(seconds + 5)).ConfigureAwait(false);
    }

    public IMicrophoneMeter StartMeter(AudioEndpoint microphone)
    {
        ArgumentNullException.ThrowIfNull(microphone);
        return new WasapiMeter(microphone.Id);
    }

    /// <summary>Primeiro dispositivo cujo nome contém alguma das dicas.</summary>
    internal static AudioEndpoint? Match(IEnumerable<AudioEndpoint> endpoints, IReadOnlyCollection<string> hints) =>
        endpoints.FirstOrDefault(e => hints.Any(h => e.Name.Contains(h, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Pico absoluto (0–1) de um buffer de áudio em float 32 ou PCM 16 bits.</summary>
    public static double Peak(ReadOnlySpan<byte> buffer, int bitsPerSample)
    {
        var peak = 0.0;
        if (bitsPerSample == 32)
        {
            for (var i = 0; i + 4 <= buffer.Length; i += 4)
                peak = Math.Max(peak, Math.Abs(BitConverter.ToSingle(buffer.Slice(i, 4))));
        }
        else if (bitsPerSample == 16)
        {
            for (var i = 0; i + 2 <= buffer.Length; i += 2)
                peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(buffer.Slice(i, 2)) / 32768.0));
        }
        return Math.Min(1.0, peak);
    }

    private static List<AudioEndpoint> List(MMDeviceEnumerator enumerator, DataFlow flow)
    {
        var result = new List<AudioEndpoint>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device) result.Add(new AudioEndpoint(device.ID, device.FriendlyName));
        }
        return result;
    }

    /// <summary>Captura WASAPI que guarda o pico recente (decai suavemente para a barra não "piscar").</summary>
    private sealed class WasapiMeter : IMicrophoneMeter
    {
        private readonly MMDeviceEnumerator _enumerator = new();
        private readonly MMDevice _device;
        private readonly WasapiCapture _capture;
        private double _level;

        public WasapiMeter(string deviceId)
        {
            _device = _enumerator.GetDevice(deviceId);
            _capture = new WasapiCapture(_device);
            _capture.DataAvailable += (_, e) =>
            {
                var peak = Peak(e.Buffer.AsSpan(0, e.BytesRecorded), _capture.WaveFormat.BitsPerSample);
                Volatile.Write(ref _level, Math.Max(peak, Volatile.Read(ref _level) * 0.85));
            };
            _capture.StartRecording();
        }

        public double Level => Volatile.Read(ref _level);

        public void Dispose()
        {
            _capture.StopRecording();
            _capture.Dispose();
            _device.Dispose();
            _enumerator.Dispose();
        }
    }
}
