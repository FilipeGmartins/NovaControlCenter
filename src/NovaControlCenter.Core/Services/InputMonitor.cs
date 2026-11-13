using Microsoft.Extensions.Logging;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Services;

/// <summary>Amostra de entrada com o slot de origem e o instante da leitura.</summary>
public sealed record InputSample(int Slot, ControllerState State, DateTimeOffset Timestamp);

/// <summary>
/// Lê as entradas do slot atual em segundo plano e publica apenas quando o pacote muda.
/// Observação: no Windows 10 o timer padrão tem resolução de ~15,6 ms, então a taxa efetiva fica em torno de 64 Hz,
/// suficiente para a interface (60 fps).
/// </summary>
public sealed partial class InputMonitor : IAsyncDisposable
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(8);
    private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(1);

    private readonly IInputReader _reader;
    private readonly Func<int?> _slotProvider;
    private readonly TimeProvider _time;
    private readonly ILogger<InputMonitor> _logger;
    private readonly TimeSpan _interval;
    private readonly Queue<DateTimeOffset> _changes = new();
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private InputSample? _latest;

    /// <param name="slotProvider">Slot XInput do controle atual (ex.: <c>() =&gt; connection.Current.Controller?.XInputSlot</c>).</param>
    public InputMonitor(IInputReader reader, Func<int?> slotProvider, TimeProvider time, ILogger<InputMonitor> logger, TimeSpan? interval = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _slotProvider = slotProvider ?? throw new ArgumentNullException(nameof(slotProvider));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval ?? DefaultInterval;
    }

    /// <summary>Nova amostra, ou null quando a leitura deixa de estar disponível. Disparado na thread do monitor.</summary>
    public event EventHandler<InputSample?>? InputChanged;

    public InputSample? Latest
    {
        get { lock (_gate) return _latest; }
    }

    /// <summary>Pacotes novos recebidos no último segundo.</summary>
    public int UpdatesPerSecond
    {
        get
        {
            lock (_gate)
            {
                TrimRateWindow(_time.GetUtcNow());
                return _changes.Count;
            }
        }
    }

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>Uma leitura. Público para testes.</summary>
    public void Poll()
    {
        InputSample? next;
        try
        {
            var slot = _slotProvider();
            var state = slot is int s ? _reader.Read(s) : null;
            next = state is null ? null : new InputSample(slot!.Value, state, _time.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogReadFailed(ex);
            next = null;
        }

        bool publish;
        lock (_gate)
        {
            publish = (next, _latest) switch
            {
                (null, null) => false,
                (null, _) => true,
                (_, null) => true,
                _ => next.Slot != _latest.Slot || next.State.PacketNumber != _latest.State.PacketNumber,
            };
            if (publish)
            {
                _latest = next;
                if (next is not null)
                {
                    _changes.Enqueue(next.Timestamp);
                    TrimRateWindow(next.Timestamp);
                }
            }
        }

        if (publish) InputChanged?.Invoke(this, next);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* encerramento normal */ }
        }
        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval, _time);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            Poll();
    }

    private void TrimRateWindow(DateTimeOffset now)
    {
        while (_changes.Count > 0 && now - _changes.Peek() > RateWindow)
            _changes.Dequeue();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao ler entradas do controle.")]
    private partial void LogReadFailed(Exception ex);
}
