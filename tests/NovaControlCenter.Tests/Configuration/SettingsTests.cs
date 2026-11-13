using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using NovaControlCenter.App.Services;
using NovaControlCenter.App.ViewModels;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.Configuration;
using NovaControlCenter.Infrastructure.Ds4;
using NSubstitute;

namespace NovaControlCenter.Tests.Configuration;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nova-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    private JsonSettingsStore Store() => new(NullLogger<JsonSettingsStore>.Instance, FilePath);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MissingFile_ReturnsDefaults() =>
        Assert.Equal(new AppSettings().Ds4, Store().Load().Ds4);

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var settings = new AppSettings
        {
            Ds4 = new Ds4Settings { AdvancedEnabled = true, RecognitionMode = "Xbox", LightColor = "9B6BFF", OutputVariant = "Ds4WindowsControl" },
        };

        Assert.True(Store().Save(settings));

        Assert.Equal(settings.Ds4, Store().Load().Ds4);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void CorruptFile_ReturnsDefaultsAndKeepsBackup()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ isto não é json");

        var loaded = Store().Load();

        Assert.Equal(new AppSettings().Ds4, loaded.Ds4);
        Assert.False(File.Exists(FilePath));
        Assert.Single(Directory.GetFiles(_dir, "settings.json.corrompido-*"));
    }
}

public class Ds4PreferencesTests
{
    [Fact]
    public void Apply_ForwardsAllPreferences()
    {
        var ds4 = Substitute.For<IDs4Controller>();
        Ds4Preferences.Apply(new Ds4Settings { AdvancedEnabled = true, RecognitionMode = "Xbox", LightColor = "FF8000", OutputVariant = "LinuxInterrupt" }, ds4);

        ds4.Received().AdvancedEnabled = true;
        ds4.Received().Mode = Ds4RecognitionMode.Xbox;
        ds4.Received().RestorePreferences(new Ds4Color(255, 128, 0), Ds4OutputVariant.LinuxInterrupt);
    }

    [Fact]
    public void Apply_InvalidValuesFallBackToDefaults()
    {
        var ds4 = Substitute.For<IDs4Controller>();
        Ds4Preferences.Apply(new Ds4Settings { RecognitionMode = "???", LightColor = "zz", OutputVariant = "nada" }, ds4);

        ds4.Received().Mode = Ds4RecognitionMode.PlayStation;
        ds4.Received().RestorePreferences(null, Ds4OutputVariant.LinuxControl);
    }

    [Fact]
    public void Color_RoundTrips() =>
        Assert.Equal("0A0B0C", Ds4Preferences.FormatColor(Ds4Preferences.ParseColor("0a0b0c")!.Value));
}

public class Ds4ViewModelPersistenceTests
{
    private static (Ds4ViewModel Vm, ISettingsStore Store, IDs4Controller Ds4) Create(AppSettings? saved = null)
    {
        var ds4 = Substitute.For<IDs4Controller>();
        ds4.Current.Returns(new Ds4Snapshot(Ds4LinkState.Off, false, null, null, false, 0));
        var store = Substitute.For<ISettingsStore>();
        store.Load().Returns(saved ?? new AppSettings());
        var vm = new Ds4ViewModel(ds4, null, null, store);
        return (vm, store, ds4);
    }

    [Fact]
    public void Construction_DoesNotSave()
    {
        var (_, store, _) = Create();
        store.DidNotReceiveWithAnyArgs().Save(default!);
    }

    [Fact]
    public void SavedColorAndVariant_AreRestoredInPage()
    {
        var (vm, _, _) = Create(new AppSettings { Ds4 = new Ds4Settings { LightColor = "112233", OutputVariant = "Ds4WindowsInterrupt" } });
        Assert.Equal(0x11, vm.LightR);
        Assert.Equal(0x22, vm.LightG);
        Assert.Equal(0x33, vm.LightB);
        Assert.Equal(Ds4OutputVariant.Ds4WindowsInterrupt, vm.SelectedVariant.Value);
    }

    [Fact]
    public void ChangingChoices_SavesThem()
    {
        var (vm, store, _) = Create();

        vm.Advanced = true;
        vm.SelectedMode = vm.Modes[1];
        vm.PresetCommand.Execute("ABCDEF");

        store.Received().Save(Arg.Is<AppSettings>(s =>
            s.Ds4.AdvancedEnabled && s.Ds4.RecognitionMode == "Xbox" && s.Ds4.LightColor == "ABCDEF"));
    }
}
