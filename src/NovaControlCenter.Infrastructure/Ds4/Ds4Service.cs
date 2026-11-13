using Microsoft.Extensions.Logging;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;
using NovaControlCenter.Infrastructure.Virtual;

namespace NovaControlCenter.Infrastructure.Ds4;

public enum Ds4LinkState
{
    /// <summary>Nenhum DS4 pareado/presente.</summary>
    NotFound,

    /// <summary>DS4 pareado, mas desligado ou fora de alcance (nó HID presente, link Bluetooth caído).</summary>
    Off,

    /// <summary>DS4 ligado e enviando dados.</summary>
    Connected,
}

/// <summary>Formatos de envio em teste (HARDWARE_DIAGNOSTICS §3.10.5). Mantidos selecionáveis para diagnóstico manual.</summary>
public enum Ds4OutputVariant
{
    LinuxControl,
    LinuxInterrupt,
    Ds4WindowsControl,
    Ds4WindowsInterrupt,
}

/// <summary>Como os jogos enxergam o DS4.</summary>
public enum Ds4RecognitionMode
{
    /// <summary>Controle PlayStation nativo (o app não cria nada).</summary>
    PlayStation,

    /// <summary>Xbox 360 virtual (ViGEm) alimentado pelo app. Só funciona com o app aberto.</summary>
    Xbox,
}

public sealed record Ds4Snapshot(Ds4LinkState Link, bool IsBluetooth, Ds4Input? Input, Ds4Battery? Battery, bool ExtendedActive, int ReportsPerSecond,
    Ds4RecognitionMode Mode = Ds4RecognitionMode.PlayStation, int? VirtualSlot = null, bool VirtualActive = false, string? VirtualError = null);

/// <summary>Contrato usado pela interface (permite testar o ViewModel sem hardware).</summary>
public interface IDs4Controller
{
    Ds4Snapshot Current { get; }

    /// <summary>"Recursos avançados": ativa o modo estendido (bateria/luz/vibração). Decisão 1 do PRODUCT.</summary>
    bool AdvancedEnabled { get; set; }

    bool SetLight(Ds4Color color, Ds4OutputVariant variant);

    bool SetRumble(byte left, byte right, Ds4OutputVariant variant);

    /// <summary>Modo de reconhecimento: PlayStation (nativo) ou Xbox (XInput emulado via ViGEm).</summary>
    Ds4RecognitionMode Mode { get; set; }

    /// <summary>
    /// Cor e formato salvos. A cor é reaplicada sozinha a cada conexão, mas só com "Recursos avançados" ligado
    /// (nada é enviado ao controle sem essa permissão).
    /// </summary>
    void RestorePreferences(Ds4Color? light, Ds4OutputVariant variant);
}

/// <summary>
/// Mantém a conexão com o primeiro DS4 encontrado em uma thread própria: detecta o link Bluetooth real,
/// lê relatórios continuamente e reconecta sozinho. Nada é enviado ao controle sem <see cref="AdvancedEnabled"/>.
/// </summary>
public sealed partial class Ds4Service : IDs4Controller, IDisposable
{
    private static readonly TimeSpan LinkCheckInterval = TimeSpan.FromSeconds(2);

    private readonly ILogger<Ds4Service> _logger;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Queue<DateTime> _reportTimes = new();
    private Task? _loop;
    private Ds4Connection? _connection;
    private Ds4Snapshot _current = new(Ds4LinkState.NotFound, false, null, null, false, 0);
    private bool _advanced;
    private bool _extendedForThisLink;
    private Ds4Color? _lastLight;
    private byte _rumbleLeft;
    private byte _rumbleRight;
    private Ds4OutputVariant _lastVariant = Ds4OutputVariant.LinuxControl;
    private Ds4RecognitionMode _mode = Ds4RecognitionMode.PlayStation;
    private VirtualXboxPad? _virtualPad;
    private string? _virtualError;
    private DateTime _nextVirtualAttempt = DateTime.MinValue;
    private bool _lightRestorePending;

    public Ds4Service(ILogger<Ds4Service> logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Ds4Snapshot Current
    {
        get { lock (_gate) return _current; }
    }

    public bool AdvancedEnabled
    {
        get { lock (_gate) return _advanced; }
        set
        {
            lock (_gate) _advanced = value;
            LogAdvanced(value);
        }
    }

    public Ds4RecognitionMode Mode
    {
        get { lock (_gate) return _mode; }
        set
        {
            lock (_gate)
            {
                _mode = value;
                _virtualError = null;
                _nextVirtualAttempt = DateTime.MinValue;
            }
            LogMode(value);
        }
    }

    public void RestorePreferences(Ds4Color? light, Ds4OutputVariant variant)
    {
        lock (_gate)
        {
            _lastLight = light;
            _lastVariant = variant;
            _lightRestorePending = light is not null;
        }
    }

    /// <summary>
    /// Perfil ativo (Sprint 05). Aplicado às entradas enviadas ao Xbox virtual e à vibração pedida pelos jogos.
    /// Null = sem processamento.
    /// </summary>
    public Func<InputProfile>? ProfileProvider { get; set; }

    public void Start() => _loop ??= Task.Factory.StartNew(() => Run(_cts.Token), TaskCreationOptions.LongRunning);

    public bool SetLight(Ds4Color color, Ds4OutputVariant variant)
    {
        var conn = ActiveConnectionForOutput();
        if (conn is null) return false;
        byte left, right;
        lock (_gate) (left, right) = (_rumbleLeft, _rumbleRight);
        // Sempre luz + estado atual dos motores: há controles (ex.: o do usuário, §3.10.6) que só aplicam
        // a cor quando a flag de motor vem junto. Num DS4 original isso é inofensivo (repete o estado atual).
        var ok = Send(conn, color, left, right, includeRumble: true, variant);
        lock (_gate)
        {
            _lastVariant = variant;
            if (ok) _lastLight = color;
        }
        LogOutput("luz", variant, ok);
        return ok;
    }

    public bool SetRumble(byte left, byte right, Ds4OutputVariant variant)
    {
        var conn = ActiveConnectionForOutput();
        if (conn is null) return false;
        Ds4Color? light;
        lock (_gate) light = _lastLight;
        var ok = Send(conn, light, left, right, includeRumble: true, variant);
        lock (_gate)
        {
            _lastVariant = variant;
            if (ok) (_rumbleLeft, _rumbleRight) = (left, right);
        }
        LogOutput("vibração", variant, ok);
        return ok;
    }

    private int _disposed;

    /// <summary>
    /// Idempotente: o container registra a mesma instância como <see cref="Ds4Service"/> e <see cref="IDs4Controller"/>
    /// e descarta as duas (antes isso lançava ObjectDisposedException ao fechar o app).
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(3)); }
        catch (AggregateException) { /* encerramento */ }
        lock (_gate)
        {
            _connection?.Dispose();
            _virtualPad?.Dispose();
            _virtualPad = null;
        }
        _cts.Dispose();
    }

    internal static bool Send(Ds4Connection conn, Ds4Color? light, byte left, byte right, bool includeRumble, Ds4OutputVariant variant)
    {
        var (path, hw, flags) = variant switch
        {
            Ds4OutputVariant.LinuxInterrupt => (Ds4Connection.OutputPath.Interrupt, (byte)0xC0, (byte?)null),
            Ds4OutputVariant.Ds4WindowsControl => (Ds4Connection.OutputPath.Control, (byte)0x84, (byte?)0xF7),
            Ds4OutputVariant.Ds4WindowsInterrupt => (Ds4Connection.OutputPath.Interrupt, (byte)0x84, (byte?)0xF7),
            _ => (Ds4Connection.OutputPath.Control, (byte)0xC0, (byte?)null),
        };
        return conn.SendOutput(light, left, right, includeRumble, path, hw, validFlags: flags);
    }

    private Ds4Connection? ActiveConnectionForOutput()
    {
        lock (_gate)
            return _advanced && _current.Link == Ds4LinkState.Connected ? _connection : null;
    }

    private void Run(CancellationToken ct)
    {
        var nextLinkCheck = DateTime.MinValue;
        var link = Ds4LinkState.NotFound;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var conn = EnsureConnection();
                if (conn is null)
                {
                    ReleaseVirtualPad();
                    Publish(Ds4LinkState.NotFound, null, null, false);
                    ct.WaitHandle.WaitOne(1000);
                    continue;
                }

                if (DateTime.UtcNow >= nextLinkCheck && conn.IsBluetooth && Ds4Connection.AnyUsbPresent())
                {
                    // Cabo conectado: troca para USB (saída sem CRC e sem a pilha Bluetooth).
                    LogSwitchToUsb();
                    DropConnection();
                    link = Ds4LinkState.NotFound;
                    continue;
                }

                if (DateTime.UtcNow >= nextLinkCheck)
                {
                    var connected = conn.IsLinkConnectedAsync().GetAwaiter().GetResult();
                    var newLink = connected ? Ds4LinkState.Connected : Ds4LinkState.Off;
                    if (newLink != link)
                    {
                        LogLink(newLink);
                        // Ao religar, o DS4 volta ao modo básico (e à cor padrão): reativar modo estendido e cor salva.
                        lock (_gate)
                        {
                            _extendedForThisLink = false;
                            _lightRestorePending = _lastLight is not null;
                        }
                    }
                    link = newLink;
                    nextLinkCheck = DateTime.UtcNow + LinkCheckInterval;
                }

                if (link != Ds4LinkState.Connected)
                {
                    ReleaseVirtualPad();
                    Publish(link, null, null, false);
                    ct.WaitHandle.WaitOne(500);
                    continue;
                }

                bool wantExtended, haveExtended;
                lock (_gate) { wantExtended = _advanced; haveExtended = _extendedForThisLink; }
                if (wantExtended && !haveExtended)
                {
                    var ok = conn.EnableExtendedMode();
                    lock (_gate) _extendedForThisLink = ok;
                    LogExtended(ok);
                }

                Ds4Color? restore = null;
                Ds4OutputVariant restoreVariant;
                lock (_gate)
                {
                    restoreVariant = _lastVariant;
                    if (_advanced && _lightRestorePending) restore = _lastLight;
                }
                if (restore is { } color && Send(conn, color, 0, 0, includeRumble: true, restoreVariant))
                {
                    lock (_gate) _lightRestorePending = false;
                    LogOutput("luz salva", restoreVariant, true);
                }

                var input = conn.Read();
                if (input is not null)
                {
                    FeedVirtualPad(input);
                    Publish(Ds4LinkState.Connected, input, input.Battery, input.Extended);
                }
            }
            catch (IOException ex)
            {
                LogDisconnected(ex.Message);
                DropConnection();
                link = Ds4LinkState.NotFound;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && !ct.IsCancellationRequested)
            {
                LogUnexpected(ex);
                DropConnection();
                ct.WaitHandle.WaitOne(1000);
            }
        }
    }

    /// <summary>Modo Xbox: cria o pad virtual sob demanda e repassa as entradas. Modo PlayStation: remove o pad.</summary>
    private void FeedVirtualPad(Ds4Input input)
    {
        VirtualXboxPad? pad;
        lock (_gate)
        {
            if (_mode != Ds4RecognitionMode.Xbox)
            {
                pad = null;
            }
            else
            {
                if (_virtualPad is null && DateTime.UtcNow >= _nextVirtualAttempt)
                {
                    _virtualPad = VirtualXboxPad.TryCreate(out _virtualError);
                    _nextVirtualAttempt = DateTime.UtcNow.AddSeconds(5);
                    if (_virtualPad is not null) _virtualPad.RumbleRequested += OnGameRumble;
                    LogVirtual(_virtualPad is not null, _virtualError);
                }
                pad = _virtualPad;
            }
        }
        if (pad is null)
        {
            ReleaseVirtualPad(onlyIfModeChanged: true);
            return;
        }
        var profile = ProfileProvider?.Invoke();
        pad.Update(profile is null ? input.State : InputProcessor.Apply(input.State, profile));
    }

    private void ReleaseVirtualPad(bool onlyIfModeChanged = false)
    {
        VirtualXboxPad? pad;
        lock (_gate)
        {
            if (onlyIfModeChanged && _mode == Ds4RecognitionMode.Xbox) return;
            pad = _virtualPad;
            _virtualPad = null;
        }
        if (pad is null) return;
        pad.RumbleRequested -= OnGameRumble;
        pad.Dispose();
        LogVirtualReleased();
    }

    /// <summary>
    /// Vibração pedida pelo jogo ao Xbox virtual → motores do DS4. Por Bluetooth isso ativaria o modo estendido,
    /// então só repassa com "Recursos avançados" ligado (decisão 1 do PRODUCT). Por USB repassa sempre.
    /// </summary>
    private void OnGameRumble(object? sender, (byte Large, byte Small) e)
    {
        Ds4Connection? conn;
        Ds4Color? light;
        Ds4OutputVariant variant;
        lock (_gate)
        {
            conn = _current.Link == Ds4LinkState.Connected ? _connection : null;
            if (conn is null || (conn.IsBluetooth && !_advanced)) return;
            light = _lastLight;
            variant = _lastVariant;
        }
        var profile = ProfileProvider?.Invoke();
        var large = profile is null ? e.Large : InputProcessor.Vibration(e.Large, profile);
        var small = profile is null ? e.Small : InputProcessor.Vibration(e.Small, profile);
        lock (_gate)
        {
            if (_rumbleLeft == large && _rumbleRight == small) return; // nada mudou
        }
        if (Send(conn, light, large, small, includeRumble: true, variant))
            lock (_gate) (_rumbleLeft, _rumbleRight) = (large, small);
    }

    private Ds4Connection? EnsureConnection()
    {
        lock (_gate)
        {
            if (_connection is not null) return _connection;
        }
        var devices = Ds4Connection.FindDevices();
        if (devices.Count == 0) return null;
        var conn = Ds4Connection.TryOpen(devices[0]);
        lock (_gate) _connection = conn;
        return conn;
    }

    private void DropConnection()
    {
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
            _extendedForThisLink = false;
        }
    }

    private void Publish(Ds4LinkState link, Ds4Input? input, Ds4Battery? battery, bool extended)
    {
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (input is not null) _reportTimes.Enqueue(now);
            while (_reportTimes.Count > 0 && now - _reportTimes.Peek() > TimeSpan.FromSeconds(1)) _reportTimes.Dequeue();

            // A bateria só chega no modo estendido; mantém a última leitura enquanto o link durar.
            var keptBattery = link == Ds4LinkState.Connected ? battery ?? _current.Battery : null;
            _current = new Ds4Snapshot(link, _connection?.IsBluetooth ?? false, input ?? (link == Ds4LinkState.Connected ? _current.Input : null),
                keptBattery, extended || (link == Ds4LinkState.Connected && _current.ExtendedActive), _reportTimes.Count,
                _mode, _virtualPad?.UserIndex, _virtualPad is not null, _virtualError);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DS4: modo de reconhecimento {Mode}")]
    private partial void LogMode(Ds4RecognitionMode mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "DS4: Xbox virtual criado={Created} erro={Error}")]
    private partial void LogVirtual(bool created, string? error);

    [LoggerMessage(Level = LogLevel.Information, Message = "DS4: Xbox virtual removido")]
    private partial void LogVirtualReleased();

    [LoggerMessage(Level = LogLevel.Information, Message = "DS4: cabo USB detectado, trocando de conexão")]
    private partial void LogSwitchToUsb();

    [LoggerMessage(Level = LogLevel.Information, Message = "DS4: link {Link}")]
    private partial void LogLink(Ds4LinkState link);

    [LoggerMessage(Level = LogLevel.Information, Message = "DS4: recursos avançados {Enabled}")]
    private partial void LogAdvanced(bool enabled);

    [LoggerMessage(Level = LogLevel.Information, Message = "DS4: modo estendido solicitado, sucesso={Ok}")]
    private partial void LogExtended(bool ok);

    [LoggerMessage(Level = LogLevel.Information, Message = "DS4: envio de {What} via {Variant}, aceito={Ok}")]
    private partial void LogOutput(string what, Ds4OutputVariant variant, bool ok);

    [LoggerMessage(Level = LogLevel.Information, Message = "DS4: desconectado ({Reason})")]
    private partial void LogDisconnected(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DS4: erro inesperado no loop de leitura")]
    private partial void LogUnexpected(Exception ex);
}
