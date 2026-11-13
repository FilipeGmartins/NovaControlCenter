using Microsoft.Extensions.Logging;
using NovaControlCenter.Core.Devices;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Services;

/// <summary>
/// Monitora a conexão do controle em segundo plano e publica <see cref="ConnectionSnapshot"/> quando muda.
/// Combina polling leve (detecta o slot XInput, que não gera eventos) com o evento de mudança de dispositivos.
/// </summary>
public sealed partial class ConnectionMonitor : IAsyncDisposable
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(500);

    private readonly IDevicePresence _presence;
    private readonly IInputReader _input;
    private readonly TimeProvider _time;
    private readonly ILogger<ConnectionMonitor> _logger;
    private readonly ConnectionDebouncer _debouncer;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private ConnectionSnapshot _current = ConnectionSnapshot.NotFound;

    public ConnectionMonitor(
        IDevicePresence presence,
        IInputReader input,
        TimeProvider time,
        ILogger<ConnectionMonitor> logger,
        TimeSpan? interval = null,
        TimeSpan? debounce = null)
    {
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval ?? DefaultInterval;
        _debouncer = new ConnectionDebouncer(time, debounce ?? ConnectionDebouncer.DefaultHold);
    }

    /// <summary>Disparado na thread do monitor. A UI deve despachar para o Dispatcher.</summary>
    public event EventHandler<ConnectionSnapshot>? SnapshotChanged;

    public ConnectionSnapshot Current
    {
        get { lock (_gate) return _current; }
    }

    public void Start()
    {
        if (_loop is not null) return;
        _presence.DevicesChanged += OnDevicesChanged;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>Executa uma verificação. Público para testes e para atualização sob demanda.</summary>
    public ConnectionSnapshot Poll()
    {
        ResolvedConnection raw;
        try
        {
            raw = ConnectionResolver.Resolve(
                _presence.GetPresentDevices(KnownDevices.GameSirVendorId),
                _input.GetConnectedSlots());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Falha transitória de hardware não derruba o monitor: mantém o último estado.
            LogQueryFailed(ex);
            return Current;
        }

        var stable = _debouncer.Update(raw);
        ConnectionSnapshot? changed = null;
        lock (_gate)
        {
            var since = stable.State == ConnectionState.Connected
                ? _current.ConnectedSince ?? _time.GetUtcNow()
                : (DateTimeOffset?)null;
            var next = new ConnectionSnapshot(stable.State, stable.Controller, since);
            if (next != _current)
            {
                _current = next;
                changed = next;
            }
        }

        if (changed is not null)
        {
            LogStateChanged(changed.State, changed.Controller?.XInputSlot, changed.Controller?.SlotMatch);
            SnapshotChanged?.Invoke(this, changed);
        }
        return Current;
    }

    public async ValueTask DisposeAsync()
    {
        _presence.DevicesChanged -= OnDevicesChanged;
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            if (_loop is not null)
            {
                try { await _loop.ConfigureAwait(false); }
                catch (OperationCanceledException) { /* encerramento normal */ }
            }
            _cts.Dispose();
        }
        _wake.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Poll();
            // Acorda no intervalo ou antes, se o Windows avisar mudança de dispositivos.
            await _wake.WaitAsync(_interval, ct).ConfigureAwait(false);
        }
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        try { _wake.Release(); }
        catch (SemaphoreFullException) { /* já há um despertar pendente */ }
        catch (ObjectDisposedException) { /* monitor encerrado */ }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao consultar dispositivos; mantendo último estado.")]
    private partial void LogQueryFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Conexão: {State} slot={Slot} ({Match})")]
    private partial void LogStateChanged(ConnectionState state, int? slot, SlotMatch? match);
}