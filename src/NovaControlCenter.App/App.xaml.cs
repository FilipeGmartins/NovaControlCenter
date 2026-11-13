using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
public partial class App : Application
{
    public static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaControlCenter", "logs");

    private IHost? _host;

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

        Log.Information("Nova Control Center iniciado");
        MainWindow = services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
            sp.GetRequiredService<ProfileService>(), sp.GetRequiredService<InputMonitor>(), sp.GetRequiredService<IDs4Controller>()));
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
