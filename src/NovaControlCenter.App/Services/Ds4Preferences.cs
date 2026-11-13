using System.Globalization;
using NovaControlCenter.Core.Models;
using NovaControlCenter.Infrastructure.Ds4;

namespace NovaControlCenter.App.Services;

/// <summary>Converte entre o arquivo de configurações (tipos primitivos) e o serviço do DS4.</summary>
public static class Ds4Preferences
{
    /// <summary>Aplica as preferências salvas ao serviço (chamado na inicialização). Valores inválidos viram o padrão.</summary>
    public static void Apply(Ds4Settings settings, IDs4Controller ds4)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ds4);
        ds4.AdvancedEnabled = settings.AdvancedEnabled;
        ds4.Mode = Enum.TryParse<Ds4RecognitionMode>(settings.RecognitionMode, out var mode) ? mode : Ds4RecognitionMode.PlayStation;
        ds4.RestorePreferences(ParseColor(settings.LightColor), ParseVariant(settings.OutputVariant));
    }

    public static Ds4OutputVariant ParseVariant(string? value) =>
        Enum.TryParse<Ds4OutputVariant>(value, out var v) ? v : Ds4OutputVariant.LinuxControl;

    public static Ds4Color? ParseColor(string? hex)
    {
        if (hex is not { Length: 6 }) return null;
        try
        {
            var rgb = Convert.FromHexString(hex);
            return new Ds4Color(rgb[0], rgb[1], rgb[2]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string FormatColor(Ds4Color c) => string.Create(CultureInfo.InvariantCulture, $"{c.R:X2}{c.G:X2}{c.B:X2}");
}
