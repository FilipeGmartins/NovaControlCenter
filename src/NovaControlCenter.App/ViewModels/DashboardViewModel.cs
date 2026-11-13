using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NovaControlCenter.App.Presentation;
using NovaControlCenter.App.Services;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;

namespace NovaControlCenter.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly BatteryService _battery;
    private readonly TimeProvider _time;
    private ConnectionSnapshot _snapshot = ConnectionSnapshot.NotFound;

    [ObservableProperty] private StatusKind _statusKind = StatusKind.Error;
    [ObservableProperty] private string _statusTitle = string.Empty;
    [ObservableProperty] private string _statusDescription = string.Empty;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _deviceName = Presenter.Dash;
    [ObservableProperty] private string _modelNote = Presenter.Dash;
    [ObservableProperty] private string _connectionType = Presenter.Dash;
    [ObservableProperty] private string _receiver = Presenter.Dash;
    [ObservableProperty] private string _slot = Presenter.Dash;
    [ObservableProperty] private string _batteryTitle = Presenter.Dash;
    [ObservableProperty] private string _batteryDetail = string.Empty;
    [ObservableProperty] private string _connectedFor = Presenter.Dash;

    public DashboardViewModel(ConnectionMonitor connection, BatteryService battery, IUiDispatcher ui, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(ui);
        _battery = battery ?? throw new ArgumentNullException(nameof(battery));
        _time = time ?? throw new ArgumentNullException(nameof(time));

        connection.SnapshotChanged += (_, s) => ui.Post(() => Apply(s));
        Apply(connection.Current);

        var clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        clock.Tick += (_, _) => UpdateDuration();
        clock.Start();
    }

    private void Apply(ConnectionSnapshot snapshot)
    {
        _snapshot = snapshot;
        var status = Presenter.Connection(snapshot);
        StatusKind = status.Kind;
        StatusTitle = status.Title;
        StatusDescription = status.Description;
        IsConnected = snapshot.State == ConnectionState.Connected;

        var c = snapshot.Controller;
        DeviceName = c?.DisplayName ?? Presenter.Dash;
        ModelNote = Presenter.Model(c);
        ConnectionType = c?.ConnectionType ?? ConnectionResolver.ConnectionType;
        Receiver = Presenter.Receiver(snapshot);
        Slot = IsConnected ? Presenter.Slot(c) : Presenter.Dash;

        // A bateria é consultada a cada mudança de conexão; hoje nenhuma fonte real existe (HARDWARE_DIAGNOSTICS §4).
        (BatteryTitle, BatteryDetail) = Presenter.Battery(_battery.GetStatus(snapshot));
        UpdateDuration();
    }

    private void UpdateDuration() =>
        ConnectedFor = _snapshot.ConnectedSince is { } since ? Presenter.Duration(_time.GetUtcNow() - since) : Presenter.Dash;
}
