namespace NovaControlCenter.Core.Models;

/// <summary>Preferências do PS4 que sobrevivem entre sessões. Tipos primitivos para manter o Core sem dependências.</summary>
public sealed record Ds4Settings
{
    public bool AdvancedEnabled { get; init; }

    /// <summary>"PlayStation" ou "Xbox".</summary>
    public string RecognitionMode { get; init; } = "PlayStation";

    /// <summary>Cor RRGGBB; null = nunca escolhida.</summary>
    public string? LightColor { get; init; }

    /// <summary>Formato de envio pelo Bluetooth (nome do enum).</summary>
    public string OutputVariant { get; init; } = "LinuxControl";
}

/// <summary>Comportamento da janela e do app em segundo plano.</summary>
public sealed record AppBehavior
{
    /// <summary>Fechar ou minimizar esconde o app na bandeja em vez de encerrar.</summary>
    public bool KeepInTray { get; init; } = true;

    /// <summary>Abrir já escondido na bandeja.</summary>
    public bool StartMinimized { get; init; }
}

/// <summary>Raiz do arquivo de configurações. <see cref="Version"/> permite migrar o formato no futuro.</summary>
public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public Ds4Settings Ds4 { get; init; } = new();

    public AppBehavior App { get; init; } = new();

    /// <summary>Perfis do usuário; null/vazio = criar os perfis iniciais.</summary>
    public IReadOnlyList<InputProfile>? Profiles { get; init; }

    public Guid? ActiveProfileId { get; init; }
}
