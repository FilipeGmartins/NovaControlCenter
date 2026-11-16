using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NovaControlCenter.App.Presentation;
using NovaControlCenter.App.Services;
using NovaControlCenter.App.ViewModels;
using NovaControlCenter.App.Views;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Services;
using NovaControlCenter.Infrastructure.Audio;
using NovaControlCenter.Infrastructure.Configuration;
using NovaControlCenter.Infrastructure.DeviceDetection;
using NovaControlCenter.Infrastructure.Ds4;
using NovaControlCenter.Infrastructure.XInput;
using Serilog;

namespace NovaControlCenter.App;

/// <summary>Composition root: monta o host, inicia os monitores e abre a janela principal.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "O ícone da bandeja é descartado em OnExit, fim do ciclo de vida do Application.")]
public partial class App : Application
{
    public static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaControlCenter", "logs");

    /// <summary>Argumento usado por "Iniciar com o Windows": abre direto na bandeja.</summary>
    public const string MinimizedArgument = "--minimized";

    private static readonly TimeSpan TrimDelay = TimeSpan.FromSeconds(2);

    private IHost? _host;
    private TrayIcon? _tray;
    private UiActivity? _activity;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(LogDirectory, "nova-.log"), formatProvider: System.Globalization.CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        DispatcherUnhandledException += OnUnhandledException;

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        var services = _host.Services;
        services.GetRequiredService<ConnectionMonitor>().Start();
        services.GetRequiredService<InputMonitor>().Start();
        // Preferências salvas são aplicadas antes de o serviço começar e antes de a tela ser criada.
        var settings = services.GetRequiredService<ISettingsStore>().Load();
        Ds4Preferences.Apply(settings.Ds4, services.GetRequiredService<IDs4Controller>());
        var profiles = services.GetRequiredService<ProfileService>();
        services.GetRequiredService<Ds4Service>().ProfileProvider = () => profiles.Active;
        services.GetRequiredService<Ds4Service>().Start();

        // Em segundo plano, a leitura do GameSir (só usada pelas telas) fica pausada.
        _activity = services.GetRequiredService<UiActivity>();
        var gamesirInput = services.GetRequiredService<InputMonitor>();
        _activity.VisibilityChanged += (_, visible) => gamesirInput.Suspended = !visible;

        Log.Information("Nova Control Center iniciado");
        var window = services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Closing += OnWindowClosing;
        window.StateChanged += (_, _) =>
        {
            if (window.WindowState == WindowState.Minimized && KeepInTray) HideToTray();
        };

        var connection = services.GetRequiredService<ConnectionMonitor>();
        var ds4 = services.GetRequiredService<IDs4Controller>();
        _tray = new TrayIcon(ds4,
            statusText: () => $"GameSir: {Presenter.Connection(connection.Current).Title}\nPS4: {Ds4Presenter.Link(ds4.Current).Title}",
            open: ShowFromTray,
            exit: ExitApp,
            setMode: mode => services.GetRequiredService<Ds4ViewModel>().SelectMode(mode));

        var startHidden = e.Args.Contains(MinimizedArgument, StringComparer.OrdinalIgnoreCase) || settings.App.StartMinimized;
        if (startHidden) HideToTray(notify: false);
        else window.Show();
    }

    private bool KeepInTray => _host?.Services.GetRequiredService<ISettingsStore>().Load().App.KeepInTray ?? false;

    /// <summary>Esconde a janela, pausa o trabalho de tela e devolve memória ao Windows.</summary>
    private void HideToTray(bool notify = true)
    {
        MainWindow?.Hide();
        _activity?.SetVisible(false);
        if (notify) _tray?.NotifyMinimized();
        Log.Information("Janela escondida na bandeja");

        // Espera a janela sumir antes de liberar a memória (o trim antes disso seria desfeito pelo desenho).
        var trim = new DispatcherTimer { Interval = TrimDelay };
        trim.Tick += (_, _) =>
        {
            trim.Stop();
            if (_activity is { IsVisible: false }) MemoryTrimmer.Trim();
        };
        trim.Start();
    }

    private void ShowFromTray()
    {
        if (MainWindow is not { } window) return;
        _activity?.SetVisible(true);
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void ExitApp()
    {
        _exiting = true;
        Shutdown();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting) return;
        if (KeepInTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _exiting = true;
        Dispatcher.BeginInvoke(Shutdown);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        if (_host is not null)
        {
            _host.Services.GetRequiredService<MonitorViewModel>().StopVibration();
            // Monitores são IAsyncDisposable; o código interno usa ConfigureAwait(false), então não há deadlock aqui.
            ((IAsyncDisposable)_host).DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        Log.Information("Nova Control Center encerrado");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Hardware
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<HidDevicePresence>();
        services.AddSingleton<IDevicePresence>(sp => sp.GetRequiredService<HidDevicePresence>());
        services.AddSingleton<IInputReader, XInputReader>();
        services.AddSingleton<IRumbleService, XInputRumble>();
        services.AddSingleton<IBatteryProvider, XInputBatteryProvider>();
        services.AddSingleton<IControllerAudio, ControllerAudio>();
        services.AddSingleton<Ds4Service>();
        services.AddSingleton<IDs4Controller>(sp => sp.GetRequiredService<Ds4Service>());
        services.AddSingleton<UiActivity>();
        services.AddSingleton<IStartupRegistration, RegistryStartupRegistration>();
        services.AddSingleton<BackgroundOptionsViewModel>();
        services.AddSingleton<ISettingsStore>(sp => new JsonSettingsStore(sp.GetRequiredService<ILogger<JsonSettingsStore>>()));

        // Core
        services.AddSingleton<BatteryService>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton(sp => new ConnectionMonitor(
            sp.GetRequiredService<IDevicePresence>(),
            sp.GetRequiredService<IInputReader>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<ConnectionMonitor>>()));
        services.AddSingleton(sp =>
        {
            var connection = sp.GetRequiredService<ConnectionMonitor>();
            return new InputMonitor(
                sp.GetRequiredService<IInputReader>(),
                () => connection.Current.Controller?.XInputSlot,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<InputMonitor>>());
        });

        // UI
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MonitorViewModel>();
        services.AddSingleton<Ds4ViewModel>();
        services.AddSingleton<IUserPrompt, MessageBoxUserPrompt>();
        services.AddSingleton(sp => new SettingsViewModel(
            sp.GetRequiredService<ProfileService>(), sp.GetRequiredService<InputMonitor>(), sp.GetRequiredService<IDs4Controller>(),
            activity: sp.GetRequiredService<UiActivity>(), background: sp.GetRequiredService<BackgroundOptionsViewModel>()));
        services.AddSingleton<ProfilesViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Erro não tratado na interface");
        MessageBox.Show(
            $"Ocorreu um erro inesperado:\n\n{e.Exception.Message}\n\nDetalhes foram gravados em:\n{LogDirectory}",
            "Nova Control Center", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
