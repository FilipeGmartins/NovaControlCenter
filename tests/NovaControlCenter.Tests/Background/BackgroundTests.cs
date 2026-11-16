using System.Windows.Threading;
using NovaControlCenter.App.Services;
using NovaControlCenter.App.ViewModels;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Tests.Profiles;

namespace NovaControlCenter.Tests.Background;

public class UiActivityTests
{
    [Fact]
    public void Hiding_StopsTrackedTimers_AndShowingRestartsThem()
    {
        var activity = new UiActivity();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Start();
        activity.Track(timer);

        activity.SetVisible(false);
        Assert.False(timer.IsEnabled);

        activity.SetVisible(true);
        Assert.True(timer.IsEnabled);
    }

    [Fact]
    public void TimerTrackedWhileHidden_IsStoppedImmediately()
    {
        var activity = new UiActivity();
        activity.SetVisible(false);
        var timer = new DispatcherTimer();
        timer.Start();

        activity.Track(timer);

        Assert.False(timer.IsEnabled);
    }

    [Fact]
    public void VisibilityChanged_FiresOnlyOnRealChanges()
    {
        var activity = new UiActivity();
        var events = new List<bool>();
        activity.VisibilityChanged += (_, v) => events.Add(v);

        activity.SetVisible(true);
        activity.SetVisible(false);
        activity.SetVisible(false);
        activity.SetVisible(true);

        Assert.Equal([false, true], events);
    }
}

internal sealed class FakeStartup : IStartupRegistration
{
    public bool IsEnabled { get; set; }
    public bool Throws { get; set; }

    public void SetEnabled(bool enabled)
    {
        if (Throws) throw new UnauthorizedAccessException("negado");
        IsEnabled = enabled;
    }
}

public class BackgroundOptionsTests
{
    [Fact]
    public void LoadsSavedValues_AndRegistryState()
    {
        var store = new MemoryStore();
        store.Save(new AppSettings { App = new AppBehavior { KeepInTray = false, StartMinimized = true } });

        var vm = new BackgroundOptionsViewModel(store, new FakeStartup { IsEnabled = true });

        Assert.False(vm.KeepInTray);
        Assert.True(vm.StartMinimized);
        Assert.True(vm.StartWithWindows);
    }

    [Fact]
    public void Toggles_ArePersisted_WithoutTouchingOtherSettings()
    {
        var store = new MemoryStore();
        store.Save(new AppSettings { Ds4 = new Ds4Settings { RecognitionMode = "Xbox" } });
        var vm = new BackgroundOptionsViewModel(store, new FakeStartup());

        vm.KeepInTray = false;
        vm.StartMinimized = true;

        Assert.False(store.Saved.App.KeepInTray);
        Assert.True(store.Saved.App.StartMinimized);
        Assert.Equal("Xbox", store.Saved.Ds4.RecognitionMode);
    }

    [Fact]
    public void StartWithWindows_UsesRegistration_AndReportsFailure()
    {
        var startup = new FakeStartup();
        var vm = new BackgroundOptionsViewModel(new MemoryStore(), startup);

        vm.StartWithWindows = true;
        Assert.True(startup.IsEnabled);

        startup.Throws = true;
        vm.StartWithWindows = false;
        Assert.Contains("Não foi possível", vm.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_KeepInTrayOn_StartMinimizedOff()
    {
        var app = new AppSettings().App;
        Assert.True(app.KeepInTray);
        Assert.False(app.StartMinimized);
    }
}

public class TrayTextTests
{
    [Fact]
    public void Fit_RespectsWindows63CharLimit()
    {
        Assert.Equal("curto", TrayIcon.Fit("curto"));
        var fitted = TrayIcon.Fit(new string('x', 100));
        Assert.Equal(63, fitted.Length);
        Assert.EndsWith("…", fitted, StringComparison.Ordinal);
    }
}
