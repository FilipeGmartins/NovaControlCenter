using CommunityToolkit.Mvvm.ComponentModel;
using NovaControlCenter.App.Services;
using NovaControlCenter.Core.Interfaces;

namespace NovaControlCenter.App.ViewModels;

/// <summary>Opções de segundo plano: bandeja, iniciar minimizado e iniciar com o Windows.</summary>
public sealed partial class BackgroundOptionsViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly IStartupRegistration _startup;
    private readonly bool _loading;

    [ObservableProperty] private bool _keepInTray;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private string _message = "";

    public BackgroundOptionsViewModel(ISettingsStore settings, IStartupRegistration startup)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));
        _loading = true;
        var app = settings.Load().App;
        KeepInTray = app.KeepInTray;
        StartMinimized = app.StartMinimized;
        StartWithWindows = SafeIsEnabled();
        _loading = false;
    }

    partial void OnKeepInTrayChanged(bool value) => Save();

    partial void OnStartMinimizedChanged(bool value) => Save();

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading) return;
        try
        {
            _startup.SetEnabled(value);
            Message = value ? "O app vai abrir escondido na bandeja quando você entrar no Windows." : "";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            Message = $"Não foi possível alterar a inicialização com o Windows ({ex.GetType().Name}).";
        }
    }

    private void Save()
    {
        if (_loading) return;
        var current = _settings.Load();
        _settings.Save(current with { App = current.App with { KeepInTray = KeepInTray, StartMinimized = StartMinimized } });
    }

    private bool SafeIsEnabled()
    {
        try { return _startup.IsEnabled; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { return false; }
    }
}
