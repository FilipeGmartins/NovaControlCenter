using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Core.Interfaces;

/// <summary>Persistência das preferências do usuário.</summary>
public interface ISettingsStore
{
    /// <summary>Carrega as preferências. Nunca lança: em caso de arquivo ausente ou inválido, devolve o padrão.</summary>
    AppSettings Load();

    /// <summary>Grava de forma atômica. Devolve false se não foi possível gravar.</summary>
    bool Save(AppSettings settings);
}
