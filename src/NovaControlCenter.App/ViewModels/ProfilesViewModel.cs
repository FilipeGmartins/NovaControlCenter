using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaControlCenter.App.Services;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Core.Services;

namespace NovaControlCenter.App.ViewModels;

/// <summary>Um perfil na lista.</summary>
public sealed record ProfileItem(Guid Id, string Name, bool IsActive, bool IsDefault, string Summary)
{
    public override string ToString() => IsActive ? $"{Name} (ativo)" : Name;
}

/// <summary>Página Perfis: criar, duplicar, renomear, excluir e ativar.</summary>
public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly ProfileService _profiles;
    private readonly IUserPrompt _prompt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanDelete))]
    private ProfileItem? _selected;

    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _messageIsError;

    public ProfilesViewModel(ProfileService profiles, IUserPrompt prompt)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _profiles.Changed += (_, _) => Rebuild(Selected?.Id);
        Rebuild(_profiles.Active.Id);
    }

    public ObservableCollection<ProfileItem> Items { get; } = [];

    public bool HasSelection => Selected is not null;

    public bool CanDelete => Selected is { IsDefault: false };

    partial void OnSelectedChanged(ProfileItem? value) => EditName = value?.Name ?? "";

    [RelayCommand]
    private void Activate(ProfileItem? item)
    {
        if (item is null) return;
        Show(_profiles.Activate(item.Id), $"Perfil \"{item.Name}\" ativado.");
    }

    [RelayCommand]
    private void Create()
    {
        var result = _profiles.Create(NewName);
        if (Show(result, $"Perfil \"{NewName.Trim()}\" criado a partir do perfil ativo."))
        {
            NewName = "";
            Rebuild(result.Profile!.Id);
        }
    }

    [RelayCommand]
    private void Duplicate(ProfileItem? item)
    {
        if (item is null) return;
        var name = UniqueCopyName(item.Name);
        var result = _profiles.Create(name, item.Id);
        if (Show(result, $"Cópia \"{name}\" criada.")) Rebuild(result.Profile!.Id);
    }

    [RelayCommand]
    private void Rename()
    {
        if (Selected is null) return;
        Show(_profiles.Rename(Selected.Id, EditName), "Nome atualizado.");
    }

    [RelayCommand]
    private void Delete(ProfileItem? item)
    {
        item ??= Selected;
        if (item is null) return;
        if (!_prompt.Confirm($"Excluir o perfil \"{item.Name}\"? Esta ação não pode ser desfeita.", "Excluir perfil")) return;
        Show(_profiles.Delete(item.Id), $"Perfil \"{item.Name}\" excluído.");
    }

    internal static string Summarize(InputProfile p) => string.Create(CultureInfo.InvariantCulture,
        $"Zona morta {p.LeftStick.Deadzone * 100:0}% / {p.RightStick.Deadzone * 100:0}% · Curva {p.LeftStick.Curve:0.0} / {p.RightStick.Curve:0.0} · Gatilho {p.Triggers.FullPressAt * 100:0}% · Vibração {p.Vibration * 100:0}%");

    private string UniqueCopyName(string baseName)
    {
        var names = _profiles.Profiles.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = $"{baseName} (cópia)";
        for (var i = 2; names.Contains(candidate); i++) candidate = $"{baseName} (cópia {i})";
        return candidate.Length > ProfileService.MaxNameLength ? candidate[..ProfileService.MaxNameLength] : candidate;
    }

    private bool Show(ProfileResult result, string success)
    {
        MessageIsError = !result.Ok;
        Message = result.Ok ? success : result.Error ?? "Não foi possível concluir.";
        return result.Ok;
    }

    private void Rebuild(Guid? select)
    {
        var active = _profiles.Active.Id;
        Items.Clear();
        foreach (var p in _profiles.Profiles)
            Items.Add(new ProfileItem(p.Id, p.Name, p.Id == active, p.IsDefault, Summarize(p)));
        Selected = Items.FirstOrDefault(i => i.Id == select) ?? Items.FirstOrDefault(i => i.IsActive);
    }
}
