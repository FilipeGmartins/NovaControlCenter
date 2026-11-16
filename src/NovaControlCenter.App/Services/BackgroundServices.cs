using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;

namespace NovaControlCenter.App.Services;

/// <summary>
/// Sabe se a janela está visível e pausa o trabalho que só serve à tela (timers de desenho a ~60 fps)
/// enquanto o app está na bandeja. Os serviços de hardware (ex.: PS4 no modo Xbox) continuam rodando.
/// </summary>
public sealed class UiActivity
{
    private readonly List<(DispatcherTimer Timer, bool WasRunning)> _timers = [];

    public bool IsVisible { get; private set; } = true;

    public event EventHandler<bool>? VisibilityChanged;

    /// <summary>Registra um timer de interface para ser parado quando a janela some.</summary>
    public DispatcherTimer Track(DispatcherTimer timer)
    {
        ArgumentNullException.ThrowIfNull(timer);
        _timers.Add((timer, timer.IsEnabled));
        if (!IsVisible) timer.Stop();
        return timer;
    }

    public void SetVisible(bool visible)
    {
        if (visible == IsVisible) return;
        IsVisible = visible;
        foreach (var (timer, _) in _timers)
        {
            if (visible) timer.Start();
            else timer.Stop();
        }
        VisibilityChanged?.Invoke(this, visible);
    }

    public int TrackedTimers => _timers.Count;
}

/// <summary>Devolve ao Windows a memória que o app não está usando (efeito visível no Gerenciador de Tarefas).</summary>
public static partial class MemoryTrimmer
{
    public static void Trim()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        using var process = Process.GetCurrentProcess();
        EmptyWorkingSet(process.Handle);
    }

    [LibraryImport("psapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyWorkingSet(IntPtr process);
}

/// <summary>"Iniciar com o Windows" (entrada do usuário atual em HKCU\...\Run; não exige administrador).</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

public sealed class RegistryStartupRegistration : IStartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NovaControlCenter";

    /// <summary>Comando gravado: o próprio executável, iniciando escondido na bandeja.</summary>
    public static string Command => $"\"{Environment.ProcessPath}\" {App.MinimizedArgument}";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
