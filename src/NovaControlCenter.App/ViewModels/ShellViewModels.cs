using System.Diagnostics;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaControlCenter.App.Presentation;
using NovaControlCenter.App.Services;
using System.Windows.Threading;
using NovaControlCenter.Core.Services;
using NovaControlCenter.Infrastructure.Ds4;

namespace NovaControlCenter.App.ViewModels;

/// <summary>Item da navegação lateral. Glyph usa a fonte Segoe MDL2 Assets (nativa do Windows 10).</summary>
public sealed record NavItem(string Title, string Glyph, object ViewModel);

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private NavItem _selectedPage;
    [ObservableProperty] private StatusKind _statusKind = StatusKind.Error;
    [ObservableProperty] private string _statusTitle = string.Empty;
    [ObservableProperty] private StatusKind _ds4StatusKind = StatusKind.Error;
    [ObservableProperty] private string _ds4StatusTitle = string.Empty;

    public MainViewModel(DashboardViewModel dashboard, MonitorViewModel monitor, Ds4ViewModel ds4, SettingsViewModel settings, ProfilesViewModel profiles, AboutViewModel about,
        ConnectionMonitor connection, IUiDispatcher ui, IDs4Controller ds4Controller)
    {
        ArgumentNullException.ThrowIfNull(ds4Controller);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(ui);

        Pages =
        [
            new NavItem("Dashboard", "", dashboard),
            new NavItem("Monitoramento", "", monitor),
            new NavItem("Controle PS4", "", ds4),
            new NavItem("Configurações", "", settings),
            new NavItem("Perfis", "", profiles),
            new NavItem("Sobre", "", about),
        ];
        _selectedPage = Pages[0];

        connection.SnapshotChanged += (_, s) => ui.Post(() => ApplyStatus(Presenter.Connection(s)));
        ApplyStatus(Presenter.Connection(connection.Current));

        // Status do PS4 no rodapé (o serviço do DS4 é consultado, não gera eventos).
        var ds4Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        ds4Timer.Tick += (_, _) => ApplyDs4(Ds4Presenter.Link(ds4Controller.Current));
        ds4Timer.Start();
        ApplyDs4(Ds4Presenter.Link(ds4Controller.Current));
    }

    private void ApplyDs4(StatusText status)
    {
        Ds4StatusKind = status.Kind;
        Ds4StatusTitle = status.Title;
    }

    public IReadOnlyList<NavItem> Pages { get; }

    private void ApplyStatus(StatusText status)
    {
        StatusKind = status.Kind;
        StatusTitle = status.Title;
    }
}

public sealed record PlaceholderViewModel(string Title, string Description, string Availability);

public sealed partial class AboutViewModel : ObservableObject
{
    public string Version { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    public string LogDirectory => App.LogDirectory;

    public IReadOnlyList<string> Facts { get; } =
    [
        "Software independente — não usa nem requer o aplicativo oficial da GameSir.",
        "Funciona offline. Nenhum dado sai do seu computador.",
        "Nenhum comando desconhecido é enviado ao controle. A única escrita é a vibração, via XInput (API pública da Microsoft).",
        "A bateria não é exibida porque o receptor 2.4 GHz não a informa ao Windows (investigação documentada em docs/HARDWARE_DIAGNOSTICS.md).",
    ];

    public IReadOnlyList<string> Licenses { get; } =
    [
        "CommunityToolkit.Mvvm — MIT",
        "Microsoft.Extensions.Hosting — MIT",
        "HidSharp — Apache-2.0",
        "Serilog — Apache-2.0",
    ];

    [RelayCommand]
    private void OpenLogs()
    {
        Directory.CreateDirectory(LogDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{LogDirectory}\"") { UseShellExecute = true });
    }
}
