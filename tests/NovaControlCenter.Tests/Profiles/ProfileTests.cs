using NovaControlCenter.App.Services;
using NovaControlCenter.App.ViewModels;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;
using NSubstitute;

namespace NovaControlCenter.Tests.Profiles;

/// <summary>Armazenamento em memória para testar a persistência de verdade (Load devolve o que foi salvo).</summary>
internal sealed class MemoryStore : ISettingsStore
{
    public AppSettings Saved { get; private set; } = new();
    public int Saves { get; private set; }

    public AppSettings Load() => Saved;

    public bool Save(AppSettings settings)
    {
        Saved = settings;
        Saves++;
        return true;
    }
}

public class InputProcessorTests
{
    private static readonly StickSettings Dz10Linear = new() { Deadzone = 0.10, Curve = 1.0 };

    [Fact]
    public void Stick_InsideDeadzone_IsZero() =>
        Assert.Equal(default, InputProcessor.Stick(new StickState(3000, 0), Dz10Linear)); // ~9%

    [Fact]
    public void Stick_OutsideDeadzone_IsRescaledWithoutJump()
    {
        // logo após a zona morta → perto de zero (sem "degrau")
        var justOut = InputProcessor.Stick(new StickState(3600, 0), Dz10Linear); // ~11%
        Assert.InRange(justOut.X, 0.0, 0.03);
        // fim de curso continua sendo fim de curso
        Assert.Equal(1.0, InputProcessor.Stick(new StickState(32767, 0), Dz10Linear).X, precision: 3);
    }

    [Fact]
    public void Stick_PreservesDirection()
    {
        var output = InputProcessor.Stick(new StickState(16000, 16000), Dz10Linear);
        Assert.Equal(output.X, output.Y, precision: 3);
        Assert.True(output.X > 0);
    }

    [Fact]
    public void Stick_CurveAboveOne_GivesMorePrecisionInCenter()
    {
        var linear = InputProcessor.Stick(new StickState(16384, 0), new StickSettings { Deadzone = 0, Curve = 1 });
        var curved = InputProcessor.Stick(new StickState(16384, 0), new StickSettings { Deadzone = 0, Curve = 2 });
        Assert.Equal(0.5, linear.X, precision: 2);
        Assert.Equal(0.25, curved.X, precision: 2);
    }

    [Fact]
    public void Stick_DiagonalBeyondCircle_IsClampedToOne()
    {
        var output = InputProcessor.Stick(new StickState(32767, 32767), new StickSettings { Deadzone = 0, Curve = 1 });
        Assert.Equal(1.0, output.Magnitude, precision: 3);
    }

    [Theory]
    [InlineData(10, 0)]     // dentro da zona morta (10% ≈ 25)
    [InlineData(153, 255)]  // 60% do curso com "completo em 60%"
    [InlineData(255, 255)]
    public void Trigger_DeadzoneAndShortPress(byte raw, byte expected) =>
        Assert.Equal(expected, InputProcessor.Trigger(raw, new TriggerSettings { Deadzone = 0.1, FullPressAt = 0.6 }));

    [Fact]
    public void Vibration_IsScaled() =>
        Assert.Equal(100, InputProcessor.Vibration(200, new InputProfile { Vibration = 0.5 }));

    [Fact]
    public void Apply_KeepsButtonsAndPacket()
    {
        var state = ControllerState.Neutral with { PacketNumber = 9, Buttons = GamepadButtons.A };
        var output = InputProcessor.Apply(state, InputProfile.BuiltIns()[1]);
        Assert.Equal(9u, output.PacketNumber);
        Assert.Equal(GamepadButtons.A, output.Buttons);
    }

    [Fact]
    public void Normalized_ClampsOutOfRangeValues()
    {
        var p = new InputProfile { LeftStick = new() { Deadzone = 2, Curve = 99 }, Vibration = -1 }.Normalized();
        Assert.Equal(0.5, p.LeftStick.Deadzone);
        Assert.Equal(3.0, p.LeftStick.Curve);
        Assert.Equal(0, p.Vibration);
    }
}

public class ProfileServiceTests
{
    [Fact]
    public void FirstRun_SeedsBuiltInsAndPersistsImmediately()
    {
        var store = new MemoryStore();
        var service = new ProfileService(store);

        Assert.Equal(["Padrão", "FPS", "Corrida"], service.Profiles.Select(p => p.Name));
        Assert.Equal("Padrão", service.Active.Name);
        Assert.Equal(1, store.Saves);
        Assert.Equal(service.Active.Id, store.Saved.ActiveProfileId);
    }

    [Fact]
    public void Reload_KeepsIdsAndActiveProfile()
    {
        var store = new MemoryStore();
        var first = new ProfileService(store);
        first.Activate(first.Profiles[1].Id);

        var second = new ProfileService(store);

        Assert.Equal("FPS", second.Active.Name);
        Assert.Equal(first.Profiles.Select(p => p.Id), second.Profiles.Select(p => p.Id));
    }

    [Theory]
    [InlineData("", "Digite um nome")]
    [InlineData("   ", "Digite um nome")]
    [InlineData("fps", "Já existe")]
    [InlineData("12345678901234567890123456789012345678901", "no máximo 40")]
    public void Create_ValidatesName(string name, string expectedError)
    {
        var result = new ProfileService(new MemoryStore()).Create(name);
        Assert.False(result.Ok);
        Assert.Contains(expectedError, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_CopiesActiveSettingsAndTrimsName()
    {
        var service = new ProfileService(new MemoryStore());
        service.Activate(service.Profiles.Single(p => p.Name == "FPS").Id);

        var created = service.Create("  Meu FPS  ").Profile!;

        Assert.Equal("Meu FPS", created.Name);
        Assert.Equal(service.Active.RightStick, created.RightStick);
        Assert.False(created.IsDefault);
    }

    [Fact]
    public void Delete_DefaultIsBlocked_AndActiveFallsBackToDefault()
    {
        var service = new ProfileService(new MemoryStore());
        Assert.False(service.Delete(service.Profiles.Single(p => p.IsDefault).Id).Ok);

        var race = service.Profiles.Single(p => p.Name == "Corrida");
        service.Activate(race.Id);
        Assert.True(service.Delete(race.Id).Ok);
        Assert.Equal("Padrão", service.Active.Name);
    }

    [Fact]
    public void Rename_AllowsChangingCaseOfOwnName()
    {
        var service = new ProfileService(new MemoryStore());
        var fps = service.Profiles.Single(p => p.Name == "FPS");
        Assert.True(service.Rename(fps.Id, "Fps").Ok);
    }

    [Fact]
    public void Update_KeepsNameAndNormalizes()
    {
        var service = new ProfileService(new MemoryStore());
        var active = service.Active;
        service.Update(active with { Name = "Hack", Vibration = 5 });

        Assert.Equal("Padrão", service.Active.Name);
        Assert.Equal(1.0, service.Active.Vibration);
    }

    [Fact]
    public void Changes_RaiseEventAndPersist()
    {
        var store = new MemoryStore();
        var service = new ProfileService(store);
        var raised = 0;
        service.Changed += (_, _) => raised++;

        service.Create("Novo");

        Assert.Equal(1, raised);
        Assert.Contains(store.Saved.Profiles!, p => p.Name == "Novo");
    }
}

public class ProfilePagesTests
{
    [Fact]
    public void SettingsPage_LoadsActiveProfileAsPercentages()
    {
        var service = new ProfileService(new MemoryStore());
        service.Activate(service.Profiles.Single(p => p.Name == "FPS").Id);

        var vm = new SettingsViewModel(service, startTimers: false);

        Assert.Equal("FPS", vm.ProfileName);
        Assert.Equal(4, vm.RightDeadzone);
        Assert.Equal(60, vm.TriggerFullAt);
        Assert.Equal(60, vm.Vibration);
    }

    [Fact]
    public void SettingsPage_CommitWritesDraftToActiveProfile()
    {
        var service = new ProfileService(new MemoryStore());
        var vm = new SettingsViewModel(service, startTimers: false) { LeftDeadzone = 20, Vibration = 30 };

        vm.CommitNow();

        Assert.Equal(0.20, service.Active.LeftStick.Deadzone, precision: 6);
        Assert.Equal(0.30, service.Active.Vibration, precision: 6);
    }

    [Fact]
    public void ProfilesPage_CreateShowsErrorForDuplicate()
    {
        var vm = new ProfilesViewModel(new ProfileService(new MemoryStore()), Substitute.For<IUserPrompt>()) { NewName = "Corrida" };
        vm.CreateCommand.Execute(null);

        Assert.True(vm.MessageIsError);
        Assert.Equal(3, vm.Items.Count);
    }

    [Fact]
    public void ProfilesPage_DeleteRequiresConfirmation()
    {
        var prompt = Substitute.For<IUserPrompt>();
        var vm = new ProfilesViewModel(new ProfileService(new MemoryStore()), prompt);
        var fps = vm.Items.Single(i => i.Name == "FPS");

        prompt.Confirm(default!, default!).ReturnsForAnyArgs(false);
        vm.DeleteCommand.Execute(fps);
        Assert.Equal(3, vm.Items.Count);

        prompt.Confirm(default!, default!).ReturnsForAnyArgs(true);
        vm.DeleteCommand.Execute(fps);
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public void ProfilesPage_DuplicateCreatesUniqueCopyNames()
    {
        var vm = new ProfilesViewModel(new ProfileService(new MemoryStore()), Substitute.For<IUserPrompt>());
        var fps = vm.Items.Single(i => i.Name == "FPS");

        vm.DuplicateCommand.Execute(fps);
        vm.DuplicateCommand.Execute(fps);

        Assert.Contains(vm.Items, i => i.Name == "FPS (cópia)");
        Assert.Contains(vm.Items, i => i.Name == "FPS (cópia 2)");
    }

    [Fact]
    public void ProfilesPage_ActivateMarksItem()
    {
        var vm = new ProfilesViewModel(new ProfileService(new MemoryStore()), Substitute.For<IUserPrompt>());
        vm.ActivateCommand.Execute(vm.Items.Single(i => i.Name == "Corrida"));
        Assert.True(vm.Items.Single(i => i.Name == "Corrida").IsActive);
    }
}
