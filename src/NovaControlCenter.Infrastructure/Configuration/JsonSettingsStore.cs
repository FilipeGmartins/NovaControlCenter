using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaControlCenter.Core.Interfaces;
using NovaControlCenter.Core.Models;

namespace NovaControlCenter.Infrastructure.Configuration;

/// <summary>
/// Preferências em JSON. Gravação atômica (arquivo temporário + troca), backup do arquivo se ele estiver
/// corrompido e acesso serializado. Padrão: %AppData%\NovaControlCenter\settings.json.
/// </summary>
public sealed partial class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly object _gate = new();

    public JsonSettingsStore(ILogger<JsonSettingsStore> logger, string? path = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _path = path ?? DefaultPath;
    }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaControlCenter", "settings.json");

    public string FilePath => _path;

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path)) return new AppSettings();
            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Json);
                return settings ?? new AppSettings();
            }
            catch (JsonException ex)
            {
                var backup = $"{_path}.corrompido-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
                TryMove(_path, backup);
                LogCorrupt(ex, backup);
                return new AppSettings();
            }
            catch (IOException ex)
            {
                LogReadFailed(ex);
                return new AppSettings();
            }
        }
    }

    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            var temp = _path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(temp, JsonSerializer.Serialize(settings with { Version = AppSettings.CurrentVersion }, Json));
                File.Move(temp, _path, overwrite: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogWriteFailed(ex);
                try { File.Delete(temp); } catch (IOException) { /* melhor esforço */ }
                return false;
            }
        }
    }

    private static void TryMove(string from, string to)
    {
        try { File.Move(from, to, overwrite: true); }
        catch (IOException) { /* se nem o backup for possível, seguimos com o padrão */ }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Configurações corrompidas; backup em {Backup}, usando o padrão")]
    private partial void LogCorrupt(Exception ex, string backup);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao ler as configurações; usando o padrão")]
    private partial void LogReadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao gravar as configurações")]
    private partial void LogWriteFailed(Exception ex);
}
