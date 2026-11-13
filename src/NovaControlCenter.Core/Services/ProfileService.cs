using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Services;

/// <summary>Resultado de uma operação de perfil, com mensagem para o usuário quando falha.</summary>
public sealed record ProfileResult(bool Ok, string? Error = null, InputProfile? Profile = null)
{
    public static ProfileResult Fail(string error) => new(false, error);
}

/// <summary>
/// Perfis: criar, renomear, excluir, editar e ativar. Persiste em <see cref="ISettingsStore"/>.
/// Regras: nome obrigatório, até 40 caracteres e único (sem diferenciar maiúsculas); o "Padrão" não pode ser excluído.
/// </summary>
public sealed class ProfileService
{
    public const int MaxNameLength = 40;

    private readonly ISettingsStore _store;
    private readonly object _gate = new();
    private List<InputProfile> _profiles;
    private Guid _activeId;

    public ProfileService(ISettingsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        var saved = store.Load();
        _profiles = saved.Profiles is { Count: > 0 } list ? list.Select(p => p.Normalized()).ToList() : [.. InputProfile.BuiltIns()];
        if (!_profiles.Any(p => p.IsDefault)) _profiles.Insert(0, InputProfile.BuiltIns()[0]);
        _activeId = _profiles.Any(p => p.Id == saved.ActiveProfileId) ? saved.ActiveProfileId!.Value : _profiles[0].Id;

        // Primeira execução (ou arquivo sem perfis): grava já, para os ids não mudarem a cada abertura.
        if (saved.Profiles is not { Count: > 0 } || saved.ActiveProfileId != _activeId)
            _store.Save(saved with { Profiles = _profiles.ToList(), ActiveProfileId = _activeId });
    }

    /// <summary>Disparado quando a lista, o perfil ativo ou um ajuste muda.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<InputProfile> Profiles
    {
        get { lock (_gate) return _profiles.ToList(); }
    }

    public InputProfile Active
    {
        get { lock (_gate) return _profiles.First(p => p.Id == _activeId); }
    }

    public ProfileResult Activate(Guid id) => Mutate(() =>
    {
        if (_profiles.All(p => p.Id != id)) return ProfileResult.Fail("Perfil não encontrado.");
        _activeId = id;
        return new ProfileResult(true);
    });

    /// <summary>Cria um perfil copiando os ajustes de <paramref name="templateId"/> (ou do ativo).</summary>
    public ProfileResult Create(string name, Guid? templateId = null) => Mutate(() =>
    {
        if (Validate(name, null) is { } error) return ProfileResult.Fail(error);
        var template = _profiles.FirstOrDefault(p => p.Id == (templateId ?? _activeId)) ?? _profiles[0];
        var created = template with { Id = Guid.NewGuid(), Name = name.Trim(), IsDefault = false };
        _profiles.Add(created);
        return new ProfileResult(true, Profile: created);
    });

    public ProfileResult Rename(Guid id, string name) => Mutate(() =>
    {
        var index = _profiles.FindIndex(p => p.Id == id);
        if (index < 0) return ProfileResult.Fail("Perfil não encontrado.");
        if (Validate(name, id) is { } error) return ProfileResult.Fail(error);
        _profiles[index] = _profiles[index] with { Name = name.Trim() };
        return new ProfileResult(true, Profile: _profiles[index]);
    });

    public ProfileResult Delete(Guid id) => Mutate(() =>
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == id);
        if (profile is null) return ProfileResult.Fail("Perfil não encontrado.");
        if (profile.IsDefault) return ProfileResult.Fail("O perfil Padrão não pode ser excluído.");
        _profiles.Remove(profile);
        if (_activeId == id) _activeId = _profiles.First(p => p.IsDefault).Id;
        return new ProfileResult(true);
    });

    /// <summary>Atualiza os ajustes de um perfil (nome e id são preservados).</summary>
    public ProfileResult Update(InputProfile profile) => Mutate(() =>
    {
        ArgumentNullException.ThrowIfNull(profile);
        var index = _profiles.FindIndex(p => p.Id == profile.Id);
        if (index < 0) return ProfileResult.Fail("Perfil não encontrado.");
        var current = _profiles[index];
        _profiles[index] = profile.Normalized() with { Name = current.Name, IsDefault = current.IsDefault };
        return new ProfileResult(true, Profile: _profiles[index]);
    });

    private string? Validate(string? name, Guid? exceptId)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0) return "Digite um nome para o perfil.";
        if (trimmed.Length > MaxNameLength) return $"Use no máximo {MaxNameLength} caracteres.";
        if (_profiles.Any(p => p.Id != exceptId && string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return "Já existe um perfil com esse nome.";
        return null;
    }

    private ProfileResult Mutate(Func<ProfileResult> change)
    {
        ProfileResult result;
        lock (_gate)
        {
            result = change();
            if (result.Ok)
            {
                var settings = _store.Load();
                _store.Save(settings with { Profiles = _profiles.ToList(), ActiveProfileId = _activeId });
            }
        }
        if (result.Ok) Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }
}
